// Copyright (c) Dave Woestenborghs and contributors. Licensed under the MIT License. See LICENSE in the project root for license information.

using Umbraco.Cms.Core.Cache;
using Umbraco.Community.PageNotFoundTracker.Core.Blocking;
using Umbraco.Extensions;

namespace Umbraco.Community.PageNotFoundTracker.Infrastructure.Blocking;

/// <summary>
/// Tells every server about a rule change through Umbraco's distributed cache. The originating server runs the refresher
/// too, which is harmless: it already rebuilt and discarded locally.
/// </summary>
internal sealed class DistributedBlockRuleChangeNotifier : IBlockRuleChangeNotifier
{
    private readonly DistributedCache distributedCache;

    /// <summary>
    /// Initializes a new instance of the <see cref="DistributedBlockRuleChangeNotifier"/> class.
    /// </summary>
    /// <param name="distributedCache">Umbraco's distributed cache.</param>
    public DistributedBlockRuleChangeNotifier(DistributedCache distributedCache)
    {
        this.distributedCache = distributedCache;
    }

    /// <inheritdoc />
    public Task NotifyAsync(BlockRuleCachePayload payload, CancellationToken cancellationToken)
    {
        this.distributedCache.RefreshByPayload(BlockRuleCacheRefresher.UniqueId, [payload]);
        return Task.CompletedTask;
    }
}
