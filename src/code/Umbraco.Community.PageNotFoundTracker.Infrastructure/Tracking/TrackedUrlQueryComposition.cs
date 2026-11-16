// Copyright (c) Dave Woestenborghs and contributors. Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Umbraco.Community.PageNotFoundTracker.Core.Tracking;

namespace Umbraco.Community.PageNotFoundTracker.Infrastructure.Tracking;

/// <summary>
/// The filter and the order of the tracked URL list, shared by the paged query and the export so both select the same
/// rows in the same order.
/// </summary>
internal static class TrackedUrlQueryComposition
{
    /// <summary>
    /// Applies the search and the domain and culture filters.
    /// </summary>
    /// <param name="rows">The rows.</param>
    /// <param name="search">The search term, or null.</param>
    /// <param name="domain">The domain filter.</param>
    /// <param name="culture">The culture filter.</param>
    /// <returns>The filtered rows.</returns>
    internal static IQueryable<TrackedUrl> Filter(IQueryable<TrackedUrl> rows, string? search, TrackedUrlFilter domain, TrackedUrlFilter culture)
    {
        if (!string.IsNullOrWhiteSpace(search))
        {
            string pattern = LikePattern.Contains(search);
            rows = rows.Where(r => EF.Functions.Like(r.Url, pattern, "\\"));
        }

        switch (domain.Kind)
        {
            case TrackedUrlFilterKind.Empty:
                rows = rows.Where(r => r.Domain == string.Empty);
                break;
            case TrackedUrlFilterKind.Exactly:
                string domainValue = domain.Value ?? string.Empty;
                rows = rows.Where(r => r.Domain == domainValue);
                break;
        }

        switch (culture.Kind)
        {
            case TrackedUrlFilterKind.Empty:
                rows = rows.Where(r => r.Culture == string.Empty);
                break;
            case TrackedUrlFilterKind.Exactly:
                string cultureValue = culture.Value ?? string.Empty;
                rows = rows.Where(r => r.Culture == cultureValue);
                break;
        }

        return rows;
    }

    /// <summary>
    /// Orders the rows by a column; the final ascending id keeps the order total.
    /// </summary>
    /// <param name="rows">The rows.</param>
    /// <param name="column">The column.</param>
    /// <param name="descending">Whether to sort descending.</param>
    /// <returns>The ordered rows.</returns>
    internal static IOrderedQueryable<TrackedUrl> Order(IQueryable<TrackedUrl> rows, TrackedUrlSortColumn column, bool descending)
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
        Expression<Func<TrackedUrl, TKey>> key,
        bool descending) =>
        (descending ? rows.OrderByDescending(key) : rows.OrderBy(key)).ThenBy(r => r.Id);
}
