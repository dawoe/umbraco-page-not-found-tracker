// Copyright (c) Dave Woestenborghs and contributors. Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Net;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Cache;
using Umbraco.Cms.Core.DependencyInjection;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Models.ContentEditing;
using Umbraco.Cms.Core.Notifications;
using Umbraco.Cms.Core.Routing;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Tests.Common.Builders;
using Umbraco.Cms.Tests.Common.Builders.Extensions;
using Umbraco.Cms.Tests.Common.Testing;
using Umbraco.Community.PageNotFoundTracker.Core.Configuration;
using Umbraco.Community.PageNotFoundTracker.Core.NotFound;
using Umbraco.Community.PageNotFoundTracker.Infrastructure.NotFound;

namespace Umbraco.Community.PageNotFoundTracker.Integration.Tests;

/// <summary>
/// Tests that a missing file inside a culture domain gets the 404 page in that domain's culture. Home gets the domains
/// <c>localhost/en</c> (en-US) and <c>localhost/nl</c> (nl-NL), and a culture-variant 404 page is the configured one.
/// </summary>
[TestFixture]
[UmbracoTest(Database = UmbracoTestOptions.Database.NewSchemaPerTest, Boot = true)]
internal sealed class FileNotFoundCultureDomainTests : PageNotFoundTestBase
{
    private const string VariantNotFoundPageAlias = "variantNotFoundPage";

    private const string EnglishTitle = "This page could not be found";

    private const string DutchTitle = "Deze pagina bestaat niet";

    /// <inheritdoc />
    public override void Setup()
    {
        base.Setup();
        this.CreateCultureSite();
    }

    /// <summary>
    /// Tests that the 404 page of the domain's culture answers a missing file in each culture domain.
    /// </summary>
    /// <param name="path">The missing file.</param>
    /// <param name="culture">The culture of the domain.</param>
    /// <param name="expectedTitle">The title of the 404 page in that culture.</param>
    /// <param name="otherTitle">The title of the 404 page in the other culture.</param>
    /// <returns>A task.</returns>
    [TestCase("/nl/missing.pdf", "nl-NL", DutchTitle, EnglishTitle)]
    [TestCase("/en/missing.pdf", "en-US", EnglishTitle, DutchTitle)]
    public async Task Missing_File_In_Culture_Domain_Gets_The_Domain_Culture_Variant(string path, string culture, string expectedTitle, string otherTitle)
    {
        using var response = await this.GetAsync(path);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
            Assert.That(body, Does.Contain(expectedTitle));
            Assert.That(body, Does.Not.Contain(otherTitle));
            Assert.That(this.Recorder.Occurrences, Has.Count.EqualTo(1));
        });
        Assert.Multiple(() =>
        {
            Assert.That(this.Recorder.Occurrences[0].Culture, Is.EqualTo(culture));
            Assert.That(this.Recorder.Occurrences[0].Domain, Is.Not.Null);
            Assert.That(this.Recorder.Occurrences[0].IsFile, Is.True);
        });
    }

    /// <summary>
    /// Tests the page path in the same setup, so both code paths are proven to render the domain culture variant.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task Missing_Page_In_Culture_Domain_Gets_The_Domain_Culture_Variant()
    {
        using var response = await this.GetAsync("/nl/does-not-exist");
        var body = await response.Content.ReadAsStringAsync();
        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
            Assert.That(body, Does.Contain(DutchTitle));
            Assert.That(body, Does.Not.Contain(EnglishTitle));
            Assert.That(this.Recorder.Occurrences, Has.Count.EqualTo(1));
        });
        Assert.That(this.Recorder.Occurrences[0].Culture, Is.EqualTo("nl-NL"));
    }

    /// <summary>
    /// Tests that the culture a resolver returns is rendered on the file path even when no domain matches (Umbraco's
    /// routing then sets its default language on the published request).
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task Missing_File_Without_Domain_Renders_The_Resolver_Culture()
    {
        using var response = await this.GetAsync(CultureForcingResolver.ForcedDutchPath);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
            Assert.That(body, Does.Contain(DutchTitle));
            Assert.That(body, Does.Not.Contain(EnglishTitle));
            Assert.That(this.Recorder.Occurrences, Has.Count.EqualTo(1));
        });
        Assert.That(this.Recorder.Occurrences[0].Domain, Is.Null);
    }

    /// <inheritdoc />
    protected override void CustomTestSetup(IUmbracoBuilder builder)
    {
        base.CustomTestSetup(builder);
        builder.Services.PostConfigure<PageNotFoundTrackerSettings>(settings => settings.NotFoundPageDocumentTypeAlias = VariantNotFoundPageAlias);
        builder.Services.AddSingleton<INotFoundPageResolver>(services =>
            new CultureForcingResolver(ActivatorUtilities.CreateInstance<DefaultNotFoundPageResolver>(services)));

        // The test base boots with AppCaches.NoCache, whose request cache is never available. Umbraco's variation context
        // accessor then falls back to an AsyncLocal, and the culture that routing sets inside an awaited call is lost
        // before the page renders. A real site uses HttpContextRequestAppCache, so this test does too.
        builder.Services.AddUnique<IRequestCache>(services => new HttpContextRequestAppCache(services.GetRequiredService<IHttpContextAccessor>()));

        // Domains and languages must reach the caches that domain matching and routing read.
        builder.AddNotificationHandler<DomainSavedNotification, DomainSavedDistributedCacheNotificationHandler>();
        builder.AddNotificationHandler<LanguageSavedNotification, LanguageSavedDistributedCacheNotificationHandler>();
    }

    private void CreateCultureSite()
    {
        var languageService = this.GetRequiredService<ILanguageService>();
        var language = languageService.CreateAsync(new Language("nl-NL", "Dutch (Netherlands)"), Constants.Security.SuperUserKey).GetAwaiter().GetResult();
        Assert.That(language.Success, Is.True, "Creating nl-NL failed.");

        var contentType = new ContentTypeBuilder()
            .WithAlias(VariantNotFoundPageAlias)
            .WithName("Variant not found page")
            .WithContentVariation(ContentVariation.Culture)
            .AddPropertyGroup()
                .WithAlias("content")
                .WithName("Content")
                .WithSupportsPublishing(true)
                .AddPropertyType()
                    .WithAlias(TitleAlias)
                    .WithName("Title")
                    .WithVariations(ContentVariation.Culture)
                    .Done()
                .Done()
            .Build();
        var created = this.GetRequiredService<IContentTypeService>().CreateAsync(contentType, Constants.Security.SuperUserKey).GetAwaiter().GetResult();
        Assert.That(created.Success, Is.True, "Creating the variant 404 type failed.");

        var contentService = this.GetRequiredService<IContentService>();
        var home = contentService.GetRootContent().Single();

        var page = new ContentBuilder()
            .WithContentType(contentType)
            .WithParent(home)
            .WithCultureName("en-US", "Not found (variant)")
            .WithCultureName("nl-NL", "Niet gevonden")
            .Build();
        page.SetValue(TitleAlias, EnglishTitle, "en-US");
        page.SetValue(TitleAlias, DutchTitle, "nl-NL");
        Assert.Multiple(() =>
        {
            Assert.That(contentService.Save(page).Success, Is.True, "Saving the variant 404 page failed.");
            Assert.That(contentService.Publish(page, ["*"]).Success, Is.True, "Publishing the variant 404 page failed.");
        });

        var domains = new DomainsUpdateModel
        {
            Domains =
            [
                new DomainModel { DomainName = "localhost/en", IsoCode = "en-US" },
                new DomainModel { DomainName = "localhost/nl", IsoCode = "nl-NL" },
            ],
        };
        var updated = this.GetRequiredService<IDomainService>().UpdateDomainsAsync(home.Key, domains).GetAwaiter().GetResult();
        Assert.That(updated.Success, Is.True, $"Assigning the domains failed: {updated.Status}.");
    }

    /// <summary>
    /// The default resolver, except that <see cref="ForcedDutchPath"/> gets the Dutch variant without a domain.
    /// </summary>
    private sealed class CultureForcingResolver : INotFoundPageResolver
    {
        public const string ForcedDutchPath = "/forced-dutch.pdf";

        private readonly INotFoundPageResolver inner;

        public CultureForcingResolver(INotFoundPageResolver inner) => this.inner = inner;

        public async Task<NotFoundPage?> ResolveAsync(NotFoundRequestContext context, Domain? domain, CancellationToken cancellationToken)
        {
            var page = await this.inner.ResolveAsync(context, domain, cancellationToken);
            return page is not null && string.Equals(context.Path, ForcedDutchPath, StringComparison.OrdinalIgnoreCase)
                ? page with { Culture = "nl-NL" }
                : page;
        }
    }
}
