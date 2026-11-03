// Copyright (c) Dave Woestenborghs and contributors. Licensed under the MIT License. See LICENSE in the project root for license information.

namespace Umbraco.Community.PageNotFoundTracker.Integration.Tests.Tracking;

/// <summary>
/// The databases the tracking persistence tests run on.
/// </summary>
internal enum TrackingDatabase
{
    /// <summary>
    /// SQLite, a temporary file per database.
    /// </summary>
    Sqlite,

    /// <summary>
    /// SQL Server, from <see cref="SqlServerTestDatabase.ConnectionVariable"/>; ignored when it is not set.
    /// </summary>
    SqlServer,
}
