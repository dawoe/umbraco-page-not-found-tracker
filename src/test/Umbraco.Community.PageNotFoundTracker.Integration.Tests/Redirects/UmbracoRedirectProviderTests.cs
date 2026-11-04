// Copyright (c) Dave Woestenborghs and contributors. Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Net;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Umbraco.Cms.Core.Cache;
using Umbraco.Cms.Core.DependencyInjection;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Models.ContentEditing;
using Umbraco.Cms.Core.Notifications;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Tests.Common.Testing;
using Umbraco.Community.PageNotFoundTracker.Core.Redirects;
using Umbraco.Community.PageNotFoundTracker.Core.Tracking;

namespace Umbraco.Community.PageNotFoundTracker.Integration.Tests.Redirects;

/// <summary>
/// Integration tests for the default redirect provider: redirects created through it are applied by Umbraco.
/// </summary>
[TestFixture]
[UmbracoTest(Database = UmbracoTestOptions.Database.NewSchemaPerTest, Boot = true)]
internal sealed class UmbracoRedirectProviderTests : PageNotFoundTestBase
{
    /// <inheritdoc />
    protected override void CustomTestSetup(IUmbracoBuilder builder)
    {
        base.CustomTestSetup(builder);

        // Domains must reach the caches that the provider and domain matching read.
        builder.AddNotificationHandler<DomainSavedNotification, DomainSavedDistributedCacheNotificationHandler>();
    }

    /// <summary>
    /// Tests that a redirect created for a domain and culture is applied to a request under that domain.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task Creates_redirect_for_domain_root_and_culture_then_request_redirects()
    {
        this.AssignDomain("localhost/sitea", "en-US");
        var provider = this.GetRequiredService<IRedirectProvider>();
        var row = new TrackedUrl { Url = "/sitea/old", RelativePath = "/old", Domain = "localhost/sitea", Culture = "en-US" };

        Assert.That(provider.CanHandle(row), Is.True);
        RedirectResult result = await provider.CreateAsync(row, Payload(OtherPageKey), CancellationToken.None);
        using var response = await this.GetAsync("/sitea/old");

        Assert.Multiple(() =>
        {
            Assert.That(result.Succeeded, Is.True, result.Message);
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.MovedPermanently));
            Assert.That(response.Headers.Location?.ToString(), Does.Contain("other"));
        });
    }

    /// <summary>
    /// Tests that creating the same redirect twice leaves one redirect, pointing at the new target.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task Creating_twice_leaves_one_redirect_with_the_new_target()
    {
        var provider = this.GetRequiredService<IRedirectProvider>();
        var row = new TrackedUrl { Url = "/old", RelativePath = "/old", Domain = string.Empty, Culture = string.Empty };

        RedirectResult first = await provider.CreateAsync(row, Payload(OtherPageKey), CancellationToken.None);
        RedirectResult second = await provider.CreateAsync(row, Payload(ConfiguredErrorPageKey), CancellationToken.None);

        var redirects = this.GetRequiredService<IRedirectUrlService>();
        var mostRecent = redirects.GetMostRecentRedirectUrl("/old");
        using var response = await this.GetAsync("/old");
        Assert.Multiple(() =>
        {
            Assert.That(first.Succeeded, Is.True);
            Assert.That(second.Succeeded, Is.True);
            Assert.That(mostRecent?.ContentKey, Is.EqualTo(ConfiguredErrorPageKey));
            Assert.That(redirects.GetContentRedirectUrls(OtherPageKey), Is.Empty);
            Assert.That(redirects.GetContentRedirectUrls(ConfiguredErrorPageKey).Count(), Is.EqualTo(1));
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.MovedPermanently));
            Assert.That(response.Headers.Location?.ToString(), Does.Contain("configured-error"));
        });
    }

    /// <summary>
    /// Tests that a record without a domain registers the bare route, which Umbraco applies without a domain.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task No_domain_record_uses_the_bare_route()
    {
        var provider = this.GetRequiredService<IRedirectProvider>();
        var row = new TrackedUrl { Url = "/old-bare", RelativePath = "/old-bare", Domain = string.Empty, Culture = string.Empty };

        RedirectResult result = await provider.CreateAsync(row, Payload(OtherPageKey), CancellationToken.None);
        using var response = await this.GetAsync("/old-bare");

        Assert.Multiple(() =>
        {
            Assert.That(result.Succeeded, Is.True, result.Message);
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.MovedPermanently));
        });
    }

    private static JsonElement Payload(Guid key) =>
        JsonDocument.Parse($"{{\"targetKey\":\"{key}\"}}").RootElement.Clone();

    private void AssignDomain(string name, string isoCode)
    {
        var home = this.GetRequiredService<IContentService>().GetRootContent().Single();
        var model = new DomainsUpdateModel { Domains = [new DomainModel { DomainName = name, IsoCode = isoCode }] };
        var updated = this.GetRequiredService<IDomainService>().UpdateDomainsAsync(home.Key, model).GetAwaiter().GetResult();
        Assert.That(updated.Success, Is.True, $"Assigning the domain failed: {updated.Status}.");
    }
}
