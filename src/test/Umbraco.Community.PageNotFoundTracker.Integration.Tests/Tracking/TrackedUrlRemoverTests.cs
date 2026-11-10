// Copyright (c) Dave Woestenborghs and contributors. Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Umbraco.Cms.Core.Configuration.Models;
using Umbraco.Community.PageNotFoundTracker.Core.Configuration;
using Umbraco.Community.PageNotFoundTracker.Core.Tracking;
using Umbraco.Community.PageNotFoundTracker.Infrastructure.Tracking;

namespace Umbraco.Community.PageNotFoundTracker.Integration.Tests.Tracking;

/// <summary>
/// Tests deleting a tracked URL against a real database (SQLite, and SQL Server when configured).
/// </summary>
internal sealed class TrackedUrlRemoverTests : TrackingDatabaseFixture
{
    private static readonly DateTime Seen = new(2026, 11, 4, 10, 0, 0, DateTimeKind.Utc);

    private TrackingQueue queue = null!;
    private TrackingFlushService flushService = null!;
    private TrackedUrlRemover remover = null!;
    private TrackedUrlQueryService queryService = null!;

    /// <summary>
    /// Initializes a new instance of the <see cref="TrackedUrlRemoverTests"/> class.
    /// </summary>
    /// <param name="database">The database to run on.</param>
    public TrackedUrlRemoverTests(TrackingDatabase database)
        : base(database)
    {
    }

    /// <summary>
    /// Builds the queue, flush service, remover and query service on the fixture's database.
    /// </summary>
    [SetUp]
    public void SetUp()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IDbContextFactory<PageNotFoundTrackerDbContext>>(new TestContextFactory(this));
        IServiceProvider sp = services.BuildServiceProvider();

        var settings = Options.Create(new PageNotFoundTrackerSettings());
        this.queue = new TrackingQueue(settings, TimeProvider.System, NullLogger<TrackingQueue>.Instance);
        var state = new TrackingPersistenceState();
        state.SetAvailable();
        this.flushService = new TrackingFlushService(
            this.queue,
            state,
            new TrackingDatabaseProviderSelector([]),
            new StaticOptionsMonitor<ConnectionStrings>(new ConnectionStrings()),
            sp,
            new DefaultTrackedUrlUpsert(NullLogger<DefaultTrackedUrlUpsert>.Instance),
            settings,
            TimeProvider.System,
            NullLogger<TrackingFlushService>.Instance);
        this.remover = new TrackedUrlRemover(this.queue, this.flushService, sp);
        this.queryService = new TrackedUrlQueryService(sp);
    }

    /// <summary>
    /// Disposes the flush service.
    /// </summary>
    [TearDown]
    public void TearDown() => this.flushService.Dispose();

    /// <summary>
    /// A queued hit does not bring a deleted row back.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task Delete_removes_row_and_a_queued_hit_does_not_bring_it_back()
    {
        TrackedUrlHit hit = Hit("/remover-a");
        await this.UpsertAsync(hit);
        TrackedUrl row = (await this.GetRowsAsync(hit.Url)).Single();
        this.queue.TryEnqueue(hit);

        bool deleted = await this.remover.DeleteAsync(row, CancellationToken.None);
        await this.flushService.FlushAsync(CancellationToken.None);

        List<TrackedUrl> after = await this.GetRowsAsync(hit.Url);
        Assert.Multiple(() =>
        {
            Assert.That(deleted, Is.True);
            Assert.That(after, Is.Empty);
        });
    }

    /// <summary>
    /// Delete waits for a flush batch in progress.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task Delete_waits_for_a_flush_in_progress()
    {
        TrackedUrlHit hit = Hit("/remover-b");
        await this.UpsertAsync(hit);
        TrackedUrl row = (await this.GetRowsAsync(hit.Url)).Single();

        var entered = new TaskCompletionSource();
        var release = new TaskCompletionSource();
        Task holder = this.flushService.RunExclusiveAsync(
            async () =>
            {
                entered.SetResult();
                await release.Task;
            },
            CancellationToken.None);
        await entered.Task;

        Task<bool> delete = this.remover.DeleteAsync(row, CancellationToken.None);
        await Task.Delay(300);
        Assert.That(delete.IsCompleted, Is.False);

        release.SetResult();
        await holder;
        Assert.That(await delete.WaitAsync(TimeSpan.FromSeconds(10)), Is.True);
    }

    /// <summary>
    /// An unknown id gives null.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task GetByIdAsync_returns_null_for_unknown_id() =>
        Assert.That(await this.queryService.GetByIdAsync(int.MaxValue, CancellationToken.None), Is.Null);

    /// <summary>
    /// Deleting by ids deletes those rows and counts them; an unknown id counts zero.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task Delete_by_ids_deletes_those_rows_and_returns_the_count()
    {
        string p = Prefix();
        List<TrackedUrl> rows = await this.SeedAsync(p, 3);

        int deleted = await this.remover.DeleteAsync([rows[0].Id, rows[1].Id, int.MaxValue, rows[0].Id, -4], CancellationToken.None);

        List<int> left = await this.IdsAsync(p);
        Assert.Multiple(() =>
        {
            Assert.That(deleted, Is.EqualTo(2));
            Assert.That(left, Is.EqualTo(new[] { rows[2].Id }));
        });
    }

    /// <summary>
    /// A queued hit does not bring a row deleted by id back.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task Delete_by_ids_discards_queued_hits_so_the_flush_does_not_bring_the_row_back()
    {
        string p = Prefix();
        TrackedUrl row = (await this.SeedAsync(p, 1)).Single();
        this.queue.TryEnqueue(HitOf(row));

        await this.remover.DeleteAsync([row.Id], CancellationToken.None);
        await this.flushService.FlushAsync(CancellationToken.None);

        Assert.That(await this.IdsAsync(p), Is.Empty);
    }

    /// <summary>
    /// Deleting by criteria deletes only the rows matching the search and both filters.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task Delete_matching_deletes_only_rows_matching_search_domain_and_culture()
    {
        string p = Prefix();
        string domain = p.Trim('/') + ".com";
        await this.InsertAsync(
            Row(p + "/old-1", domain, "nl-NL"),
            Row(p + "/old-2", domain, "nl-NL"),
            Row(p + "/new-1", domain, "nl-NL"),
            Row(p + "/old-3", domain, "en-US"),
            Row(p + "/old-4", "other-" + domain, "nl-NL"));

        int deleted = await this.remover.DeleteMatchingAsync(
            new TrackedUrlCriteria(p + "/old", TrackedUrlFilter.Exactly(domain), TrackedUrlFilter.Exactly("nl-NL")),
            CancellationToken.None);

        Assert.Multiple(async () =>
        {
            Assert.That(deleted, Is.EqualTo(2));
            Assert.That(await this.UrlsAsync(p), Is.EquivalentTo(new[] { p + "/new-1", p + "/old-3", p + "/old-4" }));
        });
    }

    /// <summary>
    /// Wildcard characters in the search are literal.
    /// </summary>
    /// <param name="special">The special character.</param>
    /// <returns>A task.</returns>
    [TestCase("%")]
    [TestCase("_")]
    [TestCase("[")]
    [TestCase("\\")]
    public async Task Delete_matching_treats_like_characters_literally(string special)
    {
        string p = Prefix();
        await this.InsertAsync(
            Row(p + "/a" + special + "b", string.Empty, string.Empty),
            Row(p + "/axb", string.Empty, string.Empty),
            Row(p + "/a" + special + special + "b", string.Empty, string.Empty));

        int deleted = await this.remover.DeleteMatchingAsync(
            new TrackedUrlCriteria(p + "/a" + special + "b", TrackedUrlFilter.Any, TrackedUrlFilter.Any),
            CancellationToken.None);

        Assert.Multiple(async () =>
        {
            Assert.That(deleted, Is.EqualTo(1));
            Assert.That(await this.UrlsAsync(p), Is.EquivalentTo(new[] { p + "/axb", p + "/a" + special + special + "b" }));
        });
    }

    /// <summary>
    /// A whitespace-only search does not filter.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task Delete_matching_with_a_whitespace_search_does_not_filter_by_search()
    {
        string p = Prefix();
        string domain = p.Trim('/') + ".com";
        await this.InsertAsync(Row(p + "/x", domain, string.Empty), Row(p + "/y", domain, string.Empty));

        int deleted = await this.remover.DeleteMatchingAsync(
            new TrackedUrlCriteria("   ", TrackedUrlFilter.Exactly(domain), TrackedUrlFilter.Any),
            CancellationToken.None);

        Assert.That(deleted, Is.EqualTo(2));
    }

    /// <summary>
    /// More than one batch is deleted and counted.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task Delete_matching_deletes_more_than_one_batch_and_returns_the_total()
    {
        string p = Prefix();
        int total = TrackedUrlRemover.BatchSize + 5;
        await this.SeedAsync(p, total);

        int deleted = await this.remover.DeleteMatchingAsync(
            new TrackedUrlCriteria(p, TrackedUrlFilter.Any, TrackedUrlFilter.Any),
            CancellationToken.None);

        Assert.Multiple(async () =>
        {
            Assert.That(deleted, Is.EqualTo(total));
            Assert.That(await this.IdsAsync(p), Is.Empty);
        });
    }

    /// <summary>
    /// A queued hit does not bring a row deleted by criteria back.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task Delete_matching_discards_queued_hits_of_the_deleted_rows()
    {
        string p = Prefix();
        TrackedUrl row = (await this.SeedAsync(p, 1)).Single();
        this.queue.TryEnqueue(HitOf(row));

        await this.remover.DeleteMatchingAsync(new TrackedUrlCriteria(p, TrackedUrlFilter.Any, TrackedUrlFilter.Any), CancellationToken.None);
        await this.flushService.FlushAsync(CancellationToken.None);

        Assert.That(await this.IdsAsync(p), Is.Empty);
    }

    /// <summary>
    /// A cancellation after the first batch keeps what was deleted and the remaining rows.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task Delete_matching_with_a_cancelled_token_keeps_what_was_deleted()
    {
        string p = Prefix();
        int total = (TrackedUrlRemover.BatchSize * 2) + 5;
        await this.SeedAsync(p, total);

        using var cts = new CancellationTokenSource();
        var cancelling = new CancelAfterFirstRunFlusher(this.flushService, cts);
        var cancellingRemover = new TrackedUrlRemover(this.queue, cancelling, this.ServiceProviderFor());

        Assert.ThrowsAsync<OperationCanceledException>(async () =>
            await cancellingRemover.DeleteMatchingAsync(new TrackedUrlCriteria(p, TrackedUrlFilter.Any, TrackedUrlFilter.Any), cts.Token));

        Assert.That((await this.IdsAsync(p)).Count, Is.EqualTo(total - TrackedUrlRemover.BatchSize));
    }

    private static string Prefix() => "/rm-" + Guid.NewGuid().ToString("N");

    private static TrackedUrl Row(string url, string domain, string culture) => new()
    {
        Url = url,
        UrlHash = Guid.NewGuid().ToString("N"),
        RelativePath = url,
        DisplayUrl = url,
        Domain = domain,
        Culture = culture,
        HitCount = 1,
        FirstSeen = Seen,
        LastSeen = Seen,
    };

    private static TrackedUrlHit HitOf(TrackedUrl row) =>
        new(row.Url, row.UrlHash, row.RelativePath, row.DisplayUrl, row.Domain, row.Culture, false, 1, Seen, Seen);

    private IServiceProvider ServiceProviderFor()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IDbContextFactory<PageNotFoundTrackerDbContext>>(new TestContextFactory(this));
        return services.BuildServiceProvider();
    }

    private async Task InsertAsync(params TrackedUrl[] rows)
    {
        await using PageNotFoundTrackerDbContext dbContext = this.CreateContext();
        dbContext.TrackedUrls.AddRange(rows);
        await dbContext.SaveChangesAsync();
    }

    private async Task<List<TrackedUrl>> SeedAsync(string prefix, int count)
    {
        TrackedUrl[] rows = Enumerable.Range(0, count).Select(i => Row($"{prefix}/{i:D5}", string.Empty, string.Empty)).ToArray();
        await this.InsertAsync(rows);
        return rows.ToList();
    }

    private async Task<List<int>> IdsAsync(string prefix)
    {
        await using PageNotFoundTrackerDbContext dbContext = this.CreateContext();
        return await dbContext.TrackedUrls.AsNoTracking().Where(r => r.Url.StartsWith(prefix)).OrderBy(r => r.Id).Select(r => r.Id).ToListAsync();
    }

    private async Task<List<string>> UrlsAsync(string prefix)
    {
        await using PageNotFoundTrackerDbContext dbContext = this.CreateContext();
        return await dbContext.TrackedUrls.AsNoTracking().Where(r => r.Url.StartsWith(prefix)).Select(r => r.Url).ToListAsync();
    }

    private static TrackedUrlHit Hit(string url) =>
        new(url, "hash" + url, url, url, string.Empty, string.Empty, false, 1, Seen, Seen);

    private async Task UpsertAsync(TrackedUrlHit hit)
    {
        await using PageNotFoundTrackerDbContext dbContext = this.CreateContext();
        await new DefaultTrackedUrlUpsert(NullLogger<DefaultTrackedUrlUpsert>.Instance).UpsertAsync(dbContext, [hit], CancellationToken.None);
    }

    private async Task<List<TrackedUrl>> GetRowsAsync(string url)
    {
        await using PageNotFoundTrackerDbContext dbContext = this.CreateContext();
        return await dbContext.TrackedUrls.AsNoTracking().Where(r => r.Url == url).ToListAsync();
    }

    private sealed class TestContextFactory(TrackedUrlRemoverTests owner) : IDbContextFactory<PageNotFoundTrackerDbContext>
    {
        public PageNotFoundTrackerDbContext CreateDbContext() => owner.CreateContext();
    }

    private sealed class CancelAfterFirstRunFlusher(ITrackingFlusher inner, CancellationTokenSource cts) : ITrackingFlusher
    {
        public Task FlushAsync(CancellationToken cancellationToken) => inner.FlushAsync(cancellationToken);

        public async Task RunExclusiveAsync(Func<Task> action, CancellationToken cancellationToken)
        {
            await inner.RunExclusiveAsync(action, cancellationToken);
            await cts.CancelAsync();
        }
    }

    private sealed class StaticOptionsMonitor<T>(T value) : IOptionsMonitor<T>
    {
        public T CurrentValue => value;

        public T Get(string? name) => value;

        public IDisposable? OnChange(Action<T, string?> listener) => null;
    }
}
