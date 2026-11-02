// Copyright (c) Dave Woestenborghs and contributors. Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Umbraco.Cms.Core.DependencyInjection;
using Umbraco.Cms.Tests.Common.Testing;
using Umbraco.Community.PageNotFoundTracker.Core.Configuration;

namespace Umbraco.Community.PageNotFoundTracker.Integration.Tests;

/// <summary>
/// Tests the 404 handling for files with the 404 page alias unset: the original 404 passes through.
/// </summary>
[TestFixture]
[UmbracoTest(Database = UmbracoTestOptions.Database.NewSchemaPerTest, Boot = true)]
internal sealed class FileNotFoundAliasUnsetTests : PageNotFoundTestBase
{
    /// <inheritdoc />
    protected override void CustomTestSetup(IUmbracoBuilder builder)
    {
        base.CustomTestSetup(builder);
        builder.Services.PostConfigure<PageNotFoundTrackerSettings>(settings => settings.NotFoundPageDocumentTypeAlias = string.Empty);
    }

    /// <summary>
    /// Tests that a missing file gets the plain 404 and is recorded without a resolved page.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task Missing_File_Returns_Plain_404()
    {
        using var response = await this.GetAsync("/missing.pdf");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
            Assert.That(body, Is.Empty);
            Assert.That(this.Recorder.Occurrences, Has.Count.EqualTo(1));
        });
        Assert.Multiple(() =>
        {
            Assert.That(this.Recorder.Occurrences[0].IsFile, Is.True);
            Assert.That(this.Recorder.Occurrences[0].PageResolved, Is.False);
        });
    }
}
