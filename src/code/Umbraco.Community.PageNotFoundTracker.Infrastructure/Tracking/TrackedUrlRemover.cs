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
    /// <summary>
    /// The number of rows selected and deleted per batch (SQL Server allows 2100 parameters per command).
    /// </summary>
    internal const int BatchSize = 1000;

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

    /// <inheritdoc />
    public async Task<int> DeleteAsync(IReadOnlyCollection<int> ids, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(ids);

        int total = 0;
        foreach (int[] chunk in ids.Where(id => id > 0).Distinct().Chunk(BatchSize))
        {
            cancellationToken.ThrowIfCancellationRequested();
            total += await this.DeleteBatchAsync(rows => rows.Where(r => chunk.Contains(r.Id)), cancellationToken);
        }

        return total;
    }

    /// <inheritdoc />
    public async Task<int> DeleteMatchingAsync(TrackedUrlCriteria criteria, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(criteria);

        int total = 0;
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            (int selected, int deleted) = await this.DeleteBatchWithCountAsync(
                rows => TrackedUrlFilters.Apply(rows, criteria.Search, criteria.Domain, criteria.Culture),
                cancellationToken);
            total += deleted;

            // Stop on what was selected, not on what was deleted: a short batch is the last one.
            if (selected < BatchSize)
            {
                return total;
            }
        }
    }

    private async Task<int> DeleteBatchAsync(Func<IQueryable<TrackedUrl>, IQueryable<TrackedUrl>> select, CancellationToken cancellationToken) =>
        (await this.DeleteBatchWithCountAsync(select, cancellationToken)).Deleted;

    private async Task<(int Selected, int Deleted)> DeleteBatchWithCountAsync(
        Func<IQueryable<TrackedUrl>, IQueryable<TrackedUrl>> select,
        CancellationToken cancellationToken)
    {
        int selectedCount = 0;
        int deletedCount = 0;

        // Select inside the lock, so a flush cannot slip in between the select and the delete.
        await flusher.RunExclusiveAsync(
            async () =>
            {
                await using PageNotFoundTrackerDbContext dbContext =
                    await serviceProvider.GetRequiredService<IDbContextFactory<PageNotFoundTrackerDbContext>>().CreateDbContextAsync(cancellationToken);

                var batch = await select(dbContext.TrackedUrls.AsNoTracking())
                    .OrderBy(r => r.Id)
                    .Take(BatchSize)
                    .Select(r => new { r.Id, r.UrlHash, r.Domain, r.Culture })
                    .ToListAsync(cancellationToken);
                selectedCount = batch.Count;
                if (batch.Count == 0)
                {
                    return;
                }

                foreach (var row in batch)
                {
                    queue.Discard(row.UrlHash, row.Domain, row.Culture);
                }

                int[] batchIds = batch.Select(r => r.Id).ToArray();
                deletedCount = await dbContext.TrackedUrls.Where(r => batchIds.Contains(r.Id)).ExecuteDeleteAsync(cancellationToken);
            },
            cancellationToken);

        return (selectedCount, deletedCount);
    }
}
