// Copyright (c) Dave Woestenborghs and contributors. Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.EntityFrameworkCore;
using Umbraco.Community.PageNotFoundTracker.Core.Tracking;

namespace Umbraco.Community.PageNotFoundTracker.Persistence.Sqlite;

/// <summary>
/// The tracking database provider for SQLite. Umbraco already opens SQLite databases in WAL mode, so the provider
/// does not set the journal mode itself.
/// </summary>
public sealed class SqliteTrackingDatabaseProvider : ITrackingDatabaseProvider
{
    /// <inheritdoc />
    public string ProviderName => "Microsoft.Data.Sqlite";

    /// <inheritdoc />
    public void ConfigureDbContext(DbContextOptionsBuilder options, string connectionString)
    {
        ArgumentNullException.ThrowIfNull(options);

        options.UseSqlite(connectionString, sqlite => sqlite
            .MigrationsAssembly(typeof(SqliteTrackingDatabaseProvider).Assembly.GetName().Name)
            .MigrationsHistoryTable(TrackingConstants.MigrationsHistoryTable));
    }
}
