// Copyright (c) Dave Woestenborghs and contributors. Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Services;
using Umbraco.Community.PageNotFoundTracker.Core.Tracking;

namespace Umbraco.Community.PageNotFoundTracker.Infrastructure.Blocking;

/// <summary>
/// Fills the first block snapshot at startup: waits until Umbraco runs and the tracking database is ready (migrated),
/// then loads once and retries a failed load in the background until it succeeds. With an unavailable database it logs
/// a warning and leaves the empty snapshot in place, so blocking passes every request through.
/// </summary>
internal sealed class BlockSnapshotService : BackgroundService
{
    private readonly IRuntimeState runtimeState;
    private readonly ITrackingPersistenceState persistenceState;
    private readonly IBlockSnapshotProvider provider;
    private readonly TimeProvider timeProvider;
    private readonly ILogger<BlockSnapshotService> logger;
    private readonly BlockSnapshotTimings timings;

    /// <summary>
    /// Initializes a new instance of the <see cref="BlockSnapshotService"/> class.
    /// </summary>
    /// <param name="runtimeState">Umbraco's runtime state.</param>
    /// <param name="persistenceState">The tracking persistence state.</param>
    /// <param name="provider">The snapshot provider.</param>
    /// <param name="timeProvider">The time provider.</param>
    /// <param name="logger">The logger.</param>
    /// <param name="timings">The waits; <c>null</c> uses <see cref="BlockSnapshotTimings.Default"/>.</param>
    public BlockSnapshotService(
        IRuntimeState runtimeState,
        ITrackingPersistenceState persistenceState,
        IBlockSnapshotProvider provider,
        TimeProvider timeProvider,
        ILogger<BlockSnapshotService> logger,
        BlockSnapshotTimings? timings = null)
    {
        this.runtimeState = runtimeState;
        this.persistenceState = persistenceState;
        this.provider = provider;
        this.timeProvider = timeProvider;
        this.logger = logger;
        this.timings = timings ?? BlockSnapshotTimings.Default;
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            if (this.runtimeState.Level != RuntimeLevel.Run)
            {
                using var timer = new PeriodicTimer(this.timings.RuntimePollInterval, this.timeProvider);
                while (this.runtimeState.Level != RuntimeLevel.Run)
                {
                    if (!await timer.WaitForNextTickAsync(stoppingToken))
                    {
                        return;
                    }
                }
            }

            if (!await this.persistenceState.WaitUntilReadyAsync(stoppingToken))
            {
                this.logger.LogWarning(
                    "Page Not Found Tracker: the tracking database is unavailable, so no block rules are loaded. Blocking passes every request through.");
                return;
            }

            while (!await this.provider.RefreshAsync(stoppingToken))
            {
                await Task.Delay(this.timings.RetryInterval, this.timeProvider, stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Stopping before the first load succeeded: requests keep passing through.
        }
    }
}
