// Copyright (c) Dave Woestenborghs and contributors. Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Umbraco.Cms.Core.Configuration.Models;
using Umbraco.Cms.Core.DependencyInjection;
using Umbraco.Cms.Tests.Common.Testing;

namespace Umbraco.Community.PageNotFoundTracker.Integration.Tests;

/// <summary>
/// Tests the 404 handling for pages with the default resolver and the alias from appsettings.Tests.json. Umbraco's own
/// configured 404 page is also set, to prove the package's last-chance step runs first.
/// </summary>
[TestFixture]
[UmbracoTest(Database = UmbracoTestOptions.Database.NewSchemaPerTest, Boot = true)]
internal sealed class PageNotFoundTests : PageNotFoundTestBase
{
    /// <inheritdoc />
    protected override void CustomTestSetup(IUmbracoBuilder builder)
    {
        base.CustomTestSetup(builder);
        builder.Services.Configure<ContentSettings>(settings =>
            settings.Error404Collection = [new ContentErrorPage { ContentKey = ConfiguredErrorPageKey, Culture = "default" }]);
    }

    /// <summary>
    /// Tests that a missing page is answered with the 404 page and status 404, ahead of Umbraco's configured 404 page.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task Missing_Page_Returns_404_With_NotFound_Page_Content()
    {
        using var response = await this.GetAsync(MissingPath);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
            Assert.That(body, Does.Contain(NotFoundPageTitle));
            Assert.That(body, Does.Not.Contain(ConfiguredErrorPageTitle));
        });
    }

    /// <summary>
    /// Tests that the visitor's URL stays the original one: no redirect.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task Missing_Page_Keeps_The_Original_Url()
    {
        using var response = await this.GetAsync(MissingPath + "?q=1");

        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
            Assert.That(response.Headers.Location, Is.Null);
            Assert.That(response.RequestMessage?.RequestUri?.PathAndQuery, Is.EqualTo(MissingPath + "?q=1"));
        });
    }

    /// <summary>
    /// Tests that a missing page is recorded exactly once with the original URL, as a page, with the page resolved.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task Missing_Page_Is_Recorded_Once()
    {
        using var response = await this.GetAsync("/Does-Not-Exist?q=1");

        Assert.That(this.Recorder.Occurrences, Has.Count.EqualTo(1));
        var occurrence = this.Recorder.Occurrences[0];
        Assert.Multiple(() =>
        {
            Assert.That(occurrence.PathAndQuery, Is.EqualTo("/Does-Not-Exist?q=1"));
            Assert.That(occurrence.IsFile, Is.False);
            Assert.That(occurrence.PageResolved, Is.True);
            Assert.That(occurrence.Domain, Is.Null);
            Assert.That(occurrence.Culture, Is.Null);
        });
    }

    /// <summary>
    /// Tests that requesting the 404 page's own URL renders it with status 200 and records nothing.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task NotFound_Page_Url_Returns_200()
    {
        using var response = await this.GetAsync("/not-found/");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(body, Does.Contain(NotFoundPageTitle));
            Assert.That(this.Recorder.Occurrences, Is.Empty);
        });
    }

    /// <summary>
    /// Tests that an existing page is unaffected.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task Existing_Page_Returns_200()
    {
        using var response = await this.GetAsync("/about/");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(body, Does.Contain(AboutPageTitle));
            Assert.That(this.Recorder.Occurrences, Is.Empty);
        });
    }

    /// <summary>
    /// Tests that a missing backoffice path is left to Umbraco: no 404 page, nothing recorded.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task Missing_Backoffice_Path_Is_Unaffected()
    {
        using var response = await this.GetAsync("/umbraco/does-not-exist");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Multiple(() =>
        {
            Assert.That(body, Does.Not.Contain(NotFoundPageTitle));
            Assert.That(this.Recorder.Occurrences, Is.Empty);
        });
    }
}
