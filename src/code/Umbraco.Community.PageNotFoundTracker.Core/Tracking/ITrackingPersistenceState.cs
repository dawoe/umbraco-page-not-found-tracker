// Copyright (c) Dave Woestenborghs and contributors. Licensed under the MIT License. See LICENSE in the project root for license information.

namespace Umbraco.Community.PageNotFoundTracker.Core.Tracking;

/// <summary>
/// Exposes whether the tracking database is ready. Later features await it before touching the database.
/// </summary>
public interface ITrackingPersistenceState
{
    /// <summary>
    /// Gets the current status.
    /// </summary>
    TrackingPersistenceStatus Status { get; }

    /// <summary>
    /// Waits until the status is no longer <see cref="TrackingPersistenceStatus.Pending"/>.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns><c>true</c> when the persistence is available, <c>false</c> when it is unavailable.</returns>
    Task<bool> WaitUntilReadyAsync(CancellationToken cancellationToken);
}
