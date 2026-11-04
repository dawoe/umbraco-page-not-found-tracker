// Copyright (c) Dave Woestenborghs and contributors. Licensed under the MIT License. See LICENSE in the project root for license information.

namespace Umbraco.Community.PageNotFoundTracker.Infrastructure.Blocking;

/// <summary>
/// Holds the current <see cref="BlockSnapshot"/> and rebuilds it.
/// </summary>
internal interface IBlockSnapshotProvider
{
    /// <summary>
    /// Gets the current snapshot. Returns at once once the first load succeeded. Before that, a request waits for the
    /// shared initial load (at most a few seconds) and gets <see cref="BlockSnapshot.Empty"/> when it takes longer or
    /// failed, when the tracking database is unavailable, or when Umbraco does not run yet.
    /// </summary>
    /// <param name="cancellationToken">The caller's cancellation token; the only reason this throws.</param>
    /// <returns>The snapshot.</returns>
    ValueTask<BlockSnapshot> GetAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Builds a complete new snapshot and swaps it in. On failure the old snapshot stays and the failure is logged; it
    /// never throws except on the caller's cancellation.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns><c>true</c> when a new snapshot was swapped in.</returns>
    Task<bool> RefreshAsync(CancellationToken cancellationToken);
}
