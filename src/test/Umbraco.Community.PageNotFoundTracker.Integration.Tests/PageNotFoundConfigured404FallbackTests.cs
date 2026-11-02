// Copyright (c) Dave Woestenborghs and contributors. Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Umbraco.Cms.Core.Configuration.Models;
using Umbraco.Cms.Core.DependencyInjection;
using Umbraco.Cms.Tests.Common.Testing;
using Umbraco.Community.PageNotFoundTracker.Core.Configuration;

namespace Umbraco.Community.PageNotFoundTracker.Integration.Tests;

/// <summary>
/// Tests that when the package resolves nothing, Umbraco's own configured 404 page (<c>Error404Collection</c>) still works.
/// </summary>
[TestFixture]
[UmbracoTest(Database = UmbracoTestOptions.Database.NewSchemaPerTest, Boot = true)]
internal sealed class PageNotFoundConfigured404FallbackTests : PageNotFoundTestBase
{
    /// <inheritdoc />
    protected override void CustomTestSetup(IUmbracoBuilder builder)
    {
        base.CustomTestSetup(builder);
        builder.Services.PostConfigure<PageNotFoundTrackerSettings>(settings => settings.NotFoundPageDocumentTypeAlias = string.Empty);
        builder.Services.Configure<ContentSettings>(settings =>
            settings.Error404Collection = [new ContentErrorPage { ContentKey = ConfiguredErrorPageKey, Culture = "default" }]);
    }

    /// <summary>
    /// Tests that a missing page shows Umbraco's configured 404 page with status 404, recorded without a resolved page.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task Missing_Page_Falls_Back_To_Umbraco_Configured_404()
    {
        using var response = await this.GetAsync(MissingPath);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
            Assert.That(body, Does.Contain(ConfiguredErrorPageTitle));
            Assert.That(body, Does.Not.Contain(NotFoundPageTitle));
            Assert.That(this.Recorder.Occurrences, Has.Count.EqualTo(1));
        });
        Assert.That(this.Recorder.Occurrences[0].PageResolved, Is.False);
    }
}
