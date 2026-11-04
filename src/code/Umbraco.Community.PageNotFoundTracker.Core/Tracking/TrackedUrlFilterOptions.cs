// Copyright (c) Dave Woestenborghs and contributors. Licensed under the MIT License. See LICENSE in the project root for license information.

namespace Umbraco.Community.PageNotFoundTracker.Core.Tracking;

/// <summary>
/// The values the domain and culture filters can offer.
/// </summary>
/// <param name="Domains">The distinct non-empty domains, sorted ordinal.</param>
/// <param name="Cultures">The distinct non-empty cultures, sorted ordinal.</param>
/// <param name="HasEmptyDomain">Whether any row has no domain.</param>
/// <param name="HasEmptyCulture">Whether any row has no culture.</param>
public sealed record TrackedUrlFilterOptions(
    IReadOnlyList<string> Domains,
    IReadOnlyList<string> Cultures,
    bool HasEmptyDomain,
    bool HasEmptyCulture);
