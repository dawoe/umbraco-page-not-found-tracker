// Copyright (c) Dave Woestenborghs and contributors. Licensed under the MIT License. See LICENSE in the project root for license information.

using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Notifications;

namespace Umbraco.Community.PageNotFoundTracker.Infrastructure.Blocking;

/// <summary>
/// Rebuilds the block snapshot when Umbraco's domains or languages change (on every server, through the cache
/// refresher notifications), so alias stripping and domain and culture scoping never go stale.
/// </summary>
internal sealed class BlockSnapshotInvalidationHandler :
    INotificationAsyncHandler<DomainCacheRefresherNotification>,
    INotificationAsyncHandler<LanguageCacheRefresherNotification>
{
    private readonly IBlockSnapshotProvider provider;

    /// <summary>
    /// Initializes a new instance of the <see cref="BlockSnapshotInvalidationHandler"/> class.
    /// </summary>
    /// <param name="provider">The snapshot provider.</param>
    public BlockSnapshotInvalidationHandler(IBlockSnapshotProvider provider)
    {
        this.provider = provider;
    }

    /// <inheritdoc />
    public Task HandleAsync(DomainCacheRefresherNotification notification, CancellationToken cancellationToken) =>
        this.provider.RefreshAsync(cancellationToken);

    /// <inheritdoc />
    public Task HandleAsync(LanguageCacheRefresherNotification notification, CancellationToken cancellationToken) =>
        this.provider.RefreshAsync(cancellationToken);
}
