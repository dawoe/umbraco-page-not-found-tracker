// Copyright (c) Dave Woestenborghs and contributors. Licensed under the MIT License. See LICENSE in the project root for license information.

using Umbraco.Cms.Core.Cache;
using Umbraco.Cms.Core.Notifications;
using Umbraco.Cms.Core.Sync;

namespace Umbraco.Community.PageNotFoundTracker.Infrastructure.Blocking;

/// <summary>
/// Published on every server after the block rule cache refresher ran.
/// </summary>
internal sealed class BlockRuleCacheRefresherNotification : CacheRefresherNotification
{
    /// <summary>
    /// Initializes a new instance of the <see cref="BlockRuleCacheRefresherNotification"/> class.
    /// </summary>
    /// <param name="messageObject">The refresher payloads.</param>
    /// <param name="messageType">The message type.</param>
    public BlockRuleCacheRefresherNotification(object messageObject, MessageType messageType)
        : base(messageObject, messageType)
    {
    }
}
