// Copyright (c) Dave Woestenborghs and contributors. Licensed under the MIT License. See LICENSE in the project root for license information.

using Umbraco.Community.PageNotFoundTracker.Core.NotFound;

namespace Umbraco.Community.PageNotFoundTracker.Infrastructure.Tests;

/// <summary>
/// Unit tests for the <see cref="UmbracoOwnedPaths"/> class.
/// </summary>
[TestFixture]
internal sealed class UmbracoOwnedPathsTests
{
    /// <summary>
    /// Tests that Umbraco-owned paths are recognised.
    /// </summary>
    /// <param name="path">The path.</param>
    [TestCase("/umbraco")]
    [TestCase("/umbraco/management/api/v1/x")]
    [TestCase("/UMBRACO/x")]
    [TestCase("/App_Plugins/a/b.js")]
    [TestCase("/_framework/blazor.js")]
    public void IsUmbracoOwned_Returns_True_For_Owned_Paths(string path)
    {
        Assert.That(UmbracoOwnedPaths.IsUmbracoOwned(path), Is.True);
    }

    /// <summary>
    /// Tests that other paths are not recognised as owned.
    /// </summary>
    /// <param name="path">The path.</param>
    [TestCase("/umbracofoo/x.js")]
    [TestCase("/media/a.png")]
    [TestCase("/about")]
    public void IsUmbracoOwned_Returns_False_For_Other_Paths(string path)
    {
        Assert.That(UmbracoOwnedPaths.IsUmbracoOwned(path), Is.False);
    }

    /// <summary>
    /// Tests that paths whose last segment has an extension are file requests.
    /// </summary>
    /// <param name="path">The path.</param>
    [TestCase("/x.pdf")]
    [TestCase("/a/b.min.js")]
    [TestCase("/media/a.png")]
    public void IsFileRequest_Returns_True_For_Paths_With_Extension(string path)
    {
        Assert.That(UmbracoOwnedPaths.IsFileRequest(path), Is.True);
    }

    /// <summary>
    /// Tests that paths without an extension in the last segment are not file requests.
    /// </summary>
    /// <param name="path">The path.</param>
    [TestCase("/about")]
    [TestCase("/a.b/c")]
    [TestCase("/")]
    public void IsFileRequest_Returns_False_For_Paths_Without_Extension(string path)
    {
        Assert.That(UmbracoOwnedPaths.IsFileRequest(path), Is.False);
    }
}
