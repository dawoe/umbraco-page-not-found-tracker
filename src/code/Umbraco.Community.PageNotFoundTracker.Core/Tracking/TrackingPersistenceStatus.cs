// Copyright (c) Dave Woestenborghs and contributors. Licensed under the MIT License. See LICENSE in the project root for license information.

namespace Umbraco.Community.PageNotFoundTracker.Core.Tracking;

/// <summary>
/// The state of the tracking persistence.
/// </summary>
public enum TrackingPersistenceStatus
{
    /// <summary>
    /// The database is not ready yet (startup is still in progress).
    /// </summary>
    Pending,

    /// <summary>
    /// The database is ready and tracking can persist hits.
    /// </summary>
    Available,

    /// <summary>
    /// Tracking persistence is not available (disabled, unknown provider or a failed migration).
    /// </summary>
    Unavailable,
}
