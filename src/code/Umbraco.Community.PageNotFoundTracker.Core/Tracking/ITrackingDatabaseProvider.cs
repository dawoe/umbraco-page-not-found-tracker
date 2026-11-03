// Copyright (c) Dave Woestenborghs and contributors. Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.EntityFrameworkCore;

namespace Umbraco.Community.PageNotFoundTracker.Core.Tracking;

/// <summary>
/// A database provider for the tracking persistence, selected by the name of Umbraco's database provider.
/// </summary>
public interface ITrackingDatabaseProvider
{
    /// <summary>
    /// Gets the name of the Umbraco database provider this provider serves (for example <c>Microsoft.Data.Sqlite</c>).
    /// </summary>
    string ProviderName { get; }

    /// <summary>
    /// Gets the provider specific bulk upsert, or <c>null</c> to use the generic EF Core implementation.
    /// </summary>
    ITrackedUrlUpsert? Upsert => null;

    /// <summary>
    /// Configures the options builder: the database provider, the <c>MigrationsAssembly</c> (the assembly of the
    /// provider, which holds its migrations) and <c>MigrationsHistoryTable(TrackingConstants.MigrationsHistoryTable)</c>.
    /// The migrations must cover every table of the package.
    /// </summary>
    /// <param name="options">The options builder.</param>
    /// <param name="connectionString">The connection string of the Umbraco database.</param>
    void ConfigureDbContext(DbContextOptionsBuilder options, string connectionString);
}
