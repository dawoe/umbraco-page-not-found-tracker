// Copyright (c) Dave Woestenborghs and contributors. Licensed under the MIT License. See LICENSE in the project root for license information.

namespace Umbraco.Community.PageNotFoundTracker.Core.Tracking;

/// <summary>
/// A request for every tracked URL that matches a search and filters, in a given order. It has no paging.
/// </summary>
/// <param name="SortColumn">The column to sort on.</param>
/// <param name="Descending">Whether to sort descending.</param>
/// <param name="Search">A case-insensitive "contains" term for the normalised URL, or null, empty or whitespace-only for none (a non-blank term is not trimmed).</param>
/// <param name="Domain">The domain filter.</param>
/// <param name="Culture">The culture filter.</param>
public sealed record TrackedUrlExportQuery(
    TrackedUrlSortColumn SortColumn,
    bool Descending,
    string? Search,
    TrackedUrlFilter Domain,
    TrackedUrlFilter Culture);
