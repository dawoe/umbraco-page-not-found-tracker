// Copyright (c) Dave Woestenborghs and contributors. Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.EntityFrameworkCore;
using Umbraco.Community.PageNotFoundTracker.Infrastructure.Tracking;
using Umbraco.Community.PageNotFoundTracker.Integration.Tests.Tracking;

namespace Umbraco.Community.PageNotFoundTracker.Integration.Tests.Retention;

/// <summary>
/// Tests the index on the last seen column, which the clean-up deletes by, on SQLite and SQL Server.
/// </summary>
internal sealed class LastSeenIndexMigrationTests : TrackingDatabaseFixture
{
    private const string IndexName = "IX_pageNotFoundTrackerUrl_LastSeen";

    /// <summary>
    /// Initializes a new instance of the <see cref="LastSeenIndexMigrationTests"/> class.
    /// </summary>
    /// <param name="database">The database to run on.</param>
    public LastSeenIndexMigrationTests(TrackingDatabase database)
        : base(database)
    {
    }

    /// <summary>
    /// Tests that migrating creates the index on the last seen column.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task Migration_creates_the_last_seen_index()
    {
        await using PageNotFoundTrackerDbContext dbContext = this.CreateContext();

        string sql = this.Database == TrackingDatabase.Sqlite
            ? $"SELECT COUNT(*) AS Value FROM sqlite_master WHERE type = 'index' AND name = '{IndexName}'"
            : $"SELECT COUNT(*) AS Value FROM sys.indexes WHERE name = '{IndexName}'";
        int count = await dbContext.Database.SqlQueryRaw<int>(sql).SingleAsync();

        Assert.That(count, Is.EqualTo(1));
    }
}
