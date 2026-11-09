// Copyright (c) Dave Woestenborghs and contributors. Licensed under the MIT License. See LICENSE in the project root for license information.

namespace Umbraco.Community.PageNotFoundTracker.Infrastructure.Blocking;

/// <summary>
/// The waits used by the block snapshot provider and its startup service. Production uses <see cref="Default"/>;
/// unit tests pass shorter values.
/// </summary>
/// <param name="InitialLoadWait">How long a request waits for the initial load before passing through (5 s).</param>
/// <param name="RetryInterval">How long the startup service waits before retrying a failed initial load (30 s).</param>
/// <param name="RuntimePollInterval">How often the startup service checks whether Umbraco runs (1 s).</param>
internal sealed record BlockSnapshotTimings(TimeSpan InitialLoadWait, TimeSpan RetryInterval, TimeSpan RuntimePollInterval)
{
    /// <summary>
    /// Gets the production values: wait at most 5 s, retry every 30 s, check the runtime level every second.
    /// </summary>
    public static BlockSnapshotTimings Default { get; } =
        new(TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(1));
}
