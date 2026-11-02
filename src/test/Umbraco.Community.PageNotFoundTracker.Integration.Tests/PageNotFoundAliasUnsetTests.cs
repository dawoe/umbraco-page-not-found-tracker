// Copyright (c) Dave Woestenborghs and contributors. Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Umbraco.Cms.Core.DependencyInjection;
using Umbraco.Cms.Tests.Common.Testing;
using Umbraco.Community.PageNotFoundTracker.Core.Configuration;

namespace Umbraco.Community.PageNotFoundTracker.Integration.Tests;

/// <summary>
/// Tests that with the alias unset and no configured 404 page, Umbraco's default 404 answer is unchanged.
/// </summary>
[TestFixture]
[UmbracoTest(Database = UmbracoTestOptions.Database.NewSchemaPerTest, Boot = true)]
internal sealed class PageNotFoundAliasUnsetTests : PageNotFoundTestBase
{
    /// <inheritdoc />
    protected override void CustomTestSetup(IUmbracoBuilder builder)
    {
        base.CustomTestSetup(builder);
        builder.Services.PostConfigure<PageNotFoundTrackerSettings>(settings => settings.NotFoundPageDocumentTypeAlias = string.Empty);
    }

    /// <summary>
    /// Tests that a missing page gets Umbraco's default 404 answer and is recorded without a resolved page.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task Missing_Page_Returns_Umbraco_Default_404()
    {
        using var response = await this.GetAsync(MissingPath);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));

            // The heading of Umbraco's own NotFound view (~/umbraco/UmbracoWebsite/NotFound.cshtml).
            Assert.That(body, Does.Contain("<h1>Page Not Found</h1>"));
            Assert.That(body, Does.Not.Contain(NotFoundPageTitle));
            Assert.That(this.Recorder.Occurrences, Has.Count.EqualTo(1));
        });
        Assert.That(this.Recorder.Occurrences[0].PageResolved, Is.False);
    }
}
