// Copyright (c) Dave Woestenborghs and contributors. Licensed under the MIT License. See LICENSE in the project root for license information.

using Umbraco.Community.PageNotFoundTracker.Infrastructure.Redirects;

namespace Umbraco.Community.PageNotFoundTracker.Infrastructure.Tests.Redirects;

/// <summary>
/// Unit tests for the <see cref="RedirectRouteBuilder"/> class.
/// </summary>
[TestFixture]
internal sealed class RedirectRouteBuilderTests
{
    /// <summary>
    /// Tests the route for the given domain root and path.
    /// </summary>
    /// <param name="rootId">The domain root id.</param>
    /// <param name="path">The relative path.</param>
    /// <param name="expected">The expected route.</param>
    [TestCase(null, "/old", "/old")]
    [TestCase(null, "old", "/old")]
    [TestCase(null, "/old/", "/old")]
    [TestCase(null, "/a/b/", "/a/b")]
    [TestCase(null, "/", "/")]
    [TestCase(null, "", "/")]
    [TestCase(1234, "/old", "1234/old")]
    [TestCase(1234, "old", "1234/old")]
    [TestCase(1234, "/old/", "1234/old")]
    [TestCase(1234, "/", "1234/")]
    public void Builds_the_route(int? rootId, string path, string expected) =>
        Assert.That(RedirectRouteBuilder.Build(rootId, path), Is.EqualTo(expected));
}
