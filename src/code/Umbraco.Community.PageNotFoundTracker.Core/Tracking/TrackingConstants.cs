// Copyright (c) Dave Woestenborghs and contributors. Licensed under the MIT License. See LICENSE in the project root for license information.

namespace Umbraco.Community.PageNotFoundTracker.Core.Tracking;

/// <summary>
/// Constants for the tracking persistence.
/// </summary>
public static class TrackingConstants
{
    /// <summary>
    /// The name of the table that holds the tracked URLs.
    /// </summary>
    public const string TableName = "pageNotFoundTrackerUrl";

    /// <summary>
    /// The name of the EF Core migrations history table of the package.
    /// </summary>
    public const string MigrationsHistoryTable = "__pageNotFoundTrackerMigrations";
}
