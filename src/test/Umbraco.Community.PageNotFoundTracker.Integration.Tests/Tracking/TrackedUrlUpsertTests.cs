// Copyright (c) Dave Woestenborghs and contributors. Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Umbraco.Community.PageNotFoundTracker.Core.Tracking;
using Umbraco.Community.PageNotFoundTracker.Infrastructure.Tracking;

namespace Umbraco.Community.PageNotFoundTracker.Integration.Tests.Tracking;

/// <summary>
/// Tests the migrations and <see cref="DefaultTrackedUrlUpsert"/> against a real database, once on SQLite and once on
/// SQL Server. Every test uses its own URLs, so the tests share the fixture's database without seeing each other's rows.
/// </summary>
internal sealed class TrackedUrlUpsertTests : TrackingDatabaseFixture
{
    private static readonly DateTime FirstSeen = new(2026, 11, 3, 10, 0, 0, DateTimeKind.Utc);

    private readonly DefaultTrackedUrlUpsert upsert = new(NullLogger<DefaultTrackedUrlUpsert>.Instance);

    /// <summary>
    /// Initializes a new instance of the <see cref="TrackedUrlUpsertTests"/> class.
    /// </summary>
    /// <param name="database">The database to run on.</param>
    public TrackedUrlUpsertTests(TrackingDatabase database)
        : base(database)
    {
    }

    /// <summary>
    /// Tests that migrating an empty database creates the tracking table and the package's migrations history table.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task Migrate_On_Empty_Database_Creates_Tracking_And_History_Tables()
    {
        string connectionString = await this.CreateEmptyDatabaseAsync();
        await using PageNotFoundTrackerDbContext dbContext = this.CreateContext(connectionString);
        List<string> before = await this.GetTableNamesAsync(dbContext);

        await dbContext.Database.MigrateAsync();

        List<string> after = await this.GetTableNamesAsync(dbContext);
        Assert.Multiple(() =>
        {
            Assert.That(before, Does.Not.Contain(TrackingConstants.TableName));
            Assert.That(after, Does.Contain(TrackingConstants.TableName));
            Assert.That(after, Does.Contain(TrackingConstants.MigrationsHistoryTable));
        });
    }

    /// <summary>
    /// Tests that a new identity is inserted as one row with the hit's count.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task Upsert_New_Hit_Inserts_One_Row()
    {
        TrackedUrlHit hit = Hit("/upsert-new", count: 3, lastSeen: FirstSeen);

        await this.UpsertAsync(hit);

        List<TrackedUrl> rows = await this.GetRowsAsync(hit.Url);
        Assert.That(rows, Has.Count.EqualTo(1));
        Assert.Multiple(() =>
        {
            Assert.That(rows[0].HitCount, Is.EqualTo(3));
            Assert.That(rows[0].FirstSeen, Is.EqualTo(FirstSeen));
            Assert.That(rows[0].LastSeen, Is.EqualTo(FirstSeen));
            Assert.That(rows[0].DisplayUrl, Is.EqualTo(hit.DisplayUrl));
        });
    }

    /// <summary>
    /// Tests that a second hit adds its count and moves LastSeen, leaving FirstSeen and DisplayUrl as they were.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task Upsert_Existing_Hit_Adds_Count_And_Moves_LastSeen()
    {
        TrackedUrlHit first = Hit("/upsert-existing", count: 3, lastSeen: FirstSeen);
        DateTime later = FirstSeen.AddMinutes(5);
        TrackedUrlHit second = first with
        {
            DisplayUrl = "/Upsert-Existing/",
            Count = 2,
            FirstSeen = later,
            LastSeen = later,
        };

        await this.UpsertAsync(first);
        await this.UpsertAsync(second);

        List<TrackedUrl> rows = await this.GetRowsAsync(first.Url);
        Assert.That(rows, Has.Count.EqualTo(1));
        Assert.Multiple(() =>
        {
            Assert.That(rows[0].HitCount, Is.EqualTo(5));
            Assert.That(rows[0].LastSeen, Is.EqualTo(later));
            Assert.That(rows[0].FirstSeen, Is.EqualTo(FirstSeen));
            Assert.That(rows[0].DisplayUrl, Is.EqualTo(first.DisplayUrl));
        });
    }

    /// <summary>
    /// Tests that a hit with an older LastSeen (written late, for example after a retry) does not move LastSeen back.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task Upsert_Older_Hit_Does_Not_Move_LastSeen_Back()
    {
        TrackedUrlHit first = Hit("/upsert-older", count: 1, lastSeen: FirstSeen);
        DateTime earlier = FirstSeen.AddMinutes(-5);
        TrackedUrlHit older = first with { FirstSeen = earlier, LastSeen = earlier };

        await this.UpsertAsync(first);
        await this.UpsertAsync(older);

        List<TrackedUrl> rows = await this.GetRowsAsync(first.Url);
        Assert.That(rows, Has.Count.EqualTo(1));
        Assert.Multiple(() =>
        {
            Assert.That(rows[0].HitCount, Is.EqualTo(2));
            Assert.That(rows[0].LastSeen, Is.EqualTo(FirstSeen));
        });
    }

    /// <summary>
    /// Tests that the same URL on another domain is another identity, so another row.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task Upsert_Same_Url_Other_Domain_Gives_Two_Rows()
    {
        TrackedUrlHit siteA = Hit("/upsert-domain", count: 1, lastSeen: FirstSeen) with { Domain = "www.sitea.local" };
        TrackedUrlHit siteB = siteA with { Domain = "www.siteb.local" };

        await this.UpsertAsync(siteA);
        await this.UpsertAsync(siteB);

        List<TrackedUrl> rows = await this.GetRowsAsync(siteA.Url);
        Assert.That(rows.Select(r => r.Domain), Is.EquivalentTo(new[] { "www.sitea.local", "www.siteb.local" }));
        Assert.That(rows.Select(r => r.HitCount), Is.All.EqualTo(1));
    }

    /// <summary>
    /// Tests that concurrent upserts of the same new identity, each through its own context (as several servers would),
    /// end in one row that counts every hit.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task Concurrent_Upserts_Of_Same_New_Identity_Give_One_Row_With_All_Hits()
    {
        const int Tasks = 8;
        const int UpsertsPerTask = 10;
        TrackedUrlHit hit = Hit("/upsert-concurrent", count: 1, lastSeen: FirstSeen);

        IReadOnlyCollection<TrackedUrlHit>[][] results = await Task.WhenAll(Enumerable.Range(0, Tasks).Select(_ => Task.Run(async () =>
        {
            await using PageNotFoundTrackerDbContext dbContext = this.CreateContext();
            var failed = new IReadOnlyCollection<TrackedUrlHit>[UpsertsPerTask];
            for (int i = 0; i < UpsertsPerTask; i++)
            {
                failed[i] = await this.upsert.UpsertAsync(dbContext, [hit], CancellationToken.None);
            }

            return failed;
        })));

        List<TrackedUrl> rows = await this.GetRowsAsync(hit.Url);
        Assert.Multiple(() =>
        {
            Assert.That(results.SelectMany(r => r).SelectMany(f => f), Is.Empty, "Every upsert must be written.");
            Assert.That(rows, Has.Count.EqualTo(1));
            Assert.That(rows.Single().HitCount, Is.EqualTo(Tasks * UpsertsPerTask));
        });
    }

    private static TrackedUrlHit Hit(string url, int count, DateTime lastSeen) =>
        new(
            url,
            Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(url))),
            url,
            url.ToUpperInvariant(),
            string.Empty,
            string.Empty,
            false,
            count,
            lastSeen,
            lastSeen);

    private async Task UpsertAsync(TrackedUrlHit hit)
    {
        await using PageNotFoundTrackerDbContext dbContext = this.CreateContext();
        IReadOnlyCollection<TrackedUrlHit> failed = await this.upsert.UpsertAsync(dbContext, [hit], CancellationToken.None);
        Assert.That(failed, Is.Empty, $"Upserting {hit.Url} failed.");
    }

    private async Task<List<TrackedUrl>> GetRowsAsync(string url)
    {
        await using PageNotFoundTrackerDbContext dbContext = this.CreateContext();
        return await dbContext.TrackedUrls.AsNoTracking().Where(r => r.Url == url).OrderBy(r => r.Domain).ToListAsync();
    }
}
