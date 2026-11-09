// Copyright (c) Dave Woestenborghs and contributors. Licensed under the MIT License. See LICENSE in the project root for license information.

namespace Umbraco.Community.PageNotFoundTracker.Core.Blocking;

/// <summary>
/// One page of block rules.
/// </summary>
/// <param name="Items">The rules of the page.</param>
/// <param name="Total">The number of rules that match the query, over all pages.</param>
public sealed record BlockRulePage(IReadOnlyList<BlockRule> Items, long Total);
