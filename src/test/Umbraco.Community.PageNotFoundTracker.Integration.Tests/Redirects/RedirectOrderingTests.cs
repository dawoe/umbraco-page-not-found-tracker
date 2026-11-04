// Copyright (c) Dave Woestenborghs and contributors. Licensed under the MIT License. See LICENSE in the project root for license information.

/* Findings for spec 5 (verified on Umbraco 17.8.0 with ILSpy and the tests below):
   (a) Route format. ContentFinderByRedirectUrl first looks up the absolute decoded request path ("/sitea/old", WITH the
       domain's own path, leading slash) and, only when a domain matched and that found nothing, "{domainRootContentId}" +
       the path relative to the domain ("1234/old", relative part starts with "/"). So without a domain register "/path";
       with a domain register "{rootId}/{relativePath}" (RelativePath of the tracked URL, starting with "/"). A bare
       "/old" does not match a request under a domain path. Culture: Register(route, key, culture) with the culture
       lower-cased (the lookup compares culture.ToLower()); a null or empty culture matches every culture; a culture
       specific entry is preferred over a culture-less one.
   (b) Query string. Lookup uses the decoded absolute PATH only; the query string is never part of the route, so a route
       registered with "?..." never matches. The visitor's query string is appended to the redirect target URL
       ("/old-page?x=1" gives Location "/other/?x=1"). Rows whose URL contains a query string cannot be redirected.
   (c) Disabling. Umbraco:CMS:WebRouting:DisableRedirectUrlTracking only stops the automatic creation of redirects when
       content is renamed or moved (RedirectTrackingHandler). Nothing switches off APPLYING redirects: the finder runs
       regardless (only removing ContentFinderByRedirectUrl from the content finder collection would).
   (d) Order. A registered redirect applies before spec 2's last-chance finder (301 instead of the 404 page); a path
       without a redirect still gets the 404 page. Not a blocker.
   Also observed (SQLite): the request path is matched case-insensitively against the stored route.
*/

using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Umbraco.Cms.Core.Cache;
using Umbraco.Cms.Core.DependencyInjection;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Models.ContentEditing;
using Umbraco.Cms.Core.Notifications;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Tests.Common.Testing;

namespace Umbraco.Community.PageNotFoundTracker.Integration.Tests.Redirects;

/// <summary>
/// Spike for spec 5. Proves that a redirect registered through Umbraco's <c>IRedirectUrlService</c> applies before the
/// spec 2 last-chance finder (the 404 page).
/// </summary>
[TestFixture]
[UmbracoTest(Database = UmbracoTestOptions.Database.NewSchemaPerTest, Boot = true)]
internal sealed class RedirectOrderingTests : PageNotFoundTestBase
{
    /// <inheritdoc />
    protected override void CustomTestSetup(IUmbracoBuilder builder)
    {
        base.CustomTestSetup(builder);

        // Domains must reach the caches that domain matching reads.
        builder.AddNotificationHandler<DomainSavedNotification, DomainSavedDistributedCacheNotificationHandler>();
    }

    /// <summary>
    /// Tests that a registered redirect wins over the 404 page and another missing path still gets the 404 page.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task Registered_redirect_applies_before_the_404_page()
    {
        this.RegisterRedirect("/old-page", OtherPageKey);

        using var redirected = await this.GetWithoutFollowingAsync("/old-page");
        using var missing = await this.GetWithoutFollowingAsync("/other-missing");
        var missingBody = await missing.Content.ReadAsStringAsync();

        Assert.Multiple(() =>
        {
            Assert.That(redirected.StatusCode, Is.EqualTo(HttpStatusCode.MovedPermanently));
            Assert.That(redirected.Headers.Location?.ToString(), Is.EqualTo("/other/"));
            Assert.That(missing.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
            Assert.That(missingBody, Does.Contain(NotFoundPageTitle));
        });
    }

    /// <summary>
    /// Tests what lookup does with a query string, and documents it.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task Redirect_with_query_string_is_ignored_or_honoured()
    {
        this.RegisterRedirect("/old-page", OtherPageKey);

        using var plain = await this.GetWithoutFollowingAsync("/old-page?x=1");
        TestContext.Out.WriteLine($"/old-page?x=1 -> {(int)plain.StatusCode} {plain.Headers.Location}");

        this.RegisterRedirect("/old-page?x=1", OtherPageKey);
        using var withQuery = await this.GetWithoutFollowingAsync("/missing-q?x=1");
        TestContext.Out.WriteLine($"/missing-q?x=1 (no registration) -> {(int)withQuery.StatusCode}");

        Assert.That(plain.StatusCode, Is.EqualTo(HttpStatusCode.MovedPermanently));
    }

    /// <summary>
    /// Documents the route forms the front-end lookup tries when a domain with a path matches.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task Route_forms_with_and_without_a_matched_domain()
    {
        var home = this.GetRequiredService<IContentService>().GetRootContent().Single();
        var domains = new DomainsUpdateModel { Domains = [new DomainModel { DomainName = "localhost/sitea", IsoCode = "en-US" }] };
        var updated = this.GetRequiredService<IDomainService>().UpdateDomainsAsync(home.Key, domains).GetAwaiter().GetResult();
        Assert.That(updated.Success, Is.True, $"Assigning the domain failed: {updated.Status}.");

        // Registered as "{rootId}/{path relative to the domain}".
        this.RegisterRedirect($"{home.Id}/old-domain", OtherPageKey);
        using var domainForm = await this.GetWithoutFollowingAsync("/sitea/old-domain");

        // Registered as the full absolute path (including the domain's own path): tried first by the finder, also with a domain.
        this.RegisterRedirect("/sitea/old-absolute", OtherPageKey);
        using var absoluteForm = await this.GetWithoutFollowingAsync("/sitea/old-absolute");

        // A bare path without the domain prefix does not match a request under the domain.
        this.RegisterRedirect("/old-bare", OtherPageKey);
        using var bare = await this.GetWithoutFollowingAsync("/sitea/old-bare");

        // A culture specific registration (lower-case culture) for the domain culture.
        this.GetRequiredService<IRedirectUrlService>().Register($"{home.Id}/old-culture", OtherPageKey, "en-us");
        using var cultureForm = await this.GetWithoutFollowingAsync("/sitea/old-culture");

        // The route is compared by the SHA-1 of the exact string, so case matters.
        using var upper = await this.GetWithoutFollowingAsync("/sitea/OLD-DOMAIN");

        TestContext.Out.WriteLine($"domain form {(int)domainForm.StatusCode} {domainForm.Headers.Location}");
        TestContext.Out.WriteLine($"absolute form {(int)absoluteForm.StatusCode} {absoluteForm.Headers.Location}");
        TestContext.Out.WriteLine($"bare form {(int)bare.StatusCode} {bare.Headers.Location}");
        TestContext.Out.WriteLine($"culture form {(int)cultureForm.StatusCode} {cultureForm.Headers.Location}");
        TestContext.Out.WriteLine($"upper-case request {(int)upper.StatusCode} {upper.Headers.Location}");

        Assert.Multiple(() =>
        {
            Assert.That(domainForm.StatusCode, Is.EqualTo(HttpStatusCode.MovedPermanently), "domain form");
            Assert.That(absoluteForm.StatusCode, Is.EqualTo(HttpStatusCode.MovedPermanently), "absolute form");
            Assert.That(bare.StatusCode, Is.EqualTo(HttpStatusCode.NotFound), "bare form under a domain");
            Assert.That(cultureForm.StatusCode, Is.EqualTo(HttpStatusCode.MovedPermanently), "culture form");
        });
    }

    /// <summary>
    /// Tests that a route registered with a query string is never found, because lookup uses the path only.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task Route_registered_with_a_query_string_is_never_matched()
    {
        this.RegisterRedirect("/old-q?x=1", OtherPageKey);

        using var withQuery = await this.GetWithoutFollowingAsync("/old-q?x=1");
        using var withoutQuery = await this.GetWithoutFollowingAsync("/old-q");

        Assert.Multiple(() =>
        {
            Assert.That(withQuery.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
            Assert.That(withoutQuery.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
        });
    }

    private void RegisterRedirect(string route, Guid contentKey) =>
        this.GetRequiredService<IRedirectUrlService>().Register(route, contentKey, null);

    private async Task<HttpResponseMessage> GetWithoutFollowingAsync(string pathAndQuery)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(pathAndQuery, UriKind.Relative));
        return await this.Client.SendAsync(request);
    }
}
