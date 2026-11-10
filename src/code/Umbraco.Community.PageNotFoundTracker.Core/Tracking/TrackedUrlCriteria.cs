// Copyright (c) Dave Woestenborghs and contributors. Licensed under the MIT License. See LICENSE in the project root for license information.

namespace Umbraco.Community.PageNotFoundTracker.Core.Tracking;

/// <summary>
/// The filter that selects tracked URLs, the same one the dashboard list applies: a search term plus a domain and a
/// culture filter.
/// </summary>
/// <param name="Search">The search term; null or whitespace means no search.</param>
/// <param name="Domain">The domain filter.</param>
/// <param name="Culture">The culture filter.</param>
public sealed record TrackedUrlCriteria(string? Search, TrackedUrlFilter Domain, TrackedUrlFilter Culture);
