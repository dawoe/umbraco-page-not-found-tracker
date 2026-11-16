// Copyright (c) Dave Woestenborghs and contributors. Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.EntityFrameworkCore;
using Umbraco.Community.PageNotFoundTracker.Core.Tracking;

namespace Umbraco.Community.PageNotFoundTracker.Infrastructure.Tracking;

/// <summary>
/// The search, domain and culture filter shared by the tracked URL list and the delete by filter.
/// </summary>
internal static class TrackedUrlFilters
{
    /// <summary>
    /// Applies the search term and the domain and culture filters.
    /// </summary>
    /// <param name="rows">The rows to filter.</param>
    /// <param name="search">The search term; null or whitespace means no search.</param>
    /// <param name="domainFilter">The domain filter.</param>
    /// <param name="cultureFilter">The culture filter.</param>
    /// <returns>The filtered rows.</returns>
    internal static IQueryable<TrackedUrl> Apply(IQueryable<TrackedUrl> rows, string? search, TrackedUrlFilter domainFilter, TrackedUrlFilter cultureFilter)
    {
        if (!string.IsNullOrWhiteSpace(search))
        {
            string pattern = LikePattern.Contains(search);
            rows = rows.Where(r => EF.Functions.Like(r.Url, pattern, "\\"));
        }

        switch (domainFilter.Kind)
        {
            case TrackedUrlFilterKind.Empty:
                rows = rows.Where(r => r.Domain == string.Empty);
                break;
            case TrackedUrlFilterKind.Exactly:
                string domain = domainFilter.Value ?? string.Empty;
                rows = rows.Where(r => r.Domain == domain);
                break;
        }

        switch (cultureFilter.Kind)
        {
            case TrackedUrlFilterKind.Empty:
                rows = rows.Where(r => r.Culture == string.Empty);
                break;
            case TrackedUrlFilterKind.Exactly:
                string culture = cultureFilter.Value ?? string.Empty;
                rows = rows.Where(r => r.Culture == culture);
                break;
        }

        return rows;
    }
}
