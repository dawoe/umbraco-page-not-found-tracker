// Copyright (c) Dave Woestenborghs and contributors. Licensed under the MIT License. See LICENSE in the project root for license information.

using Umbraco.Community.PageNotFoundTracker.Core.Blocking;

namespace Umbraco.Community.PageNotFoundTracker.Infrastructure.Blocking;

/// <summary>
/// Tells the other servers that a block rule changed, so they rebuild their rule sets and discard their own matching
/// queued hits. The server that made the change has already done both itself.
/// </summary>
internal interface IBlockRuleChangeNotifier
{
    /// <summary>
    /// Sends a rule change.
    /// </summary>
    /// <param name="payload">The change.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task.</returns>
    Task NotifyAsync(BlockRuleCachePayload payload, CancellationToken cancellationToken);
}
