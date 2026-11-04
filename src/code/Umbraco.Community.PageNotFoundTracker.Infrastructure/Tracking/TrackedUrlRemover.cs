// Copyright (c) Dave Woestenborghs and contributors. Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Umbraco.Community.PageNotFoundTracker.Core.Tracking;

namespace Umbraco.Community.PageNotFoundTracker.Infrastructure.Tracking;

/// <summary>
/// The default <see cref="ITrackedUrlRemover"/>. Discards the queued hits and deletes the row while holding the flush
/// lock, so a batch that is being written finishes first and nothing queued is written back afterwards.
/// </summary>
/// <remarks>
/// The context factory is resolved per call from the service provider, like in <see cref="TrackedUrlQueryService"/>.
/// </remarks>
/// <param name="queue">The queue of hits.</param>
/// <param name="flusher">The flusher that owns the flush lock.</param>
/// <param name="serviceProvider">The service provider the context factory is resolved from.</param>
internal sealed class TrackedUrlRemover(TrackingQueue queue, ITrackingFlusher flusher, IServiceProvider serviceProvider) : ITrackedUrlRemover
{
    /// <inheritdoc />
    public async Task<bool> DeleteAsync(TrackedUrl url, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(url);

        bool deleted = false;
        await flusher.RunExclusiveAsync(
            async () =>
            {
                queue.Discard(url.UrlHash, url.Domain, url.Culture);
                await using PageNotFoundTrackerDbContext dbContext =
                    await serviceProvider.GetRequiredService<IDbContextFactory<PageNotFoundTrackerDbContext>>().CreateDbContextAsync(cancellationToken);
                deleted = await dbContext.TrackedUrls.Where(r => r.Id == url.Id).ExecuteDeleteAsync(cancellationToken) > 0;
            },
            cancellationToken);
        return deleted;
    }
}
