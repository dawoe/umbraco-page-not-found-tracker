// Copyright (c) Dave Woestenborghs and contributors. Licensed under the MIT License. See LICENSE in the project root for license information.

using Umbraco.Community.PageNotFoundTracker.Infrastructure.Tracking;

namespace Umbraco.Community.PageNotFoundTracker.Infrastructure.Blocking;

/// <summary>
/// Normalises block patterns the same way tracked paths are normalised, after decoding percent escapes once.
/// </summary>
internal static class BlockPatternNormalizer
{
    /// <summary>
    /// Normalises a pattern: percent-decoded once, lower-cased, leading slash ensured, repeated slashes collapsed and
    /// the trailing slash dropped (a lone slash is kept).
    /// </summary>
    /// <param name="pattern">The pattern as entered.</param>
    /// <returns>The normalised pattern.</returns>
    public static string Normalize(string pattern)
    {
        return TrackedUrlNormalizer.NormalizePath(Uri.UnescapeDataString(pattern ?? string.Empty));
    }
}
