// Copyright (c) Dave Woestenborghs and contributors. Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Reflection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Cache;
using Umbraco.Cms.Core.DependencyInjection;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Notifications;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core.Sync;
using Umbraco.Cms.Tests.Common.Builders;
using Umbraco.Cms.Tests.Common.Builders.Extensions;
using Umbraco.Cms.Tests.Integration.TestServerTest;
using Umbraco.Community.PageNotFoundTracker.Infrastructure.NotFound;
using Umbraco.Extensions;

namespace Umbraco.Community.PageNotFoundTracker.Integration.Tests;

/// <summary>
/// Base for the web integration tests of the 404 handling for pages. Boots Umbraco in a test server with the package
/// composed in, captures the 404 signal with <see cref="CapturingNotFoundRecorder"/> and publishes a small site:
/// Home (root) with the children About, Not found (the 404 page), Other, Configured error and Plain (no template).
/// </summary>
/// <remarks>
/// The pages render through hijacked controllers (<see cref="TextPageController"/>, <see cref="NotFoundPageController"/>)
/// instead of Razor views, because the test server compiles no views. The controllers are normal Umbraco render
/// controllers, so the request goes through the full Umbraco routing and rendering pipeline.
/// </remarks>
internal abstract class PageNotFoundTestBase : UmbracoTestServerTestBase
{
    /// <summary>
    /// The alias of the title property on both test document types.
    /// </summary>
    public const string TitleAlias = "title";

    /// <summary>
    /// The alias of the 404 page document type, as configured in appsettings.Tests.json.
    /// </summary>
    protected const string NotFoundPageAlias = "notFoundPage";

    /// <summary>
    /// The alias of the ordinary page document type.
    /// </summary>
    protected const string TextPageAlias = "textPage";

    /// <summary>
    /// The alias of a document type with neither a template nor a hijacked controller, so Umbraco cannot render it.
    /// </summary>
    protected const string PlainPageAlias = "plainPage";

    /// <summary>
    /// The title of the Plain page.
    /// </summary>
    protected const string PlainPageTitle = "The plain page";

    /// <summary>
    /// The title of the 404 page.
    /// </summary>
    protected const string NotFoundPageTitle = "Sorry, this page does not exist";

    /// <summary>
    /// The title of the Other page.
    /// </summary>
    protected const string OtherPageTitle = "The other page";

    /// <summary>
    /// The title of the Configured error page.
    /// </summary>
    protected const string ConfiguredErrorPageTitle = "The configured error page";

    /// <summary>
    /// The title of the About page.
    /// </summary>
    protected const string AboutPageTitle = "All about us";

    /// <summary>
    /// A path that matches no page.
    /// </summary>
    protected const string MissingPath = "/does-not-exist";

    /// <summary>
    /// The key of the Other page.
    /// </summary>
    protected static readonly Guid OtherPageKey = new("5f0b5a0e-0e47-4f0c-9d0f-3b5f1c0a7a01");

    /// <summary>
    /// The key of the Configured error page.
    /// </summary>
    protected static readonly Guid ConfiguredErrorPageKey = new("5f0b5a0e-0e47-4f0c-9d0f-3b5f1c0a7a02");

    /// <summary>
    /// Gets the recorder that captures the 404 signal of the current test.
    /// </summary>
    protected CapturingNotFoundRecorder Recorder { get; private set; } = new();

    /// <inheritdoc />
    public override void Setup()
    {
        this.Recorder = new CapturingNotFoundRecorder();
        base.Setup();
        this.CreateSite();
    }

    /// <inheritdoc />
    protected override void CustomTestSetup(IUmbracoBuilder builder)
    {
        builder.Services.AddUnique<INotFoundRecorder>(this.Recorder);

        // The file 404 middleware, placed where UsePageNotFoundTracker() puts it on a real site.
        builder.Services.AddTransient<IStartupFilter, PageNotFoundTrackerStartupFilter>();

        // Publishing must refresh the caches that front-end routing reads (see LocalServerMessenger).
        builder.Services.AddUnique<IServerMessenger, LocalServerMessenger>();
        builder.AddNotificationHandler<ContentTreeChangeNotification, ContentTreeChangeDistributedCacheNotificationHandler>();
        builder.AddNotificationHandler<ContentTypeChangedNotification, ContentTypeChangedDistributedCacheNotificationHandler>();
    }

    /// <inheritdoc />
    protected override void CustomMvcSetup(IMvcBuilder mvcBuilder)
    {
        // Makes the hijacked render controllers of this assembly discoverable.
        mvcBuilder.AddApplicationPart(typeof(PageNotFoundTestBase).Assembly);

        // Umbraco's default 404 view (~/umbraco/UmbracoWebsite/NotFound.cshtml) is a compiled view in this assembly. A
        // real site gets it through the Umbraco.Cms package; the test server does not register it by itself.
        mvcBuilder.AddApplicationPart(Assembly.Load("Umbraco.Cms.StaticAssets"));
    }

    /// <summary>
    /// Requests a path from the test server.
    /// </summary>
    /// <param name="pathAndQuery">The path and query to request.</param>
    /// <returns>The response.</returns>
    protected Task<HttpResponseMessage> GetAsync(string pathAndQuery) => this.Client.GetAsync(new Uri(pathAndQuery, UriKind.Relative));

    /// <summary>
    /// Sends a request with the given method to the test server.
    /// </summary>
    /// <param name="method">The HTTP method.</param>
    /// <param name="pathAndQuery">The path and query to request.</param>
    /// <returns>The response.</returns>
    protected async Task<HttpResponseMessage> SendAsync(HttpMethod method, string pathAndQuery)
    {
        using var request = new HttpRequestMessage(method, new Uri(pathAndQuery, UriKind.Relative));
        return await this.Client.SendAsync(request);
    }

    /// <summary>
    /// Builds a document type with a title property.
    /// </summary>
    /// <param name="alias">The alias.</param>
    /// <param name="name">The name.</param>
    /// <returns>The document type.</returns>
    protected static IContentType BuildContentType(string alias, string name) =>
        new ContentTypeBuilder()
            .WithAlias(alias)
            .WithName(name)
            .AddPropertyGroup()
                .WithAlias("content")
                .WithName("Content")
                .WithSupportsPublishing(true)
                .AddPropertyType()
                    .WithAlias(TitleAlias)
                    .WithName("Title")
                    .Done()
                .Done()
            .Build();

    private void CreateSite()
    {
        var contentTypeService = this.GetRequiredService<IContentTypeService>();
        var textPage = BuildContentType(TextPageAlias, "Text page");
        var notFoundPage = BuildContentType(NotFoundPageAlias, "Not found page");
        var plainPage = BuildContentType(PlainPageAlias, "Plain page");
        Assert.Multiple(() =>
        {
            Assert.That(contentTypeService.CreateAsync(textPage, Constants.Security.SuperUserKey).GetAwaiter().GetResult().Success, Is.True);
            Assert.That(contentTypeService.CreateAsync(notFoundPage, Constants.Security.SuperUserKey).GetAwaiter().GetResult().Success, Is.True);
            Assert.That(contentTypeService.CreateAsync(plainPage, Constants.Security.SuperUserKey).GetAwaiter().GetResult().Success, Is.True);
        });

        var home = this.CreatePublishedPage(textPage, "Home", "Welcome home", null, null);
        this.CreatePublishedPage(textPage, "About", AboutPageTitle, home, null);
        this.CreatePublishedPage(notFoundPage, "Not found", NotFoundPageTitle, home, null);
        this.CreatePublishedPage(textPage, "Other", OtherPageTitle, home, OtherPageKey);
        this.CreatePublishedPage(textPage, "Configured error", ConfiguredErrorPageTitle, home, ConfiguredErrorPageKey);
        this.CreatePublishedPage(plainPage, "Plain", PlainPageTitle, home, null);
    }

    /// <summary>
    /// Creates and publishes a page with a title.
    /// </summary>
    /// <param name="contentType">The document type.</param>
    /// <param name="name">The name.</param>
    /// <param name="title">The title.</param>
    /// <param name="parent">The parent, or <c>null</c> for a root node.</param>
    /// <param name="key">The key, or <c>null</c> for a generated one.</param>
    /// <returns>The page.</returns>
    protected IContent CreatePublishedPage(IContentType contentType, string name, string title, IContent? parent, Guid? key)
    {
        var contentService = this.GetRequiredService<IContentService>();
        var builder = new ContentBuilder().WithContentType(contentType).WithName(name);
        builder = parent is null ? builder.WithParentId(Constants.System.Root) : builder.WithParent(parent);
        if (key is not null)
        {
            builder = builder.WithKey(key.Value);
        }

        var content = builder.Build();
        content.SetValue(TitleAlias, title);

        var saved = contentService.Save(content);
        var published = contentService.Publish(content, ["*"]);
        Assert.Multiple(() =>
        {
            Assert.That(saved.Success, Is.True, $"Saving {name} failed.");
            Assert.That(published.Success, Is.True, $"Publishing {name} failed.");
        });

        return content;
    }
}
