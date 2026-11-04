// Copyright (c) Dave Woestenborghs and contributors. Licensed under the MIT License. See LICENSE in the project root for license information.

namespace Umbraco.Community.PageNotFoundTracker.Core.Tracking;

/// <summary>
/// One page of tracked URLs.
/// </summary>
/// <param name="Items">The rows of the page.</param>
/// <param name="Total">The number of rows that match the query, over all pages.</param>
public sealed record TrackedUrlPage(IReadOnlyList<TrackedUrl> Items, long Total);
