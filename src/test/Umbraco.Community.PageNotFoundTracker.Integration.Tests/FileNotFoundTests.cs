// Copyright (c) Dave Woestenborghs and contributors. Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Net;
using Umbraco.Cms.Tests.Common.Testing;

namespace Umbraco.Community.PageNotFoundTracker.Integration.Tests;

/// <summary>
/// Tests the 404 handling for files (the file middleware) with the default resolver and the alias from
/// appsettings.Tests.json.
/// </summary>
[TestFixture]
[UmbracoTest(Database = UmbracoTestOptions.Database.NewSchemaPerTest, Boot = true)]
internal sealed class FileNotFoundTests : PageNotFoundTestBase
{
    private const string ExistingFileName = "page-not-found-tracker-existing-file.txt";

    private const string ExistingFileContent = "This file exists.";

    /// <summary>
    /// Tests that a GET for a missing file is answered with the 404 page, status 404 and the original URL.
    /// </summary>
    /// <param name="pathAndQuery">The missing file.</param>
    /// <returns>A task.</returns>
    [TestCase("/missing.pdf")]
    [TestCase("/media/missing.png?x=1")]
    [TestCase("/a/missing.min.js")]
    public async Task Get_Missing_File_Returns_404_With_NotFound_Page_Content(string pathAndQuery)
    {
        using var response = await this.GetAsync(pathAndQuery);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
            Assert.That(body, Does.Contain(NotFoundPageTitle));
            Assert.That(response.Content.Headers.ContentType?.MediaType, Is.EqualTo("text/html"));
            Assert.That(response.Headers.Location, Is.Null);
            Assert.That(response.RequestMessage?.RequestUri?.PathAndQuery, Is.EqualTo(pathAndQuery));
        });
    }

    /// <summary>
    /// Tests that a HEAD for a missing file gets status 404 and the 404 page's headers, without a body.
    /// </summary>
    /// <param name="pathAndQuery">The missing file.</param>
    /// <returns>A task.</returns>
    [TestCase("/missing.pdf")]
    [TestCase("/media/missing.png?x=1")]
    [TestCase("/a/missing.min.js")]
    public async Task Head_Missing_File_Returns_404_Without_Body(string pathAndQuery)
    {
        using var response = await this.SendAsync(HttpMethod.Head, pathAndQuery);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
            Assert.That(body, Is.Empty);
            Assert.That(response.Content.Headers.ContentType?.MediaType, Is.EqualTo("text/html"));
            Assert.That(this.Recorder.Occurrences, Has.Count.EqualTo(1));
        });
    }

    /// <summary>
    /// Tests that a missing file is recorded exactly once, as a file, with the original URL and the page resolved.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task Missing_File_Is_Recorded_Once_As_File()
    {
        using var response = await this.GetAsync("/Media/Missing.PNG?x=1");

        Assert.That(this.Recorder.Occurrences, Has.Count.EqualTo(1));
        var occurrence = this.Recorder.Occurrences[0];
        Assert.Multiple(() =>
        {
            Assert.That(occurrence.PathAndQuery, Is.EqualTo("/Media/Missing.PNG?x=1"));
            Assert.That(occurrence.IsFile, Is.True);
            Assert.That(occurrence.PageResolved, Is.True);
            Assert.That(occurrence.Domain, Is.Null);
            Assert.That(occurrence.Culture, Is.Null);
        });
    }

    /// <summary>
    /// Tests that a POST for a missing file is left alone: no 404 page, nothing recorded.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task Post_Missing_File_Is_Untouched()
    {
        using var response = await this.SendAsync(HttpMethod.Post, "/missing.pdf");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
            Assert.That(body, Does.Not.Contain(NotFoundPageTitle));
            Assert.That(this.Recorder.Occurrences, Is.Empty);
        });
    }

    /// <summary>
    /// Tests that missing files on Umbraco-owned paths and under /.well-known are left alone.
    /// </summary>
    /// <param name="path">The missing file.</param>
    /// <returns>A task.</returns>
    [TestCase("/umbraco/missing.js")]
    [TestCase("/App_Plugins/missing.js")]
    [TestCase("/.well-known/missing.txt")]
    public async Task Excluded_Paths_Are_Not_Handled(string path)
    {
        using var response = await this.GetAsync(path);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
            Assert.That(body, Does.Not.Contain(NotFoundPageTitle));
            Assert.That(this.Recorder.Occurrences, Is.Empty);
        });
    }

    /// <summary>
    /// Tests that an existing static file is served unchanged.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task Existing_File_Is_Served_Unchanged()
    {
        var filePath = Path.Combine(this.TestHelper.WorkingDirectory, ExistingFileName);
        await File.WriteAllTextAsync(filePath, ExistingFileContent);
        try
        {
            using var response = await this.GetAsync("/" + ExistingFileName);
            var body = await response.Content.ReadAsStringAsync();

            Assert.Multiple(() =>
            {
                Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
                Assert.That(body, Is.EqualTo(ExistingFileContent));
                Assert.That(this.Recorder.Occurrences, Is.Empty);
            });
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    /// <summary>
    /// Tests that a missing page (no extension) is handled by the page path only: rendered once, recorded once as a page.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task Missing_Page_Is_Handled_By_The_Page_Path_Only()
    {
        using var response = await this.GetAsync(MissingPath);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
            Assert.That(CountOccurrences(body, NotFoundPageTitle), Is.EqualTo(1));
            Assert.That(this.Recorder.Occurrences, Has.Count.EqualTo(1));
        });
        Assert.That(this.Recorder.Occurrences[0].IsFile, Is.False);
    }

    private static int CountOccurrences(string text, string value)
    {
        var count = 0;
        var index = text.IndexOf(value, StringComparison.Ordinal);
        while (index >= 0)
        {
            count++;
            index = text.IndexOf(value, index + value.Length, StringComparison.Ordinal);
        }

        return count;
    }
}
