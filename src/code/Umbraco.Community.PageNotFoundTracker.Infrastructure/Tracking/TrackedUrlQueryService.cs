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
        IQueryable<TrackedUrl> filtered = Filter(dbContext.TrackedUrls.AsNoTracking(), query);

        long total = await filtered.LongCountAsync(cancellationToken);
        List<TrackedUrl> items = await Order(filtered, query.SortColumn, query.Descending)
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

    private async Task<PageNotFoundTrackerDbContext> CreateContextAsync(CancellationToken cancellationToken) =>
        await serviceProvider.GetRequiredService<IDbContextFactory<PageNotFoundTrackerDbContext>>().CreateDbContextAsync(cancellationToken);

    private static IQueryable<TrackedUrl> Filter(IQueryable<TrackedUrl> rows, TrackedUrlQuery query)
    {
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            string pattern = LikePattern.Contains(query.Search);
            rows = rows.Where(r => EF.Functions.Like(r.Url, pattern, "\\"));
        }

        switch (query.Domain.Kind)
        {
            case TrackedUrlFilterKind.Empty:
                rows = rows.Where(r => r.Domain == string.Empty);
                break;
            case TrackedUrlFilterKind.Exactly:
                string domain = query.Domain.Value ?? string.Empty;
                rows = rows.Where(r => r.Domain == domain);
                break;
        }

        switch (query.Culture.Kind)
        {
            case TrackedUrlFilterKind.Empty:
                rows = rows.Where(r => r.Culture == string.Empty);
                break;
            case TrackedUrlFilterKind.Exactly:
                string culture = query.Culture.Value ?? string.Empty;
                rows = rows.Where(r => r.Culture == culture);
                break;
        }

        return rows;
    }

    private static IOrderedQueryable<TrackedUrl> Order(IQueryable<TrackedUrl> rows, TrackedUrlSortColumn column, bool descending)
    {
        // The final ascending Id keeps the order total, so paging neither repeats nor skips rows.
        return column switch
        {
            TrackedUrlSortColumn.Url => OrderBy(rows, r => r.Url, descending),
            TrackedUrlSortColumn.Domain => OrderBy(rows, r => r.Domain, descending),
            TrackedUrlSortColumn.Culture => OrderBy(rows, r => r.Culture, descending),
            TrackedUrlSortColumn.IsFile => OrderBy(rows, r => r.IsFile, descending),
            TrackedUrlSortColumn.FirstSeen => OrderBy(rows, r => r.FirstSeen, descending),
            TrackedUrlSortColumn.LastSeen => OrderBy(rows, r => r.LastSeen, descending),
            _ => OrderBy(rows, r => r.HitCount, descending),
        };
    }

    private static IOrderedQueryable<TrackedUrl> OrderBy<TKey>(
        IQueryable<TrackedUrl> rows,
        System.Linq.Expressions.Expression<Func<TrackedUrl, TKey>> key,
        bool descending) =>
        (descending ? rows.OrderByDescending(key) : rows.OrderBy(key)).ThenBy(r => r.Id);
}
