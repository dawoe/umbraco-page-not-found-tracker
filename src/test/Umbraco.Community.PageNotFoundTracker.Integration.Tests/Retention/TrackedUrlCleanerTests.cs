// Copyright (c) Dave Woestenborghs and contributors. Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Umbraco.Community.PageNotFoundTracker.Core.Blocking;
using Umbraco.Community.PageNotFoundTracker.Core.Tracking;
using Umbraco.Community.PageNotFoundTracker.Infrastructure.Retention;
using Umbraco.Community.PageNotFoundTracker.Infrastructure.Tracking;
using Umbraco.Community.PageNotFoundTracker.Integration.Tests.Tracking;

namespace Umbraco.Community.PageNotFoundTracker.Integration.Tests.Retention;

/// <summary>
/// Tests deleting old tracked URLs in batches against a real database (SQLite, and SQL Server when configured).
/// </summary>
internal sealed class TrackedUrlCleanerTests : TrackingDatabaseFixture
{
    private static readonly DateTime Cutoff = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    private TrackedUrlCleaner cleaner = null!;

    /// <summary>
    /// Initializes a new instance of the <see cref="TrackedUrlCleanerTests"/> class.
    /// </summary>
    /// <param name="database">The database to run on.</param>
    public TrackedUrlCleanerTests(TrackingDatabase database)
        : base(database)
    {
    }

    /// <summary>
    /// Empties both tables (the fixture's database is shared by its tests) and builds the cleaner.
    /// </summary>
    /// <returns>A task.</returns>
    [SetUp]
    public async Task SetUp()
    {
        await using (PageNotFoundTrackerDbContext dbContext = this.CreateContext())
        {
            await dbContext.TrackedUrls.ExecuteDeleteAsync();
            await dbContext.BlockRules.ExecuteDeleteAsync();
        }

        var services = new ServiceCollection();
        services.AddSingleton<IDbContextFactory<PageNotFoundTrackerDbContext>>(new TestContextFactory(this));
        this.cleaner = new TrackedUrlCleaner(services.BuildServiceProvider());
    }

    /// <summary>
    /// Only rows last seen before the cut-off go; a row exactly at the cut-off, newer rows and rows dated in the future
    /// (clock skew between servers) stay.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task Deletes_only_rows_strictly_older_than_the_cutoff()
    {
        await this.SeedAsync(
            Row("/old", Cutoff.AddTicks(-10)),
            Row("/at-cutoff", Cutoff),
            Row("/newer", Cutoff.AddDays(1)),
            Row("/future", Cutoff.AddYears(5)));

        int deleted = await this.cleaner.DeleteBatchAsync(Cutoff, 1000, CancellationToken.None);

        List<string> left = await this.UrlsAsync();
        Assert.Multiple(() =>
        {
            Assert.That(deleted, Is.EqualTo(1));
            Assert.That(left, Is.EquivalentTo(new[] { "/at-cutoff", "/newer", "/future" }));
        });
    }

    /// <summary>
    /// One call deletes at most one batch, so a large backlog takes several calls.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task Deletes_at_most_one_batch_per_call()
    {
        await this.SeedAsync(Enumerable.Range(0, 2500).Select(i => Row($"/bulk-{i}", Cutoff.AddDays(-1))).ToArray());

        int[] batches =
        [
            await this.cleaner.DeleteBatchAsync(Cutoff, 1000, CancellationToken.None),
            await this.cleaner.DeleteBatchAsync(Cutoff, 1000, CancellationToken.None),
            await this.cleaner.DeleteBatchAsync(Cutoff, 1000, CancellationToken.None),
            await this.cleaner.DeleteBatchAsync(Cutoff, 1000, CancellationToken.None),
        ];

        List<string> left = await this.UrlsAsync();
        Assert.Multiple(() =>
        {
            Assert.That(batches, Is.EqualTo(new[] { 1000, 1000, 500, 0 }));
            Assert.That(left, Is.Empty);
        });
    }

    /// <summary>
    /// A row a flush updated between the select and the delete of a batch is no longer old and survives.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task Delete_keeps_a_row_seen_again_after_it_was_selected()
    {
        await this.SeedAsync(Row("/race", Cutoff.AddDays(-1)), Row("/gone", Cutoff.AddDays(-1)));

        IReadOnlyList<int> ids = await this.cleaner.SelectBatchAsync(Cutoff, 1000, CancellationToken.None);
        await this.SetLastSeenAsync("/race", Cutoff.AddDays(30));
        int deleted = await this.cleaner.DeleteAsync(ids.ToList(), Cutoff, CancellationToken.None);

        List<string> left = await this.UrlsAsync();
        Assert.Multiple(() =>
        {
            Assert.That(ids, Has.Count.EqualTo(2));
            Assert.That(deleted, Is.EqualTo(1));
            Assert.That(left, Is.EqualTo(new[] { "/race" }));
        });
    }

    /// <summary>
    /// Block rules are never removed by the clean-up, however old they are.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task Block_rules_are_untouched()
    {
        await this.SeedAsync(Row("/old", Cutoff.AddYears(-3)));
        await using (PageNotFoundTrackerDbContext dbContext = this.CreateContext())
        {
            dbContext.BlockRules.Add(new BlockRule
            {
                Pattern = "/wp-admin/*",
                PatternHash = "hash-wp-admin",
                DisplayPattern = "/wp-admin/*",
                StatusCode = 410,
                Created = Cutoff.AddYears(-3),
            });
            await dbContext.SaveChangesAsync();
        }

        await this.cleaner.DeleteBatchAsync(Cutoff, 1000, CancellationToken.None);

        await using PageNotFoundTrackerDbContext check = this.CreateContext();
        int rules = await check.BlockRules.CountAsync();
        List<string> left = await this.UrlsAsync();
        Assert.Multiple(() =>
        {
            Assert.That(rules, Is.EqualTo(1));
            Assert.That(left, Is.Empty);
        });
    }

    private static TrackedUrl Row(string url, DateTime lastSeen) => new()
    {
        Url = url,
        UrlHash = "hash" + url,
        RelativePath = url,
        DisplayUrl = url,
        HitCount = 1,
        FirstSeen = lastSeen,
        LastSeen = lastSeen,
    };

    private async Task SeedAsync(params TrackedUrl[] rows)
    {
        await using PageNotFoundTrackerDbContext dbContext = this.CreateContext();
        dbContext.TrackedUrls.AddRange(rows);
        await dbContext.SaveChangesAsync();
    }

    private async Task SetLastSeenAsync(string url, DateTime lastSeen)
    {
        await using PageNotFoundTrackerDbContext dbContext = this.CreateContext();
        await dbContext.TrackedUrls.Where(r => r.Url == url).ExecuteUpdateAsync(s => s.SetProperty(r => r.LastSeen, lastSeen));
    }

    private async Task<List<string>> UrlsAsync()
    {
        await using PageNotFoundTrackerDbContext dbContext = this.CreateContext();
        return await dbContext.TrackedUrls.AsNoTracking().Select(r => r.Url).ToListAsync();
    }

    private sealed class TestContextFactory(TrackedUrlCleanerTests owner) : IDbContextFactory<PageNotFoundTrackerDbContext>
    {
        public PageNotFoundTrackerDbContext CreateDbContext() => owner.CreateContext();
    }
}
