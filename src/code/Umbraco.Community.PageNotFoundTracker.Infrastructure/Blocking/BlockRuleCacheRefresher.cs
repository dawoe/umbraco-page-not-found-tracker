// Copyright (c) Dave Woestenborghs and contributors. Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.Logging;
using Umbraco.Cms.Core.Cache;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Serialization;
using Umbraco.Community.PageNotFoundTracker.Core.Blocking;
using Umbraco.Community.PageNotFoundTracker.Core.Tracking;
using Umbraco.Community.PageNotFoundTracker.Infrastructure.Tracking;

namespace Umbraco.Community.PageNotFoundTracker.Infrastructure.Blocking;

/// <summary>
/// Runs on every server when a block rule changes: rebuilds the server's rule snapshot and, for a saved rule, discards the
/// queued hits the rule covers. It never throws; a failure is logged and the old snapshot stays.
/// </summary>
internal sealed class BlockRuleCacheRefresher : PayloadCacheRefresherBase<BlockRuleCacheRefresherNotification, BlockRuleCachePayload>
{
    /// <summary>
    /// The fixed, never changing id of this cache refresher.
    /// </summary>
    public static readonly Guid UniqueId = new("6d1c8a40-3f2b-4e57-9b1d-5a7c0e2f9d31");

    private readonly IBlockSnapshotProvider snapshotProvider;
    private readonly ITrackingFlusher flusher;
    private readonly TrackingQueue queue;
    private readonly ILogger<BlockRuleCacheRefresher> logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="BlockRuleCacheRefresher"/> class.
    /// </summary>
    /// <param name="appCaches">The app caches.</param>
    /// <param name="serializer">The JSON serializer that carries payloads between servers.</param>
    /// <param name="eventAggregator">The event aggregator.</param>
    /// <param name="factory">The notification factory.</param>
    /// <param name="snapshotProvider">The block snapshot provider.</param>
    /// <param name="flusher">The flusher that owns the flush lock.</param>
    /// <param name="queue">The queue of hits.</param>
    /// <param name="logger">The logger.</param>
    public BlockRuleCacheRefresher(
        AppCaches appCaches,
        IJsonSerializer serializer,
        IEventAggregator eventAggregator,
        ICacheRefresherNotificationFactory factory,
        IBlockSnapshotProvider snapshotProvider,
        ITrackingFlusher flusher,
        TrackingQueue queue,
        ILogger<BlockRuleCacheRefresher> logger)
        : base(appCaches, serializer, eventAggregator, factory)
    {
        this.snapshotProvider = snapshotProvider;
        this.flusher = flusher;
        this.queue = queue;
        this.logger = logger;
    }

    /// <inheritdoc />
    public override Guid RefresherUniqueId => UniqueId;

    /// <inheritdoc />
    public override string Name => "Page Not Found Tracker Block Rules";

    /// <inheritdoc />
    public override void Refresh(BlockRuleCachePayload[] payloads)
    {
        try
        {
            this.RebuildAndDiscardAsync(payloads).GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            this.logger.LogWarning(ex, "Page Not Found Tracker: refreshing the block rules from the distributed cache failed.");
        }

        base.Refresh(payloads);
    }

    private async Task RebuildAndDiscardAsync(BlockRuleCachePayload[] payloads)
    {
        try
        {
            await this.snapshotProvider.RefreshAsync(CancellationToken.None);
        }
        catch (Exception ex)
        {
            this.logger.LogWarning(ex, "Page Not Found Tracker: rebuilding the block rule snapshot failed; the old snapshot stays.");
        }

        foreach (BlockRuleCachePayload payload in payloads.Where(p => p.Change == BlockRuleChange.Saved))
        {
            try
            {
                Func<TrackedUrlHit, bool> matches = BlockHitMatcher.Create(payload.Pattern, payload.Domain, payload.Culture);
                await this.flusher.RunExclusiveAsync(
                    () =>
                    {
                        this.queue.DiscardMatching(matches);
                        return Task.CompletedTask;
                    },
                    CancellationToken.None);
            }
            catch (Exception ex)
            {
                this.logger.LogWarning(ex, "Page Not Found Tracker: discarding queued hits for the block rule {Pattern} failed.", payload.Pattern);
            }
        }
    }
}
