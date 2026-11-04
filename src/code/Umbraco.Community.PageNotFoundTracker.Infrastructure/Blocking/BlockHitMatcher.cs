// Copyright (c) Dave Woestenborghs and contributors. Licensed under the MIT License. See LICENSE in the project root for license information.

using Umbraco.Community.PageNotFoundTracker.Core.Tracking;

namespace Umbraco.Community.PageNotFoundTracker.Infrastructure.Blocking;

/// <summary>
/// Builds the predicate that decides which queued hits a block rule covers. The rule service (this server) and the cache
/// refresher (the other servers) both use it, so they discard the same hits.
/// </summary>
internal static class BlockHitMatcher
{
    /// <summary>
    /// Creates the predicate: the hit's domain and culture match the rule's (ignoring case; empty means all) and its
    /// relative path matches the pattern.
    /// </summary>
    /// <param name="normalisedPattern">The normalised pattern.</param>
    /// <param name="domain">The rule's domain; empty for all.</param>
    /// <param name="culture">The rule's culture; empty for all.</param>
    /// <returns>The predicate.</returns>
    public static Func<TrackedUrlHit, bool> Create(string normalisedPattern, string domain, string culture)
    {
        var compiled = CompiledBlockPattern.Compile(normalisedPattern);
        return hit =>
            (domain.Length == 0 || string.Equals(hit.Domain, domain, StringComparison.OrdinalIgnoreCase))
            && (culture.Length == 0 || string.Equals(hit.Culture, culture, StringComparison.OrdinalIgnoreCase))
            && compiled.IsMatch(hit.RelativePath);
    }
}
