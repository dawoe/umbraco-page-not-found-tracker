// Copyright (c) Dave Woestenborghs and contributors. Licensed under the MIT License. See LICENSE in the project root for license information.

using Moq;
using Umbraco.Cms.Core.PublishedCache;
using Umbraco.Cms.Core.Routing;
using Umbraco.Community.PageNotFoundTracker.Core.NotFound;
using Umbraco.Community.PageNotFoundTracker.Infrastructure.NotFound;

namespace Umbraco.Community.PageNotFoundTracker.Infrastructure.Tests;

/// <summary>
/// Unit tests for the <see cref="RequestDomainMatcher"/> class.
/// </summary>
[TestFixture]
internal sealed class RequestDomainMatcherTests
{
    private static RequestDomainMatcher CreateMatcher(params string[] names)
    {
        var domains = names
            .Select((name, index) => new Domain(index + 1, name, 1000 + index, "en-US", false, index))
            .ToList();
        var cache = new Mock<IDomainCache>();
        cache.Setup(c => c.GetAll(false)).Returns(domains);
        return new RequestDomainMatcher(cache.Object);
    }

    private static NotFoundRequestContext Request(string host, int? port, string path)
    {
        return new NotFoundRequestContext(path, host, port, true);
    }

    /// <summary>
    /// Tests that the domain whose path prefixes the request is returned.
    /// </summary>
    /// <param name="path">The request path.</param>
    /// <param name="expectedContentId">The expected content id.</param>
    [TestCase("/siteA/x.png", 1000)]
    [TestCase("/siteB/x.png", 1001)]
    [TestCase("/SITEA/x.png", 1000)]
    public void Match_Selects_Domain_By_Path(string path, int expectedContentId)
    {
        var matcher = CreateMatcher("localhost:44402/sitea", "localhost:44402/siteb");

        var result = matcher.Match(Request("LocalHost", 44402, path));

        Assert.That(result?.ContentId, Is.EqualTo(expectedContentId));
    }

    /// <summary>
    /// Tests that a bare host domain matches any path.
    /// </summary>
    [Test]
    public void Match_Returns_Bare_Host_Domain()
    {
        var matcher = CreateMatcher("localhost:44401");

        Assert.That(matcher.Match(Request("localhost", 44401, "/x.png"))?.ContentId, Is.EqualTo(1000));
    }

    /// <summary>
    /// Tests that the longest path wins over a bare host.
    /// </summary>
    [Test]
    public void Match_Prefers_Most_Specific_Path()
    {
        var matcher = CreateMatcher("localhost:44403", "localhost:44403/nl");

        Assert.That(matcher.Match(Request("localhost", 44403, "/nl/x.png"))?.ContentId, Is.EqualTo(1001));
        Assert.That(matcher.Match(Request("localhost", 44403, "/x.png"))?.ContentId, Is.EqualTo(1000));
    }

    /// <summary>
    /// Tests that a path prefix must end on a segment boundary.
    /// </summary>
    [Test]
    public void Match_Requires_Segment_Boundary()
    {
        var matcher = CreateMatcher("localhost:44402/sitea");

        Assert.That(matcher.Match(Request("localhost", 44402, "/siteabc/x.png")), Is.Null);
    }

    /// <summary>
    /// Tests that no match returns null.
    /// </summary>
    [Test]
    public void Match_Returns_Null_When_Nothing_Matches()
    {
        var matcher = CreateMatcher("localhost:44402/sitea", "localhost:44402/siteb");

        Assert.That(matcher.Match(Request("localhost", 44402, "/x.png")), Is.Null);
        Assert.That(matcher.Match(Request("localhost", 44499, "/sitea/x.png")), Is.Null);
    }

    /// <summary>
    /// Tests the hostname scenario.
    /// </summary>
    [Test]
    public void Match_Selects_Domain_By_Host()
    {
        var matcher = CreateMatcher("www.sitea.local:44404", "www.siteb.local:44404");

        Assert.That(matcher.Match(Request("www.siteB.local", 44404, "/x.png"))?.ContentId, Is.EqualTo(1001));
        Assert.That(matcher.Match(Request("localhost", 44404, "/x.png")), Is.Null);
    }
}
