// Copyright (c) Dave Woestenborghs and contributors. Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.EntityFrameworkCore;
using Umbraco.Community.PageNotFoundTracker.Core.Tracking;

namespace Umbraco.Community.PageNotFoundTracker.Samples;

// docs:begin db-provider-class

/// <summary>
/// Sample tracking database provider. A real provider ships EF Core migrations for all package tables, generated
/// against <c>PageNotFoundTrackerDbContext</c> in its own assembly, as the built-in provider projects do.
/// </summary>
public sealed class DatabaseProviderSample : ITrackingDatabaseProvider
{
    /// <summary>
    /// Gets Umbraco's connection string provider name for your database.
    /// </summary>
    public string ProviderName => "My.Data.Provider";

    /// <inheritdoc />
    public void ConfigureDbContext(DbContextOptionsBuilder options, string connectionString) =>
        options.UseSqlite(connectionString, db => db // a real provider calls its own UseXxx here
            .MigrationsAssembly(typeof(DatabaseProviderSample).Assembly.GetName().Name)
            .MigrationsHistoryTable(TrackingConstants.MigrationsHistoryTable));
}

// docs:end db-provider-class
