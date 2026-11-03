// Copyright (c) Dave Woestenborghs and contributors. Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Data.Common;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Umbraco.Community.PageNotFoundTracker.Core.Tracking;
using Umbraco.Community.PageNotFoundTracker.Infrastructure.Tests.TestSupport;
using Umbraco.Community.PageNotFoundTracker.Infrastructure.Tracking;

namespace Umbraco.Community.PageNotFoundTracker.Infrastructure.Tests;

/// <summary>
/// Unit tests for the <see cref="DefaultTrackedUrlUpsert"/> class against an in-memory SQLite database.
/// </summary>
[TestFixture]
internal sealed class DefaultTrackedUrlUpsertTests
{
    private static readonly DateTime T0 = new(2026, 11, 3, 10, 0, 0, DateTimeKind.Utc);

    private SqliteConnection connection = null!;
    private DbContextOptions<PageNotFoundTrackerDbContext> options = null!;

    /// <summary>
    /// Opens the in-memory database and creates the schema.
    /// </summary>
    [SetUp]
    public void SetUp()
    {
        this.connection = new SqliteConnection("Data Source=:memory:");
        this.connection.Open();
        this.options = new DbContextOptionsBuilder<PageNotFoundTrackerDbContext>().UseSqlite(this.connection).Options;
        using var context = new PageNotFoundTrackerDbContext(this.options);
        context.Database.EnsureCreated();
    }

    /// <summary>
    /// Closes the in-memory database.
    /// </summary>
    [TearDown]
    public void TearDown() => this.connection.Dispose();

    /// <summary>
    /// A new identity is inserted, a known one is incremented, and an older last seen time does not move it back.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task Inserts_Then_Increments_And_Keeps_Latest_LastSeen()
    {
        var upsert = new DefaultTrackedUrlUpsert(NullLogger<DefaultTrackedUrlUpsert>.Instance);

        await this.UpsertAsync(upsert, Hit("/a", count: 2, first: T0, last: T0.AddMinutes(5)), Hit("/a", "x.com", "en", 1, T0, T0));
        await this.UpsertAsync(upsert, Hit("/a", count: 3, first: T0.AddMinutes(6), last: T0.AddMinutes(10)));
        await this.UpsertAsync(upsert, Hit("/a", count: 1, first: T0.AddMinutes(1), last: T0.AddMinutes(1)));

        using var context = new PageNotFoundTrackerDbContext(this.options);
        var rows = await context.TrackedUrls.OrderBy(r => r.Id).ToListAsync();
        Assert.That(rows, Has.Count.EqualTo(2));
        Assert.Multiple(() =>
        {
            Assert.That(rows[0].HitCount, Is.EqualTo(6));
            Assert.That(rows[0].FirstSeen, Is.EqualTo(T0));
            Assert.That(rows[0].LastSeen, Is.EqualTo(T0.AddMinutes(10)));
            Assert.That(rows[0].Domain, Is.Empty);
            Assert.That(rows[1].Domain, Is.EqualTo("x.com"));
            Assert.That(rows[1].Culture, Is.EqualTo("en"));
            Assert.That(rows[1].HitCount, Is.EqualTo(1));
        });
    }

    /// <summary>
    /// A hit the database rejects is returned as not written, while the other hits of the batch are written.
    /// SQLite does not enforce column lengths, so the rejected hit breaks the required (NOT NULL) Url column instead.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task Returns_Failed_Hit_And_Writes_The_Others()
    {
        var upsert = new DefaultTrackedUrlUpsert(NullLogger<DefaultTrackedUrlUpsert>.Instance);
        TrackedUrlHit poison = Hit("/poison") with { Url = null! };

        var failed = await this.UpsertAsync(upsert, Hit("/a"), poison, Hit("/b"));

        Assert.That(failed, Is.EqualTo(new[] { poison }));
        using var context = new PageNotFoundTrackerDbContext(this.options);
        var urls = await context.TrackedUrls.OrderBy(r => r.Url).Select(r => r.Url).ToListAsync();
        Assert.That(urls, Is.EqualTo(new[] { "/a", "/b" }));
    }

    /// <summary>
    /// When the database fails every write, the upsert stops after <see cref="DefaultTrackedUrlUpsert.MaxConsecutiveFailures"/>
    /// attempts, logs the failures at debug only, and returns the failed and the unattempted hits with the first error.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task Failing_Database_Stops_Early_And_Returns_All_Unwritten_Hits()
    {
        var logger = new CapturingLogger<DefaultTrackedUrlUpsert>();
        var upsert = new DefaultTrackedUrlUpsert(logger);
        using (var context = new PageNotFoundTrackerDbContext(this.options))
        {
            await context.Database.ExecuteSqlRawAsync("DROP TABLE " + TrackingConstants.TableName);
        }

        TrackedUrlHit[] hits = [.. Enumerable.Range(0, 10).Select(i => Hit("/" + i))];

        var failed = await this.UpsertAsync(upsert, hits);

        Assert.Multiple(() =>
        {
            Assert.That(failed, Is.EqualTo(hits));
            Assert.That(logger.Debugs.Count(m => m.Contains("could not be written", StringComparison.Ordinal)), Is.EqualTo(DefaultTrackedUrlUpsert.MaxConsecutiveFailures));
            Assert.That(logger.Warnings, Is.Empty);
            Assert.That(failed, Is.TypeOf<FailedTrackedUrlHits>());
            Assert.That(((FailedTrackedUrlHits)failed).FirstException, Is.Not.Null);
        });
    }

    /// <summary>
    /// A failure that is followed by a success resets the consecutive failure count, so the batch is not stopped.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task Isolated_Failures_Do_Not_Stop_The_Batch()
    {
        var upsert = new DefaultTrackedUrlUpsert(NullLogger<DefaultTrackedUrlUpsert>.Instance);
        TrackedUrlHit Poison(string url) => Hit(url) with { Url = null! };
        TrackedUrlHit[] hits = [Poison("/p1"), Poison("/p2"), Hit("/a"), Poison("/p3"), Poison("/p4"), Hit("/b")];

        var failed = await this.UpsertAsync(upsert, hits);

        Assert.That(failed.Select(h => h.UrlHash), Is.EqualTo(new[] { "hash/p1", "hash/p2", "hash/p3", "hash/p4" }));
        using var context = new PageNotFoundTrackerDbContext(this.options);
        Assert.That(await context.TrackedUrls.OrderBy(r => r.Url).Select(r => r.Url).ToListAsync(), Is.EqualTo(new[] { "/a", "/b" }));
    }

    /// <summary>
    /// Cancelling in the middle of a batch does not throw: the written hits are not returned, the current and the
    /// unattempted hits are, so sending the returned hits again does not count anything twice.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task Cancelling_Mid_Batch_Returns_Only_Unwritten_Hits()
    {
        using var cts = new CancellationTokenSource();
        var interceptor = new CancelOnCommandInterceptor(cts, cancelOnCommand: 3);
        var interceptedOptions = new DbContextOptionsBuilder<PageNotFoundTrackerDbContext>()
            .UseSqlite(this.connection)
            .AddInterceptors(interceptor)
            .Options;
        var upsert = new DefaultTrackedUrlUpsert(NullLogger<DefaultTrackedUrlUpsert>.Instance);
        TrackedUrlHit[] hits = [Hit("/a"), Hit("/b"), Hit("/c")];

        IReadOnlyCollection<TrackedUrlHit> failed;
        using (var context = new PageNotFoundTrackerDbContext(interceptedOptions))
        {
            failed = await upsert.UpsertAsync(context, hits, cts.Token);
        }

        Assert.That(failed.Select(h => h.Url), Is.EqualTo(new[] { "/b", "/c" }));

        await this.UpsertAsync(upsert, [.. failed]);

        using var check = new PageNotFoundTrackerDbContext(this.options);
        var rows = await check.TrackedUrls.OrderBy(r => r.Url).ToListAsync();
        Assert.That(rows.Select(r => r.Url + ":" + r.HitCount), Is.EqualTo(new[] { "/a:1", "/b:1", "/c:1" }));
    }

    /// <summary>
    /// An already cancelled token writes nothing and returns the whole batch.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task Cancelled_Token_Returns_Whole_Batch()
    {
        var upsert = new DefaultTrackedUrlUpsert(NullLogger<DefaultTrackedUrlUpsert>.Instance);
        TrackedUrlHit[] hits = [Hit("/a"), Hit("/b")];

        IReadOnlyCollection<TrackedUrlHit> failed;
        using (var context = new PageNotFoundTrackerDbContext(this.options))
        {
            failed = await upsert.UpsertAsync(context, hits, new CancellationToken(true));
        }

        Assert.That(failed, Is.EqualTo(hits));
        using var check = new PageNotFoundTrackerDbContext(this.options);
        Assert.That(await check.TrackedUrls.CountAsync(), Is.Zero);
    }

    /// <summary>
    /// The hit count is a 64-bit value: incrementing past the 32-bit maximum keeps counting.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task HitCount_Counts_Past_Int32_Maximum()
    {
        var upsert = new DefaultTrackedUrlUpsert(NullLogger<DefaultTrackedUrlUpsert>.Instance);

        await this.UpsertAsync(upsert, Hit("/a", count: int.MaxValue));
        await this.UpsertAsync(upsert, Hit("/a", count: int.MaxValue));

        using var context = new PageNotFoundTrackerDbContext(this.options);
        Assert.That((await context.TrackedUrls.SingleAsync()).HitCount, Is.EqualTo(2L * int.MaxValue));
    }

    private static TrackedUrlHit Hit(string url, string domain = "", string culture = "", long count = 1, DateTime? first = null, DateTime? last = null) =>
        new(url, "hash" + url, url, url, domain, culture, false, count, first ?? T0, last ?? T0);

    private async Task<IReadOnlyCollection<TrackedUrlHit>> UpsertAsync(DefaultTrackedUrlUpsert upsert, params TrackedUrlHit[] hits)
    {
        using var context = new PageNotFoundTrackerDbContext(this.options);
        return await upsert.UpsertAsync(context, hits, CancellationToken.None);
    }

    /// <summary>
    /// Cancels the token when the given database command (1-based) is about to run.
    /// </summary>
    private sealed class CancelOnCommandInterceptor(CancellationTokenSource cts, int cancelOnCommand) : DbCommandInterceptor
    {
        private int commands;

        public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand command, CommandEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            this.Count();
            return base.NonQueryExecutingAsync(command, eventData, result, cancellationToken);
        }

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        {
            this.Count();
            return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
        }

        private void Count()
        {
            if (++this.commands == cancelOnCommand)
            {
                cts.Cancel();
            }
        }
    }
}
