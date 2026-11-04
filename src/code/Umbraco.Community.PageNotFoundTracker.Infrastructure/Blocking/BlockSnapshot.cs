// Copyright (c) Dave Woestenborghs and contributors. Licensed under the MIT License. See LICENSE in the project root for license information.

using Umbraco.Community.PageNotFoundTracker.Core.Blocking;
using Umbraco.Community.PageNotFoundTracker.Core.NotFound;
using Umbraco.Community.PageNotFoundTracker.Infrastructure.NotFound;
using Umbraco.Community.PageNotFoundTracker.Infrastructure.Tracking;

namespace Umbraco.Community.PageNotFoundTracker.Infrastructure.Blocking;

/// <summary>
/// An immutable set of block rules with their compiled patterns and Umbraco's domains, replaced as a whole on refresh.
/// Rules are sorted once by specificity (domain-scoped first, then culture-scoped, then the longer literal prefix,
/// then the lowest status code), so a request scans them in order and the first match wins.
/// </summary>
internal sealed class BlockSnapshot
{
    private readonly Entry[] rules;
    private readonly IReadOnlyList<DomainEntry> domains;

    private BlockSnapshot(Entry[] rules, IReadOnlyList<DomainEntry> domains)
    {
        this.rules = rules;
        this.domains = domains;
        this.NeedsDomainMatch = rules.Any(r => r.Domain.Length > 0 || r.Culture.Length > 0)
            || domains.Any(d => d.Path.Length > 0);
    }

    /// <summary>
    /// Gets the empty snapshot: no rules, nothing is blocked.
    /// </summary>
    public static BlockSnapshot Empty { get; } = new([], []);

    /// <summary>
    /// Gets a value indicating whether a request needs domain matching: some rule is scoped to a domain or culture, or
    /// some domain has its own path (an alias prefix to strip).
    /// </summary>
    public bool NeedsDomainMatch { get; }

    /// <summary>
    /// Builds a snapshot.
    /// </summary>
    /// <param name="rules">The rules; their <see cref="BlockRule.Pattern"/> is already normalised.</param>
    /// <param name="domains">Umbraco's domains (see <see cref="DomainSelector.FromCache"/>).</param>
    /// <returns>The snapshot.</returns>
    public static BlockSnapshot Build(IEnumerable<BlockRule> rules, IReadOnlyList<DomainEntry> domains)
    {
        Entry[] sorted = rules
            .Select(r => new Entry(
                CompiledBlockPattern.Compile(r.Pattern),
                r.StatusCode,
                (r.Domain ?? string.Empty).Trim().ToLowerInvariant(),
                (r.Culture ?? string.Empty).Trim()))
            .OrderBy(e => e.Domain.Length > 0 ? 0 : 1)
            .ThenBy(e => e.Culture.Length > 0 ? 0 : 1)
            .ThenByDescending(e => e.Pattern.LiteralPrefixLength)
            .ThenBy(e => e.StatusCode)
            .ToArray();

        return new BlockSnapshot(sorted, domains.ToArray());
    }

    /// <summary>
    /// Finds the status code of the most specific rule that matches a request. The snapshot selects the request's
    /// domain itself (only when <see cref="NeedsDomainMatch"/>), strips that domain's own path (alias prefix) and
    /// applies the rules' domain and culture scope. Umbraco-owned paths never match, with or without the prefix.
    /// </summary>
    /// <param name="path">
    /// The request path, without query string, normalised by the caller with the tracking rules
    /// (<see cref="TrackedUrlNormalizer.NormalizePath"/>: leading slash, repeated and trailing slashes removed,
    /// lower-cased) but NOT percent-decoded again (Kestrel already decoded it once).
    /// </param>
    /// <param name="host">The request host.</param>
    /// <param name="port">The request port, or <c>null</c>.</param>
    /// <returns>The status code, or <c>null</c> when no rule matches.</returns>
    public int? FindStatus(string path, string host, int? port)
    {
        if (this.rules.Length == 0 || UmbracoOwnedPaths.IsUmbracoOwned(path))
        {
            return null;
        }

        DomainEntry? domain = this.NeedsDomainMatch ? DomainSelector.Select(this.domains, host, port, path) : null;
        string relative = domain is null ? path : TrackedUrlNormalizer.RelativeTo(path, domain.Name);
        if (UmbracoOwnedPaths.IsUmbracoOwned(relative))
        {
            return null;
        }

        foreach (Entry rule in this.rules)
        {
            if (rule.Domain.Length > 0
                && (domain is null || !string.Equals(rule.Domain, domain.Name, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            if (rule.Culture.Length > 0
                && (domain is null || !string.Equals(rule.Culture, domain.Culture, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            if (rule.Pattern.IsMatch(relative))
            {
                return rule.StatusCode;
            }
        }

        return null;
    }

    private sealed record Entry(CompiledBlockPattern Pattern, int StatusCode, string Domain, string Culture);
}
