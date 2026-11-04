// Copyright (c) Dave Woestenborghs and contributors. Licensed under the MIT License. See LICENSE in the project root for license information.

namespace Umbraco.Community.PageNotFoundTracker.Infrastructure.Redirects;

/// <summary>
/// Builds the Umbraco redirect route for a tracked URL.
/// </summary>
/// <remarks>
/// Umbraco's redirect finder looks up the route as <c>{domainRootContentId}/{path relative to the domain}</c> when a domain
/// matched (for example <c>1234/old</c>), and as the absolute path (<c>/old</c>) when none did. A bare path does not match
/// under a domain with its own path. Verified in <c>RedirectOrderingTests</c>.
/// </remarks>
internal static class RedirectRouteBuilder
{
    /// <summary>
    /// Builds the route.
    /// </summary>
    /// <param name="domainRootId">The content id of the matched domain's root node, or <c>null</c> when no domain matched.</param>
    /// <param name="relativePath">The path relative to the domain, as stored on the tracked URL.</param>
    /// <returns>The route to register.</returns>
    public static string Build(int? domainRootId, string relativePath)
    {
        var path = (relativePath ?? string.Empty).Trim();
        if (!path.StartsWith('/'))
        {
            path = "/" + path;
        }

        if (path.Length > 1)
        {
            path = path.TrimEnd('/');
            if (path.Length == 0)
            {
                path = "/";
            }
        }

        return domainRootId is { } id ? id.ToString(System.Globalization.CultureInfo.InvariantCulture) + path : path;
    }
}
