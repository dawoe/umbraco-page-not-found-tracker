// Copyright (c) Dave Woestenborghs and contributors. Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Umbraco.Cms.Core.DependencyInjection;
using Umbraco.Cms.Tests.Common.Testing;
using Umbraco.Community.PageNotFoundTracker.Core.Configuration;

namespace Umbraco.Community.PageNotFoundTracker.Integration.Tests;

/// <summary>
/// Tests a 404 page that Umbraco cannot render (no template, no hijacked controller). Umbraco then runs the
/// last-chance finder a second time for the same request; the occurrence must still be recorded once.
/// </summary>
[TestFixture]
[UmbracoTest(Database = UmbracoTestOptions.Database.NewSchemaPerTest, Boot = true)]
internal sealed class PageNotFoundPageWithoutTemplateTests : PageNotFoundTestBase
{
    /// <inheritdoc />
    protected override void CustomTestSetup(IUmbracoBuilder builder)
    {
        base.CustomTestSetup(builder);
        builder.Services.PostConfigure<PageNotFoundTrackerSettings>(settings => settings.NotFoundPageDocumentTypeAlias = PlainPageAlias);
    }

    /// <summary>
    /// Tests that the response is a 404 and the occurrence is recorded exactly once.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task Missing_Page_Is_Recorded_Once_When_The_404_Page_Has_No_Template()
    {
        using var response = await this.GetAsync(MissingPath);

        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
            Assert.That(this.Recorder.Occurrences, Has.Count.EqualTo(1));
        });
        Assert.That(this.Recorder.Occurrences[0].PageResolved, Is.True);
    }

    /// <summary>
    /// Tests the file path with that 404 page: the file middleware renders it through Umbraco, which runs the
    /// last-chance finder again. The answer is still a 404 (never a 500) and the request is recorded once, as a file.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task Missing_File_Is_Recorded_Once_When_The_404_Page_Has_No_Template()
    {
        using var response = await this.GetAsync("/missing.pdf");

        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
            Assert.That(this.Recorder.Occurrences, Has.Count.EqualTo(1));
        });
        Assert.That(this.Recorder.Occurrences[0].IsFile, Is.True);
    }
}
