// Copyright (c) Dave Woestenborghs and contributors. Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.DependencyInjection;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.PublishedCache;
using Umbraco.Cms.Core.Routing;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Tests.Common.Testing;
using Umbraco.Community.PageNotFoundTracker.Core.NotFound;

namespace Umbraco.Community.PageNotFoundTracker.Integration.Tests;

/// <summary>
/// Tests how the file path renders special 404 pages, compared with the page path: link generation, a hijacked
/// controller with <c>[Authorize]</c>, and a 404 page under Umbraco public access protection. A resolver picks the
/// page by request path.
/// </summary>
[TestFixture]
[UmbracoTest(Database = UmbracoTestOptions.Database.NewSchemaPerTest, Boot = true)]
internal sealed class FileNotFoundRenderingTests : PageNotFoundTestBase
{
    private const string LinkPageTitle = "The link page";

    private const string SecurePageTitle = "The secure page";

    private const string ProtectedPageTitle = "The protected 404 page";

    private static readonly Guid LinkPageKey = new("5f0b5a0e-0e47-4f0c-9d0f-3b5f1c0a7b01");

    private static readonly Guid SecurePageKey = new("5f0b5a0e-0e47-4f0c-9d0f-3b5f1c0a7b02");

    private static readonly Guid ProtectedPageKey = new("5f0b5a0e-0e47-4f0c-9d0f-3b5f1c0a7b03");

    private static readonly Dictionary<string, Guid> PagesByPath = new(StringComparer.OrdinalIgnoreCase)
    {
        ["/link.pdf"] = LinkPageKey,
        ["/link-page-miss"] = LinkPageKey,
        ["/secure.pdf"] = SecurePageKey,
        ["/protected.pdf"] = ProtectedPageKey,
        ["/protected-page-miss"] = ProtectedPageKey,
    };

    /// <inheritdoc />
    public override void Setup()
    {
        base.Setup();
        this.CreatePages();
    }

    /// <summary>
    /// Tests that a 404 page generating URLs (<c>Url.Action</c>) renders on both paths.
    /// </summary>
    /// <param name="path">The missing file or page.</param>
    /// <returns>A task.</returns>
    [TestCase("/link.pdf")]
    [TestCase("/link-page-miss")]
    public async Task NotFound_Page_With_Link_Generation_Renders(string path)
    {
        using var response = await this.GetAsync(path);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
            Assert.That(body, Does.Contain(LinkPageController.LinkMarker));
        });
    }

    /// <summary>
    /// Tests that a 404 page whose hijacked controller requires an authenticated visitor is not shown to an anonymous
    /// visitor of a missing file: the plain 404 is returned, recorded once.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task NotFound_Page_With_Authorize_Is_Not_Shown_To_Anonymous_Visitor()
    {
        using var response = await this.GetAsync("/secure.pdf");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
            Assert.That(body, Does.Not.Contain(SecurePageTitle));
            Assert.That(this.Recorder.Occurrences, Has.Count.EqualTo(1));
        });
    }

    /// <summary>
    /// Tests that a protected 404 page is handled by Umbraco's public access on both paths: an anonymous visitor gets
    /// the login page of the protection, never the protected page.
    /// </summary>
    /// <param name="path">The missing file or page.</param>
    /// <param name="expectedStatus">
    /// The status: the file path keeps 404; on the page path Umbraco's public access rewrite builds a new published
    /// request for the login page, which drops the 404 status, so Umbraco answers 200.
    /// </param>
    /// <returns>A task.</returns>
    [TestCase("/protected.pdf", HttpStatusCode.NotFound)]
    [TestCase("/protected-page-miss", HttpStatusCode.OK)]
    public async Task Protected_NotFound_Page_Shows_The_Login_Page(string path, HttpStatusCode expectedStatus)
    {
        using var response = await this.GetAsync(path);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(expectedStatus));
            Assert.That(body, Does.Not.Contain(ProtectedPageTitle));
            Assert.That(body, Does.Contain(OtherPageTitle));
            Assert.That(this.Recorder.Occurrences, Has.Count.EqualTo(1));
        });
    }

    /// <inheritdoc />
    protected override void CustomTestSetup(IUmbracoBuilder builder)
    {
        base.CustomTestSetup(builder);
        builder.Services.AddSingleton<INotFoundPageResolver, PathResolver>();
    }

    private void CreatePages()
    {
        var contentTypeService = this.GetRequiredService<IContentTypeService>();
        var contentService = this.GetRequiredService<IContentService>();
        var linkPage = BuildContentType("linkPage", "Link page");
        var securePage = BuildContentType("securePage", "Secure page");
        Assert.Multiple(() =>
        {
            Assert.That(contentTypeService.CreateAsync(linkPage, Constants.Security.SuperUserKey).GetAwaiter().GetResult().Success, Is.True);
            Assert.That(contentTypeService.CreateAsync(securePage, Constants.Security.SuperUserKey).GetAwaiter().GetResult().Success, Is.True);
        });

        var home = contentService.GetRootContent().Single();
        this.CreatePublishedPage(linkPage, "Link", LinkPageTitle, home, LinkPageKey);
        this.CreatePublishedPage(securePage, "Secure", SecurePageTitle, home, SecurePageKey);
        var protectedPage = this.CreatePublishedPage(contentTypeService.Get(NotFoundPageAlias)!, "Protected", ProtectedPageTitle, home, ProtectedPageKey);

        var login = contentService.GetById(OtherPageKey)!;
        var noAccess = contentService.GetById(ConfiguredErrorPageKey)!;
        var rule = new PublicAccessRule { RuleType = Constants.Conventions.PublicAccess.MemberRoleRuleType, RuleValue = "Members" };
        var saved = this.GetRequiredService<IPublicAccessService>().Save(new PublicAccessEntry(protectedPage, login, noAccess, [rule]));
        Assert.That(saved.Success, Is.True, "Protecting the page failed.");
    }

    private sealed class PathResolver : INotFoundPageResolver
    {
        private readonly IPublishedContentCache contentCache;

        public PathResolver(IPublishedContentCache contentCache) => this.contentCache = contentCache;

        public async Task<NotFoundPage?> ResolveAsync(NotFoundRequestContext context, Domain? domain, CancellationToken cancellationToken)
        {
            if (!PagesByPath.TryGetValue(context.Path, out var key))
            {
                return null;
            }

            var content = await this.contentCache.GetByIdAsync(key, false);
            return content is null ? null : new NotFoundPage(content, null);
        }
    }
}
