// Copyright (c) Dave Woestenborghs and contributors. Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Umbraco.Community.PageNotFoundTracker.Core.Blocking;
using Umbraco.Community.PageNotFoundTracker.Core.Tracking;
using Umbraco.Community.PageNotFoundTracker.Infrastructure.Tracking;
using Umbraco.Community.PageNotFoundTracker.Integration.Tests.Tracking;

namespace Umbraco.Community.PageNotFoundTracker.Integration.Tests.Blocking;

/// <summary>
/// Tests the block rule table and its migration against a real database, once on SQLite and once on SQL Server.
/// </summary>
internal sealed class BlockRuleMigrationTests : TrackingDatabaseFixture
{
    private static readonly DateTime Created = new(2026, 11, 4, 10, 0, 0, DateTimeKind.Utc);

    /// <summary>
    /// Initializes a new instance of the <see cref="BlockRuleMigrationTests"/> class.
    /// </summary>
    /// <param name="database">The database to run on.</param>
    public BlockRuleMigrationTests(TrackingDatabase database)
        : base(database)
    {
    }

    /// <summary>
    /// Tests that migrating an empty database creates the block rule table.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task Migration_creates_the_block_rule_table()
    {
        string connectionString = await this.CreateEmptyDatabaseAsync();
        await using PageNotFoundTrackerDbContext dbContext = this.CreateContext(connectionString);

        await dbContext.Database.MigrateAsync();

        List<string> tables = await this.GetTableNamesAsync(dbContext);
        Assert.That(tables, Does.Contain(BlockingConstants.TableName));
    }

    /// <summary>
    /// Tests that the same pattern, domain and culture cannot be stored twice.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task Unique_index_rejects_the_same_pattern_domain_and_culture()
    {
        string pattern = "/unique-" + Guid.NewGuid().ToString("N");
        await using PageNotFoundTrackerDbContext dbContext = this.CreateContext();
        dbContext.BlockRules.Add(Rule(pattern, "example.com", "en-us"));
        await dbContext.SaveChangesAsync();

        await using PageNotFoundTrackerDbContext second = this.CreateContext();
        second.BlockRules.Add(Rule(pattern, "example.com", "en-us"));

        Assert.ThrowsAsync<DbUpdateException>(() => second.SaveChangesAsync());
    }

    /// <summary>
    /// Tests that the same pattern may exist for another domain.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task Same_pattern_with_another_domain_is_allowed()
    {
        string pattern = "/domains-" + Guid.NewGuid().ToString("N");
        await using PageNotFoundTrackerDbContext dbContext = this.CreateContext();
        dbContext.BlockRules.Add(Rule(pattern, "a.example.com", string.Empty));
        dbContext.BlockRules.Add(Rule(pattern, "b.example.com", string.Empty));
        dbContext.BlockRules.Add(Rule(pattern, string.Empty, string.Empty));

        await dbContext.SaveChangesAsync();

        int count = await dbContext.BlockRules.CountAsync(r => r.Pattern == pattern);
        Assert.That(count, Is.EqualTo(3));
    }

    /// <summary>
    /// Tests that the longest allowed values fit the columns and the index.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task Rule_with_maximum_length_values_is_stored()
    {
        string pattern = "/" + new string('a', BlockingConstants.MaxPatternLength - 1);
        await using PageNotFoundTrackerDbContext dbContext = this.CreateContext();
        dbContext.BlockRules.Add(Rule(pattern, new string('d', 255), new string('c', 50)));

        await dbContext.SaveChangesAsync();

        Assert.That(await dbContext.BlockRules.CountAsync(r => r.Pattern == pattern), Is.EqualTo(1));
    }

    /// <summary>
    /// Tests that tracked URL rows written before the block rule migration are intact after it.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task Existing_tracking_table_data_survives_the_migration()
    {
        string connectionString = await this.CreateEmptyDatabaseAsync();
        await using PageNotFoundTrackerDbContext dbContext = this.CreateContext(connectionString);
        string[] migrations = [.. dbContext.Database.GetMigrations()];
        Assert.That(migrations, Has.Length.GreaterThan(1));
        IMigrator migrator = dbContext.GetService<IMigrator>();
        await migrator.MigrateAsync(migrations[0]);
        string url = "/survives-" + Guid.NewGuid().ToString("N");
        dbContext.TrackedUrls.Add(new TrackedUrl
        {
            Url = url,
            UrlHash = "hash",
            RelativePath = url,
            DisplayUrl = url,
            HitCount = 7,
            FirstSeen = Created,
            LastSeen = Created,
        });
        await dbContext.SaveChangesAsync();
        dbContext.ChangeTracker.Clear();

        await migrator.MigrateAsync();

        TrackedUrl row = await dbContext.TrackedUrls.AsNoTracking().SingleAsync(t => t.Url == url);
        List<string> tables = await this.GetTableNamesAsync(dbContext);
        Assert.Multiple(() =>
        {
            Assert.That(row.HitCount, Is.EqualTo(7));
            Assert.That(row.UrlHash, Is.EqualTo("hash"));
            Assert.That(tables, Does.Contain(BlockingConstants.TableName));
        });
    }

    private static BlockRule Rule(string pattern, string domain, string culture) => new()
    {
        Pattern = pattern,
        PatternHash = TrackedUrlNormalizer.Hash(pattern),
        DisplayPattern = pattern,
        StatusCode = BlockingConstants.DefaultStatusCode,
        Domain = domain,
        Culture = culture,
        Created = Created,
    };
}
