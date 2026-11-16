// Copyright (c) Dave Woestenborghs and contributors. Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Umbraco.Community.PageNotFoundTracker.Core.Tracking;

namespace Umbraco.Community.PageNotFoundTracker.Infrastructure.Tracking;

/// <summary>
/// The default <see cref="ITrackedUrlQueryService"/>: reads the tracked URLs with EF Core. Filtering, ordering, paging
/// and counting are one database query each; nothing is evaluated on the client.
/// </summary>
/// <remarks>
/// The context factory is resolved lazily, not injected: the pooled factory runs its options action on first resolve,
/// which must happen after the persistence gate opened, so a singleton resolved early must not resolve it.
/// </remarks>
/// <param name="serviceProvider">The service provider; the context factory is resolved from it at call time only.</param>
internal sealed class TrackedUrlQueryService(IServiceProvider serviceProvider) : ITrackedUrlQueryService
{
    /// <summary>
    /// The largest page size the service returns.
    /// </summary>
    internal const int MaxTake = 100;

    /// <inheritdoc />
    public async Task<TrackedUrlPage> GetPageAsync(TrackedUrlQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        int skip = Math.Max(query.Skip, 0);
        int take = Math.Clamp(query.Take, 1, MaxTake);

        await using PageNotFoundTrackerDbContext dbContext = await this.CreateContextAsync(cancellationToken);
        IQueryable<TrackedUrl> filtered = TrackedUrlQueryComposition.Filter(dbContext.TrackedUrls.AsNoTracking(), query.Search, query.Domain, query.Culture);

        long total = await filtered.LongCountAsync(cancellationToken);
        List<TrackedUrl> items = await TrackedUrlQueryComposition.Order(filtered, query.SortColumn, query.Descending)
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken);

        return new TrackedUrlPage(items, total);
    }

    /// <inheritdoc />
    public async Task<TrackedUrlFilterOptions> GetFilterOptionsAsync(CancellationToken cancellationToken)
    {
        await using PageNotFoundTrackerDbContext dbContext = await this.CreateContextAsync(cancellationToken);
        IQueryable<TrackedUrl> rows = dbContext.TrackedUrls.AsNoTracking();

        List<string> domains = await rows.Where(r => r.Domain != string.Empty).Select(r => r.Domain).Distinct().ToListAsync(cancellationToken);
        List<string> cultures = await rows.Where(r => r.Culture != string.Empty).Select(r => r.Culture).Distinct().ToListAsync(cancellationToken);
        bool hasEmptyDomain = await rows.AnyAsync(r => r.Domain == string.Empty, cancellationToken);
        bool hasEmptyCulture = await rows.AnyAsync(r => r.Culture == string.Empty, cancellationToken);

        // Sorted in memory (ordinal), so the order does not depend on the database collation.
        domains.Sort(StringComparer.Ordinal);
        cultures.Sort(StringComparer.Ordinal);
        return new TrackedUrlFilterOptions(domains, cultures, hasEmptyDomain, hasEmptyCulture);
    }

    /// <inheritdoc />
    public async Task<TrackedUrl?> GetByIdAsync(int id, CancellationToken cancellationToken)
    {
        await using PageNotFoundTrackerDbContext dbContext = await this.CreateContextAsync(cancellationToken);
        return await dbContext.TrackedUrls.AsNoTracking().FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
    }

    private async Task<PageNotFoundTrackerDbContext> CreateContextAsync(CancellationToken cancellationToken) =>
        await serviceProvider.GetRequiredService<IDbContextFactory<PageNotFoundTrackerDbContext>>().CreateDbContextAsync(cancellationToken);
}
