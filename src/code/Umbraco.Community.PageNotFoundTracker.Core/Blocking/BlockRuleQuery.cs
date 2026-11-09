// Copyright (c) Dave Woestenborghs and contributors. Licensed under the MIT License. See LICENSE in the project root for license information.

using Umbraco.Community.PageNotFoundTracker.Core.Tracking;

namespace Umbraco.Community.PageNotFoundTracker.Core.Blocking;

/// <summary>
/// A request for one page of block rules.
/// </summary>
/// <param name="Skip">The number of rows to skip; below 0 counts as 0.</param>
/// <param name="Take">The page size; clamped to 1 to 100.</param>
/// <param name="SortColumn">The column to sort on.</param>
/// <param name="Descending">Whether to sort descending.</param>
/// <param name="Search">A case-insensitive "contains" term for the normalised pattern, or null, empty or whitespace-only for none (a non-blank term is not trimmed).</param>
/// <param name="Domain">The domain filter.</param>
/// <param name="Culture">The culture filter.</param>
public sealed record BlockRuleQuery(
    int Skip,
    int Take,
    BlockRuleSortColumn SortColumn,
    bool Descending,
    string? Search,
    TrackedUrlFilter Domain,
    TrackedUrlFilter Culture);
