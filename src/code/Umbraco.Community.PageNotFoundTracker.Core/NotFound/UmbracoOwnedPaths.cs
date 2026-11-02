// Copyright (c) Dave Woestenborghs and contributors. Licensed under the MIT License. See LICENSE in the project root for license information.

namespace Umbraco.Community.PageNotFoundTracker.Core.NotFound;

/// <summary>
/// Paths that belong to Umbraco (backoffice, Management API and their assets), shared by the 404 handling and blocking.
/// </summary>
public static class UmbracoOwnedPaths
{
    private static readonly string[] OwnedPrefixes = ["/umbraco", "/App_Plugins", "/_framework"];

    /// <summary>
    /// Gets the path prefixes owned by Umbraco.
    /// </summary>
    public static IReadOnlyList<string> Prefixes => OwnedPrefixes;

    /// <summary>
    /// Determines whether a path is Umbraco-owned. The comparison ignores case and works on segment boundaries.
    /// </summary>
    /// <param name="path">The request path, without query string.</param>
    /// <returns><c>true</c> when the path is one of the prefixes or below one.</returns>
    public static bool IsUmbracoOwned(string path)
    {
        foreach (var prefix in OwnedPrefixes)
        {
            if (path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
                && (path.Length == prefix.Length || path[prefix.Length] == '/'))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Determines whether a path is a file request, meaning its last segment has an extension.
    /// </summary>
    /// <param name="path">The request path, without query string.</param>
    /// <returns><c>true</c> when the last segment has an extension.</returns>
    public static bool IsFileRequest(string path)
    {
        var lastSegment = path[(path.LastIndexOf('/') + 1)..];
        return Path.HasExtension(lastSegment);
    }
}
