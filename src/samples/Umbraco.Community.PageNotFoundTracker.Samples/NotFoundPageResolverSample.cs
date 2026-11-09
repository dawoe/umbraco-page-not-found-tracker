// Copyright (c) Dave Woestenborghs and contributors. Licensed under the MIT License. See LICENSE in the project root for license information.

using Umbraco.Cms.Core.PublishedCache;
using Umbraco.Cms.Core.Routing;
using Umbraco.Community.PageNotFoundTracker.Core.NotFound;

namespace Umbraco.Community.PageNotFoundTracker.Samples;

// docs:begin resolver-class

/// <summary>
/// Sample resolver: every 404 is answered by one fixed page, chosen by its key, instead of the page found by
/// document type alias under the site root. It must be singleton-compatible: the page finder holds it.
/// </summary>
public sealed class NotFoundPageResolverSample : INotFoundPageResolver
{
    // Replace with the key of your own 404 page.
    private static readonly Guid PageKey = Guid.Parse("00000000-0000-0000-0000-000000000001");

    private readonly IPublishedContentCache contentCache;

    /// <summary>
    /// Initializes a new instance of the <see cref="NotFoundPageResolverSample"/> class.
    /// </summary>
    /// <param name="contentCache">The published content cache.</param>
    public NotFoundPageResolverSample(IPublishedContentCache contentCache) => this.contentCache = contentCache;

    /// <inheritdoc />
    public async Task<NotFoundPage?> ResolveAsync(NotFoundRequestContext context, Domain? domain, CancellationToken cancellationToken)
    {
        var page = await this.contentCache.GetByIdAsync(PageKey, false);

        // null means "not found": Umbraco's default 404 behaviour applies.
        return page is null ? null : new NotFoundPage(page, domain?.Culture);
    }
}

// docs:end resolver-class
