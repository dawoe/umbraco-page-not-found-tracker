// Copyright (c) Dave Woestenborghs and contributors. Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Umbraco.Community.PageNotFoundTracker.Core.Tracking;
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
        var upsert = new DefaultTrackedUrlUpsert();

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

    private static TrackedUrlHit Hit(string url, string domain = "", string culture = "", int count = 1, DateTime? first = null, DateTime? last = null) =>
        new(url, "hash" + url, url, url, domain, culture, false, count, first ?? T0, last ?? T0);

    private async Task UpsertAsync(DefaultTrackedUrlUpsert upsert, params TrackedUrlHit[] hits)
    {
        using var context = new PageNotFoundTrackerDbContext(this.options);
        await upsert.UpsertAsync(context, hits, CancellationToken.None);
    }
}
