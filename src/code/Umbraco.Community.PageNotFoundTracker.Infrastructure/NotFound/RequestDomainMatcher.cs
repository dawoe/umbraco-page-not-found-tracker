// Copyright (c) Dave Woestenborghs and contributors. Licensed under the MIT License. See LICENSE in the project root for license information.

using Umbraco.Cms.Core.PublishedCache;
using Umbraco.Cms.Core.Routing;
using Umbraco.Community.PageNotFoundTracker.Core.NotFound;

namespace Umbraco.Community.PageNotFoundTracker.Infrastructure.NotFound;

/// <summary>
/// Finds the Umbraco domain that serves a request by reading the domains from <see cref="IDomainCache"/>.
/// </summary>
/// <remarks>
/// Umbraco's public <c>DomainUtilities.SelectDomain</c>/<c>SelectDomains</c> are not used: they work on
/// <c>DomainAndUri</c> pairs built from the current request URI and select on the authority only, so they cannot tell
/// <c>localhost:44402/sitea</c> from <c>localhost:44402/siteb</c>, and they cannot be fed a request that has no
/// published request. This class therefore implements the match itself: the host (case-insensitive) and the port must
/// equal the request's, and the domain path (if any) must be a segment prefix of the request path (case-insensitive).
/// The most specific (longest) path wins, so a domain with a path beats a bare host. A domain stored without a port
/// only matches a request without a port, as Umbraco matches on host plus port.
/// </remarks>
internal sealed class RequestDomainMatcher : IRequestDomainMatcher
{
    private readonly IDomainCache domainCache;

    /// <summary>
    /// Initializes a new instance of the <see cref="RequestDomainMatcher"/> class.
    /// </summary>
    /// <param name="cache">The domain cache.</param>
    public RequestDomainMatcher(IDomainCache cache)
    {
        this.domainCache = cache;
    }

    /// <inheritdoc />
    public Domain? Match(NotFoundRequestContext context)
    {
        var requestPath = context.Path.TrimEnd('/');
        Domain? best = null;
        var bestLength = -1;

        foreach (var domain in this.domainCache.GetAll(false))
        {
            if (!TryParse(domain.Name, out var host, out var port, out var path))
            {
                continue;
            }

            if (!string.Equals(host, context.Host, StringComparison.OrdinalIgnoreCase) || port != context.Port)
            {
                continue;
            }

            var matches = path.Length == 0
                || requestPath.Equals(path, StringComparison.OrdinalIgnoreCase)
                || requestPath.StartsWith(path + "/", StringComparison.OrdinalIgnoreCase);

            if (matches && path.Length > bestLength)
            {
                best = domain;
                bestLength = path.Length;
            }
        }

        return best;
    }

    private static bool TryParse(string name, out string host, out int? port, out string path)
    {
        host = string.Empty;
        port = null;
        path = string.Empty;

        var rest = name.Trim();
        var schemeIndex = rest.IndexOf("://", StringComparison.Ordinal);
        if (schemeIndex >= 0)
        {
            rest = rest[(schemeIndex + 3)..];
        }

        var slash = rest.IndexOf('/', StringComparison.Ordinal);
        var authority = slash < 0 ? rest : rest[..slash];
        path = slash < 0 ? string.Empty : rest[slash..].TrimEnd('/');

        var colon = authority.LastIndexOf(':');
        if (colon >= 0)
        {
            if (!int.TryParse(authority[(colon + 1)..], out var parsedPort))
            {
                return false;
            }

            port = parsedPort;
            authority = authority[..colon];
        }

        host = authority;
        return host.Length > 0;
    }
}
