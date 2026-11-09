// Copyright (c) Dave Woestenborghs and contributors. Licensed under the MIT License. See LICENSE in the project root for license information.

using Umbraco.Cms.Core.Routing;

namespace Umbraco.Community.PageNotFoundTracker.Infrastructure.NotFound;

/// <summary>
/// Selects the Umbraco domain that serves a request from a list of parsed domains. Shared by the 404 handling
/// (<see cref="RequestDomainMatcher"/>) and blocking, so "matched domain" means the same thing in both.
/// </summary>
/// <remarks>
/// Umbraco's public <c>DomainUtilities.SelectDomain</c>/<c>SelectDomains</c> are not used: they work on
/// <c>DomainAndUri</c> pairs built from the current request URI and select on the authority only, so they cannot tell
/// <c>localhost:44402/sitea</c> from <c>localhost:44402/siteb</c>, and they cannot be fed a request that has no
/// published request. The host (case-insensitive) and the port must equal the request's, and the domain path (if any)
/// must be a segment prefix of the request path (case-insensitive). The most specific (longest) path wins, so a domain
/// with a path beats a bare host; on a tie the first domain wins. A domain stored without a port only matches a request
/// without a port, as Umbraco matches on host plus port.
/// </remarks>
internal static class DomainSelector
{
    /// <summary>
    /// Parses Umbraco's domains into entries, skipping the ones that cannot be parsed.
    /// </summary>
    /// <param name="domains">The domains, usually <c>IDomainCache.GetAll(false)</c>.</param>
    /// <returns>The entries, in the given order.</returns>
    public static IReadOnlyList<DomainEntry> FromCache(IEnumerable<Domain> domains)
    {
        var entries = new List<DomainEntry>();
        foreach (Domain domain in domains)
        {
            if (TryCreateEntry(domain, out DomainEntry? entry))
            {
                entries.Add(entry);
            }
        }

        return entries;
    }

    /// <summary>
    /// Selects the domain that serves a request.
    /// </summary>
    /// <param name="domains">The domain entries.</param>
    /// <param name="host">The request host.</param>
    /// <param name="port">The request port, or <c>null</c>.</param>
    /// <param name="path">The request path, without query string.</param>
    /// <returns>The matched entry, or <c>null</c>.</returns>
    public static DomainEntry? Select(IReadOnlyList<DomainEntry> domains, string host, int? port, string path)
    {
        int index = SelectIndex(domains, host, port, path);
        return index < 0 ? null : domains[index];
    }

    /// <summary>
    /// Selects the index of the domain that serves a request.
    /// </summary>
    /// <param name="domains">The domain entries.</param>
    /// <param name="host">The request host.</param>
    /// <param name="port">The request port, or <c>null</c>.</param>
    /// <param name="path">The request path, without query string.</param>
    /// <returns>The index of the matched entry, or -1.</returns>
    internal static int SelectIndex(IReadOnlyList<DomainEntry> domains, string host, int? port, string path)
    {
        string requestPath = path.TrimEnd('/');
        int best = -1;
        int bestLength = -1;

        for (int i = 0; i < domains.Count; i++)
        {
            DomainEntry domain = domains[i];
            if (!string.Equals(domain.Host, host, StringComparison.OrdinalIgnoreCase) || domain.Port != port)
            {
                continue;
            }

            bool matches = domain.Path.Length == 0
                || requestPath.Equals(domain.Path, StringComparison.OrdinalIgnoreCase)
                || requestPath.StartsWith(domain.Path + "/", StringComparison.OrdinalIgnoreCase);

            if (matches && domain.Path.Length > bestLength)
            {
                best = i;
                bestLength = domain.Path.Length;
            }
        }

        return best;
    }

    /// <summary>
    /// Parses one domain.
    /// </summary>
    /// <param name="domain">The domain.</param>
    /// <param name="entry">The entry, when parsed.</param>
    /// <returns><c>true</c> when the domain name could be parsed.</returns>
    internal static bool TryCreateEntry(Domain domain, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out DomainEntry? entry)
    {
        entry = null;
        if (!TryParse(domain.Name, out string host, out int? port, out string path))
        {
            return false;
        }

        entry = new DomainEntry(domain.Name.ToLowerInvariant(), host, port, path, domain.Culture ?? string.Empty);
        return true;
    }

    private static bool TryParse(string name, out string host, out int? port, out string path)
    {
        host = string.Empty;
        port = null;
        path = string.Empty;

        string rest = name.Trim();
        int schemeIndex = rest.IndexOf("://", StringComparison.Ordinal);
        if (schemeIndex >= 0)
        {
            rest = rest[(schemeIndex + 3)..];
        }

        int slash = rest.IndexOf('/', StringComparison.Ordinal);
        string authority = slash < 0 ? rest : rest[..slash];
        path = slash < 0 ? string.Empty : rest[slash..].TrimEnd('/');

        int colon = authority.LastIndexOf(':');
        if (colon >= 0)
        {
            if (!int.TryParse(authority[(colon + 1)..], out int parsedPort))
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
