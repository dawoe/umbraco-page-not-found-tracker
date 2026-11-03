// Copyright (c) Dave Woestenborghs and contributors. Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Umbraco.Community.PageNotFoundTracker.Infrastructure.Tracking;

namespace Umbraco.Community.PageNotFoundTracker.Persistence.Sqlite;

/// <summary>
/// Creates the context for <c>dotnet ef</c> with the SQLite provider, so the tools need no running site.
/// </summary>
internal sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<PageNotFoundTrackerDbContext>
{
    /// <inheritdoc />
    public PageNotFoundTrackerDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<PageNotFoundTrackerDbContext>();
        new SqliteTrackingDatabaseProvider().ConfigureDbContext(options, "Data Source=design-time.sqlite.db");
        return new PageNotFoundTrackerDbContext(options.Options);
    }
}
