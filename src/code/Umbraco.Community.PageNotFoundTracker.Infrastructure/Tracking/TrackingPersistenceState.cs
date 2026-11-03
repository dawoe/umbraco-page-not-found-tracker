// Copyright (c) Dave Woestenborghs and contributors. Licensed under the MIT License. See LICENSE in the project root for license information.

using Umbraco.Community.PageNotFoundTracker.Core.Tracking;

namespace Umbraco.Community.PageNotFoundTracker.Infrastructure.Tracking;

/// <summary>
/// Holds whether the tracking database is ready. Starts pending; the migration service sets the final status.
/// </summary>
internal sealed class TrackingPersistenceState : ITrackingPersistenceState
{
    private readonly TaskCompletionSource<bool> ready = new(TaskCreationOptions.RunContinuationsAsynchronously);

    private int status = (int)TrackingPersistenceStatus.Pending;

    /// <inheritdoc />
    public TrackingPersistenceStatus Status => (TrackingPersistenceStatus)Volatile.Read(ref this.status);

    /// <inheritdoc />
    public Task<bool> WaitUntilReadyAsync(CancellationToken cancellationToken) => this.ready.Task.WaitAsync(cancellationToken);

    /// <summary>
    /// Marks the persistence as available.
    /// </summary>
    internal void SetAvailable()
    {
        Volatile.Write(ref this.status, (int)TrackingPersistenceStatus.Available);
        this.ready.TrySetResult(true);
    }

    /// <summary>
    /// Marks the persistence as unavailable.
    /// </summary>
    internal void SetUnavailable()
    {
        Volatile.Write(ref this.status, (int)TrackingPersistenceStatus.Unavailable);
        this.ready.TrySetResult(false);
    }
}
