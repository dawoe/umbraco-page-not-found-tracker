// Copyright (c) Dave Woestenborghs and contributors. Licensed under the MIT License. See LICENSE in the project root for license information.

namespace Umbraco.Community.PageNotFoundTracker.Infrastructure.Tracking;

/// <summary>
/// Writes the pending hits to the database on demand (the integration tests force a flush with it).
/// </summary>
internal interface ITrackingFlusher
{
    /// <summary>
    /// Writes the pending hits. Failures are logged, never thrown; failed hits stay pending.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that completes when the flush is done.</returns>
    Task FlushAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Runs an action while holding the flush lock, so no flush batch is in flight while it runs (a batch in progress
    /// finishes first, and no new one starts until the action is done).
    /// </summary>
    /// <param name="action">The action.</param>
    /// <param name="cancellationToken">The cancellation token for waiting on the lock.</param>
    /// <returns>A task that completes when the action is done.</returns>
    Task RunExclusiveAsync(Func<Task> action, CancellationToken cancellationToken);
}
