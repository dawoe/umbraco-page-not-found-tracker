// Copyright (c) Dave Woestenborghs and contributors. Licensed under the MIT License. See LICENSE in the project root for license information.

using Umbraco.Community.PageNotFoundTracker.Core.Blocking;

namespace Umbraco.Community.PageNotFoundTracker.Infrastructure.Blocking;

/// <summary>
/// An <see cref="IBlockRuleChangeNotifier"/> that sends nothing (single server).
/// </summary>
internal sealed class NoOpBlockRuleChangeNotifier : IBlockRuleChangeNotifier
{
    /// <inheritdoc />
    public Task NotifyAsync(BlockRuleCachePayload payload, CancellationToken cancellationToken) => Task.CompletedTask;
}
