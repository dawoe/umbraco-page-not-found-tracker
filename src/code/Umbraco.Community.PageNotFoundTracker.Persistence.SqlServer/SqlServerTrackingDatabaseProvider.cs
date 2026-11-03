// Copyright (c) Dave Woestenborghs and contributors. Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.EntityFrameworkCore;
using Umbraco.Community.PageNotFoundTracker.Core.Tracking;

namespace Umbraco.Community.PageNotFoundTracker.Persistence.SqlServer;

/// <summary>
/// The tracking database provider for SQL Server. The migrations history table lives in the <c>dbo</c> schema.
/// </summary>
public sealed class SqlServerTrackingDatabaseProvider : ITrackingDatabaseProvider
{
    /// <inheritdoc />
    public string ProviderName => "Microsoft.Data.SqlClient";

    /// <inheritdoc />
    public void ConfigureDbContext(DbContextOptionsBuilder options, string connectionString)
    {
        ArgumentNullException.ThrowIfNull(options);

        options.UseSqlServer(connectionString, sqlServer => sqlServer
            .MigrationsAssembly(typeof(SqlServerTrackingDatabaseProvider).Assembly.GetName().Name)
            .MigrationsHistoryTable(TrackingConstants.MigrationsHistoryTable, "dbo"));
    }
}
