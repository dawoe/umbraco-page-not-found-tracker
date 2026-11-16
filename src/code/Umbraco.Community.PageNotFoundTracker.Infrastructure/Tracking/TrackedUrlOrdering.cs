// Copyright (c) Dave Woestenborghs and contributors. Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Linq.Expressions;
using Umbraco.Community.PageNotFoundTracker.Core.Tracking;

namespace Umbraco.Community.PageNotFoundTracker.Infrastructure.Tracking;

/// <summary>
/// The order of the tracked URL list, shared by the paged query and the export so both return the same rows in the same
/// order (the filter is shared the same way, in <see cref="TrackedUrlFilters"/>).
/// </summary>
internal static class TrackedUrlOrdering
{
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
