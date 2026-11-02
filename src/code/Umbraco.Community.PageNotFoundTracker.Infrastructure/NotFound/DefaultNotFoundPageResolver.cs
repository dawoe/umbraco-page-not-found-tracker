// Copyright (c) Dave Woestenborghs and contributors. Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.Options;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Core.PublishedCache;
using Umbraco.Cms.Core.Routing;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core.Services.Navigation;
using Umbraco.Community.PageNotFoundTracker.Core.Configuration;
using Umbraco.Community.PageNotFoundTracker.Core.NotFound;

namespace Umbraco.Community.PageNotFoundTracker.Infrastructure.NotFound;

/// <summary>
/// Resolves the 404 page: the first published page of the configured document type under the root node of the
/// matched domain, direct children before deeper descendants.
/// </summary>
/// <remarks>
/// The root node is the node the domain is assigned to, or the first root node of the content tree when no domain
/// matched. The domain culture selects the language variant; without one Umbraco's default language is used for the
/// selection, but only a domain culture is reported back. Unpublished pages and pages without a published variant in
/// the culture are skipped.
/// </remarks>
internal sealed class DefaultNotFoundPageResolver : INotFoundPageResolver
{
    private readonly IOptionsMonitor<PageNotFoundTrackerSettings> settings;
    private readonly IPublishedContentCache contentCache;
    private readonly IDocumentNavigationQueryService navigation;
    private readonly ILanguageService languageService;

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultNotFoundPageResolver"/> class.
    /// </summary>
    /// <param name="settings">The package settings.</param>
    /// <param name="contentCache">The published content cache.</param>
    /// <param name="navigation">The document navigation query service.</param>
    /// <param name="languageService">The language service.</param>
    public DefaultNotFoundPageResolver(
        IOptionsMonitor<PageNotFoundTrackerSettings> settings,
        IPublishedContentCache contentCache,
        IDocumentNavigationQueryService navigation,
        ILanguageService languageService)
    {
        this.settings = settings;
        this.contentCache = contentCache;
        this.navigation = navigation;
        this.languageService = languageService;
    }

    /// <inheritdoc />
    public async Task<NotFoundPage?> ResolveAsync(NotFoundRequestContext context, Domain? domain, CancellationToken cancellationToken)
    {
        var alias = this.settings.CurrentValue.NotFoundPageDocumentTypeAlias;
        if (string.IsNullOrWhiteSpace(alias))
        {
            return null;
        }

        var rootKey = await this.GetRootKeyAsync(domain);
        if (rootKey is null)
        {
            return null;
        }

        var domainCulture = string.IsNullOrWhiteSpace(domain?.Culture) ? null : domain.Culture;
        var variantCulture = domainCulture ?? await this.languageService.GetDefaultIsoCodeAsync();

        var candidates = new List<Guid>();
        if (this.navigation.TryGetChildrenKeysOfType(rootKey.Value, alias, out var children))
        {
            candidates.AddRange(children);
        }

        if (this.navigation.TryGetDescendantsKeysOfType(rootKey.Value, alias, out var descendants))
        {
            candidates.AddRange(descendants);
        }

        foreach (var key in candidates.Distinct())
        {
            cancellationToken.ThrowIfCancellationRequested();

            var content = await this.contentCache.GetByIdAsync(key, false);
            if (content is null)
            {
                continue;
            }

            var culture = content.ContentType.Variations.HasFlag(ContentVariation.Culture) ? variantCulture : null;
            if (content.IsPublished(culture))
            {
                return new NotFoundPage(content, domainCulture);
            }
        }

        return null;
    }

    private async Task<Guid?> GetRootKeyAsync(Domain? domain)
    {
        if (domain?.ContentId is { } contentId)
        {
            var root = await this.contentCache.GetByIdAsync(contentId, false);
            return root?.Key;
        }

        if (!this.navigation.TryGetRootKeys(out var roots))
        {
            return null;
        }

        var first = roots.FirstOrDefault();
        return first == Guid.Empty ? null : first;
    }
}
