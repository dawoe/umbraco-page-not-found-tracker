// Copyright (c) Dave Woestenborghs and contributors. Licensed under the MIT License. See LICENSE in the project root for license information.

using Umbraco.Cms.Core.Routing;
using Umbraco.Community.PageNotFoundTracker.Infrastructure.NotFound;

namespace Umbraco.Community.PageNotFoundTracker.Infrastructure.Tests.NotFound;

/// <summary>
/// Unit tests for <see cref="DomainSelector"/>.
/// </summary>
[TestFixture]
internal sealed class DomainSelectorTests
{
    private static IReadOnlyList<DomainEntry> Entries(params string[] names)
    {
        return DomainSelector.FromCache(
            names.Select((name, index) => new Domain(index + 1, name, 1000 + index, index % 2 == 0 ? "en-US" : "nl-NL", false, index)));
    }

    /// <summary>
    /// Tests that the cache domains are turned into entries with the lower-cased name, the parsed parts and the culture.
    /// </summary>
    [Test]
    public void FromCache_Parses_Name_Host_Port_Path_And_Culture()
    {
        var entries = Entries("LocalHost:44402/SiteA/", "www.siteB.local", "https://example.com:8080/x");

        Assert.That(entries, Has.Count.EqualTo(3));
        Assert.That(entries[0], Is.EqualTo(new DomainEntry("localhost:44402/sitea/", "LocalHost", 44402, "/SiteA", "en-US")));
        Assert.That(entries[1], Is.EqualTo(new DomainEntry("www.siteb.local", "www.siteB.local", null, string.Empty, "nl-NL")));
        Assert.That(entries[2], Is.EqualTo(new DomainEntry("https://example.com:8080/x", "example.com", 8080, "/x", "en-US")));
    }

    /// <summary>
    /// Tests that an unparsable domain is skipped.
    /// </summary>
    [Test]
    public void FromCache_Skips_Unparsable_Domains()
    {
        Assert.That(Entries("localhost:abc", "localhost:44401").Select(e => e.Name), Is.EqualTo(new[] { "localhost:44401" }));
    }

    /// <summary>
    /// Tests that the domain whose path prefixes the request is returned.
    /// </summary>
    /// <param name="path">The request path.</param>
    /// <param name="expectedName">The expected domain name.</param>
    [TestCase("/siteA/x.png", "localhost:44402/sitea")]
    [TestCase("/siteB/x.png", "localhost:44402/siteb")]
    [TestCase("/SITEA/x.png", "localhost:44402/sitea")]
    [TestCase("/sitea", "localhost:44402/sitea")]
    [TestCase("/sitea/", "localhost:44402/sitea")]
    public void Select_By_Path(string path, string expectedName)
    {
        var entries = Entries("localhost:44402/sitea", "localhost:44402/siteb");

        Assert.That(DomainSelector.Select(entries, "LocalHost", 44402, path)?.Name, Is.EqualTo(expectedName));
    }

    /// <summary>
    /// Tests that the longest path wins over a bare host.
    /// </summary>
    [Test]
    public void Select_Prefers_Most_Specific_Path()
    {
        var entries = Entries("localhost:44403", "localhost:44403/nl");

        Assert.That(DomainSelector.Select(entries, "localhost", 44403, "/nl/x")?.Name, Is.EqualTo("localhost:44403/nl"));
        Assert.That(DomainSelector.Select(entries, "localhost", 44403, "/x")?.Name, Is.EqualTo("localhost:44403"));
    }

    /// <summary>
    /// Tests that a path prefix must end on a segment boundary.
    /// </summary>
    [Test]
    public void Select_Requires_Segment_Boundary()
    {
        Assert.That(DomainSelector.Select(Entries("localhost:44402/sitea"), "localhost", 44402, "/siteabc/x"), Is.Null);
    }

    /// <summary>
    /// Tests that host and port must both match.
    /// </summary>
    [Test]
    public void Select_By_Host_And_Port()
    {
        var entries = Entries("www.sitea.local:44404", "www.siteb.local:44404", "localhost");

        Assert.That(DomainSelector.Select(entries, "www.siteB.local", 44404, "/x")?.Name, Is.EqualTo("www.siteb.local:44404"));
        Assert.That(DomainSelector.Select(entries, "localhost", 44404, "/x"), Is.Null);
        Assert.That(DomainSelector.Select(entries, "localhost", null, "/x")?.Name, Is.EqualTo("localhost"));
    }

    /// <summary>
    /// Tests that no match gives null.
    /// </summary>
    [Test]
    public void Select_Returns_Null_When_Nothing_Matches()
    {
        var entries = Entries("localhost:44402/sitea", "localhost:44402/siteb");

        Assert.That(DomainSelector.Select(entries, "localhost", 44402, "/x.png"), Is.Null);
        Assert.That(DomainSelector.Select(entries, "localhost", 44499, "/sitea/x.png"), Is.Null);
        Assert.That(DomainSelector.Select([], "localhost", 44402, "/sitea"), Is.Null);
    }
}
