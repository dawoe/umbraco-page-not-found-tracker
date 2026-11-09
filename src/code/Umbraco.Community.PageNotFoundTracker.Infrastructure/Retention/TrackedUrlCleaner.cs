// Copyright (c) Dave Woestenborghs and contributors. Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Umbraco.Community.PageNotFoundTracker.Infrastructure.Tracking;

namespace Umbraco.Community.PageNotFoundTracker.Infrastructure.Retention;

/// <summary>
/// Deletes old tracked URLs with EF Core, one short statement per batch, the same way on every database provider.
/// </summary>
/// <param name="serviceProvider">
/// The service provider; the context factory is resolved from it per call, never before the persistence gate opened.
/// </param>
internal sealed class TrackedUrlCleaner(IServiceProvider serviceProvider) : ITrackedUrlCleaner
{
    /// <inheritdoc />
    public async Task<int> DeleteBatchAsync(DateTime cutoff, int batchSize, CancellationToken cancellationToken)
    {
        IReadOnlyList<int> ids = await this.SelectBatchAsync(cutoff, batchSize, cancellationToken);
        return await this.DeleteAsync(ids, cutoff, cancellationToken);
    }

    /// <summary>
    /// Selects the ids of at most <paramref name="batchSize"/> rows last seen before <paramref name="cutoff"/>, by id.
    /// </summary>
    /// <param name="cutoff">The UTC cut-off.</param>
    /// <param name="batchSize">The maximum number of ids.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The ids.</returns>
    internal async Task<IReadOnlyList<int>> SelectBatchAsync(DateTime cutoff, int batchSize, CancellationToken cancellationToken)
    {
        await using PageNotFoundTrackerDbContext dbContext = await this.CreateContextAsync(cancellationToken);
        return await dbContext.TrackedUrls
            .Where(r => r.LastSeen < cutoff)
            .OrderBy(r => r.Id)
            .Select(r => r.Id)
            .Take(batchSize)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Deletes the rows with the given ids that are still last seen before <paramref name="cutoff"/>, so a row a flush
    /// updated after it was selected is kept.
    /// </summary>
    /// <param name="ids">The selected ids.</param>
    /// <param name="cutoff">The UTC cut-off.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The number of rows deleted.</returns>
    internal async Task<int> DeleteAsync(IReadOnlyCollection<int> ids, DateTime cutoff, CancellationToken cancellationToken)
    {
        if (ids.Count == 0)
        {
            return 0;
        }

        await using PageNotFoundTrackerDbContext dbContext = await this.CreateContextAsync(cancellationToken);
        return await dbContext.TrackedUrls
            .Where(r => ids.Contains(r.Id) && r.LastSeen < cutoff)
            .ExecuteDeleteAsync(cancellationToken);
    }

    private Task<PageNotFoundTrackerDbContext> CreateContextAsync(CancellationToken cancellationToken) =>
        serviceProvider.GetRequiredService<IDbContextFactory<PageNotFoundTrackerDbContext>>().CreateDbContextAsync(cancellationToken);
}
