// Copyright (c) Dave Woestenborghs and contributors. Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Text.Json;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.PublishedCache;
using Umbraco.Cms.Core.Routing;
using Umbraco.Cms.Core.Services;
using Umbraco.Community.PageNotFoundTracker.Core.Redirects;
using Umbraco.Community.PageNotFoundTracker.Core.Tracking;
using Umbraco.Extensions;

namespace Umbraco.Community.PageNotFoundTracker.Infrastructure.Redirects;

/// <summary>
/// The default <see cref="IRedirectProvider"/>: registers the redirect with Umbraco's <see cref="IRedirectUrlService"/>,
/// so it shows in Umbraco's own redirect management and is applied by Umbraco's redirect content finder.
/// </summary>
/// <remarks>
/// The payload is <c>{ "targetKey": "&lt;guid&gt;" }</c>, the key of a published content item. Umbraco has no setting that
/// switches off applying redirects (<c>WebRouting:DisableRedirectUrlTracking</c> only stops their automatic creation on
/// rename or move), so none is checked here. Query strings are not part of Umbraco's lookup, so rows with one are not handled.
/// </remarks>
internal sealed class UmbracoRedirectProvider : IRedirectProvider
{
    private const string TargetKeyProperty = "targetKey";
    private const string InvalidPayloadMessage = "A target page is required: the payload must be { \"targetKey\": \"<guid>\" }.";
    private const string TargetNotFoundMessage = "The target page does not exist or is not published.";

    private readonly IRedirectUrlService redirectUrlService;
    private readonly IDomainCache domainCache;
    private readonly IPublishedContentCache contentCache;

    /// <summary>
    /// Initializes a new instance of the <see cref="UmbracoRedirectProvider"/> class.
    /// </summary>
    /// <param name="redirectUrlService">Umbraco's redirect URL service.</param>
    /// <param name="domainCache">The domain cache.</param>
    /// <param name="contentCache">The published content cache.</param>
    public UmbracoRedirectProvider(IRedirectUrlService redirectUrlService, IDomainCache domainCache, IPublishedContentCache contentCache)
    {
        this.redirectUrlService = redirectUrlService;
        this.domainCache = domainCache;
        this.contentCache = contentCache;
    }

    /// <inheritdoc />
    public bool CanHandle(TrackedUrl url) =>
        !url.IsFile && !url.Url.Contains('?', StringComparison.Ordinal);

    /// <inheritdoc />
    public async Task<RedirectResult> CreateAsync(TrackedUrl url, JsonElement? payload, CancellationToken cancellationToken)
    {
        if (!this.CanHandle(url))
        {
            return RedirectResult.Failure("This URL cannot be redirected: files and URLs with a query string are not supported.");
        }

        if (!TryReadTargetKey(payload, out var targetKey))
        {
            return RedirectResult.Failure(InvalidPayloadMessage);
        }

        var target = await this.contentCache.GetByIdAsync(targetKey, false);
        if (target is null)
        {
            return RedirectResult.Failure(TargetNotFoundMessage);
        }

        if (!string.IsNullOrWhiteSpace(url.Culture) && !target.IsInvariantOrHasCulture(url.Culture))
        {
            return RedirectResult.Failure($"The target page is not published in {url.Culture}.");
        }

        int? rootId = null;
        if (!string.IsNullOrEmpty(url.Domain))
        {
            var domain = this.FindDomain(url);
            if (domain is null)
            {
                return RedirectResult.Failure($"The domain '{url.Domain}' no longer exists in Umbraco, so the redirect cannot be created.");
            }

            if (domain.ContentId <= 0)
            {
                return RedirectResult.Failure($"The domain '{url.Domain}' has no root page, so the redirect cannot be created.");
            }

            rootId = domain.ContentId;
        }

        var route = RedirectRouteBuilder.Build(rootId, url.RelativePath);
        var culture = string.IsNullOrWhiteSpace(url.Culture) ? null : url.Culture.ToLowerInvariant();

        // Replace an existing redirect for the same route and culture, so creating twice is safe.
        var existing = this.redirectUrlService.GetMostRecentRedirectUrl(route, culture);
        if (existing is not null
            && string.Equals(existing.Url, route, StringComparison.Ordinal)
            && string.Equals(NullIfEmpty(existing.Culture), culture, StringComparison.OrdinalIgnoreCase))
        {
            // Delete(IRedirectUrl) fails in Umbraco 17.8 (it casts the integer id to a Guid), so delete by key.
            this.redirectUrlService.Delete(existing.Key);
        }

        this.redirectUrlService.Register(route, targetKey, culture);
        return RedirectResult.Success();
    }

    private static string? NullIfEmpty(string? value) => string.IsNullOrEmpty(value) ? null : value;

    private static string Normalize(string name) => name.Trim().TrimEnd('/').ToLowerInvariant();

    private static bool TryReadTargetKey(JsonElement? payload, out Guid key)
    {
        key = Guid.Empty;
        if (payload is not { ValueKind: JsonValueKind.Object } element
            || !element.TryGetProperty(TargetKeyProperty, out var value)
            || value.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        return Guid.TryParse(value.GetString(), out key) && key != Guid.Empty;
    }

    private Domain? FindDomain(TrackedUrl url)
    {
        var name = Normalize(url.Domain);
        var matches = this.domainCache.GetAll(false)
            .Where(d => Normalize(d.Name) == name)
            .ToList();

        if (matches.Count <= 1)
        {
            return matches.FirstOrDefault();
        }

        // Several domains share the name: only the one with the record's culture is meant; none matching is no match.
        return matches.FirstOrDefault(d => string.Equals(d.Culture, url.Culture, StringComparison.OrdinalIgnoreCase));
    }
}
