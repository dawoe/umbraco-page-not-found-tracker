// Copyright (c) Dave Woestenborghs and contributors. Licensed under the MIT License. See LICENSE in the project root for license information.

using Umbraco.Cms.Core.PublishedCache;
using Umbraco.Cms.Core.Routing;
using Umbraco.Community.PageNotFoundTracker.Core.NotFound;

namespace Umbraco.Community.PageNotFoundTracker.Infrastructure.NotFound;

/// <summary>
/// Finds the Umbraco domain that serves a request by reading the domains from <see cref="IDomainCache"/>.
/// </summary>
/// <remarks>
/// The matching rules live in <see cref="DomainSelector"/> (shared with blocking): host and port must equal the
/// request's, the domain path must be a segment prefix of the request path, and the longest path wins.
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
        var domains = new List<Domain>();
        var entries = new List<DomainEntry>();
        foreach (Domain domain in this.domainCache.GetAll(false))
        {
            if (DomainSelector.TryCreateEntry(domain, out DomainEntry? entry))
            {
                domains.Add(domain);
                entries.Add(entry);
            }
        }

        int index = DomainSelector.SelectIndex(entries, context.Host, context.Port, context.Path);
        return index < 0 ? null : domains[index];
    }
}
