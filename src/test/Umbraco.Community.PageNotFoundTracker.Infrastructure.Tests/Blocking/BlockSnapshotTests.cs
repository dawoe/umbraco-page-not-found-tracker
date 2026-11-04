// Copyright (c) Dave Woestenborghs and contributors. Licensed under the MIT License. See LICENSE in the project root for license information.

using Umbraco.Cms.Core.Routing;
using Umbraco.Community.PageNotFoundTracker.Core.Blocking;
using Umbraco.Community.PageNotFoundTracker.Infrastructure.Blocking;
using Umbraco.Community.PageNotFoundTracker.Infrastructure.NotFound;

namespace Umbraco.Community.PageNotFoundTracker.Infrastructure.Tests.Blocking;

/// <summary>
/// Unit tests for <see cref="BlockSnapshot"/>.
/// </summary>
[TestFixture]
internal sealed class BlockSnapshotTests
{
    private const string SiteA = "localhost:44402/sitea";
    private const string SiteB = "localhost:44402/siteb";

    private static BlockRule Rule(string pattern, int status, string domain = "", string culture = "")
    {
        return new BlockRule
        {
            Pattern = BlockPatternNormalizer.Normalize(pattern),
            DisplayPattern = pattern,
            StatusCode = status,
            Domain = domain,
            Culture = culture,
        };
    }

    private static IReadOnlyList<DomainEntry> Domains(params (string Name, string Culture)[] domains)
    {
        return DomainSelector.FromCache(
            domains.Select((d, index) => new Domain(index + 1, d.Name, 1000 + index, d.Culture, false, index)));
    }

    private static IReadOnlyList<DomainEntry> AliasDomains() => Domains((SiteA, "en-US"), (SiteB, "nl-NL"));

    /// <summary>
    /// Tests that the empty snapshot blocks nothing.
    /// </summary>
    [Test]
    public void Empty_Blocks_Nothing()
    {
        Assert.That(BlockSnapshot.Empty.FindStatus("/wp-admin", "localhost", 44401), Is.Null);
        Assert.That(BlockSnapshot.Empty.NeedsDomainMatch, Is.False);
    }

    /// <summary>
    /// Tests that a rule for all domains matches on every domain and without a domain.
    /// </summary>
    /// <param name="path">The request path.</param>
    [TestCase("/sitea/old")]
    [TestCase("/siteb/old")]
    [TestCase("/old")]
    public void All_Domain_Rule_Matches_Every_Domain_And_No_Domain(string path)
    {
        var snapshot = BlockSnapshot.Build([Rule("/old", 410)], AliasDomains());

        Assert.That(snapshot.FindStatus(path, "localhost", 44402), Is.EqualTo(410));
    }

    /// <summary>
    /// Tests that a domain-scoped rule matches only its own domain and never a request without a domain.
    /// </summary>
    [Test]
    public void Domain_Rule_Matches_Only_Its_Domain()
    {
        var snapshot = BlockSnapshot.Build([Rule("/old", 410, SiteA)], AliasDomains());

        Assert.That(snapshot.FindStatus("/sitea/old", "localhost", 44402), Is.EqualTo(410));
        Assert.That(snapshot.FindStatus("/siteb/old", "localhost", 44402), Is.Null);
        Assert.That(snapshot.FindStatus("/old", "localhost", 44402), Is.Null);
        Assert.That(snapshot.FindStatus("/sitea/old", "localhost", 44499), Is.Null);
    }

    /// <summary>
    /// Tests that a culture-scoped rule needs the matched domain's culture.
    /// </summary>
    [Test]
    public void Culture_Rule_Needs_The_Domain_Culture()
    {
        var snapshot = BlockSnapshot.Build([Rule("/old", 403, culture: "nl-NL")], AliasDomains());

        Assert.That(snapshot.FindStatus("/siteb/old", "localhost", 44402), Is.EqualTo(403));
        Assert.That(snapshot.FindStatus("/sitea/old", "localhost", 44402), Is.Null);
        Assert.That(snapshot.FindStatus("/old", "localhost", 44402), Is.Null);
    }

    /// <summary>
    /// Tests that the domain's alias path is removed before matching.
    /// </summary>
    [Test]
    public void Alias_Prefix_Is_Stripped_Before_Matching()
    {
        var snapshot = BlockSnapshot.Build([Rule("/wp-admin/*", 410)], Domains(("localhost:44402/sitea", "en-US")));

        Assert.That(snapshot.NeedsDomainMatch, Is.True);
        Assert.That(snapshot.FindStatus("/sitea/wp-admin/x", "localhost", 44402), Is.EqualTo(410));
        Assert.That(snapshot.FindStatus("/sitea/wp-admin", "localhost", 44402), Is.EqualTo(410));
        Assert.That(snapshot.FindStatus("/sitea/x/wp-admin/x", "localhost", 44402), Is.Null);
    }

    /// <summary>
    /// Precedence: domain over all domains, culture over all cultures, longer literal prefix, then lowest status.
    /// Rule A and rule B both match <c>/xyz/a</c> on <c>localhost:44401</c> (culture en-US); the order they are
    /// given in does not matter.
    /// </summary>
    /// <param name="patternA">Pattern of rule A.</param>
    /// <param name="statusA">Status of rule A.</param>
    /// <param name="domainA">Domain of rule A.</param>
    /// <param name="cultureA">Culture of rule A.</param>
    /// <param name="patternB">Pattern of rule B.</param>
    /// <param name="statusB">Status of rule B.</param>
    /// <param name="domainB">Domain of rule B.</param>
    /// <param name="cultureB">Culture of rule B.</param>
    /// <param name="expected">The expected status.</param>
    [TestCase("/xyz/*", 403, "", "", "/x*", 410, "localhost:44401", "", 410, TestName = "Domain beats all domains")]
    [TestCase("/xyz/*", 403, "", "", "/x*", 410, "", "en-US", 410, TestName = "Culture beats all cultures")]
    [TestCase("/xyz/*", 403, "", "en-US", "/x*", 410, "localhost:44401", "", 410, TestName = "Domain beats culture")]
    [TestCase("/x*", 403, "localhost:44401", "en-US", "/xyz/*", 410, "localhost:44401", "", 403, TestName = "Domain and culture beat domain only")]
    [TestCase("/x*", 403, "", "", "/xyz/*", 410, "", "", 410, TestName = "Longer literal prefix wins")]
    [TestCase("/x*z*", 410, "", "", "/x*a", 403, "", "", 403, TestName = "Lowest status breaks ties")]
    public void Most_Specific_Rule_Wins(
        string patternA,
        int statusA,
        string domainA,
        string cultureA,
        string patternB,
        int statusB,
        string domainB,
        string cultureB,
        int expected)
    {
        var domains = Domains(("localhost:44401", "en-US"));
        var a = Rule(patternA, statusA, domainA, cultureA);
        var b = Rule(patternB, statusB, domainB, cultureB);

        Assert.That(BlockSnapshot.Build([a, b], domains).FindStatus("/xyz/a", "localhost", 44401), Is.EqualTo(expected));
        Assert.That(BlockSnapshot.Build([b, a], domains).FindStatus("/xyz/a", "localhost", 44401), Is.EqualTo(expected));
    }

    /// <summary>
    /// Tests that Umbraco-owned paths are never blocked, even by a rule the validator would reject.
    /// </summary>
    /// <param name="path">The request path.</param>
    [TestCase("/umbraco")]
    [TestCase("/umbraco/backoffice/x")]
    [TestCase("/app_plugins/x.js")]
    [TestCase("/_framework/blazor.js")]
    [TestCase("/sitea/umbraco/x")]
    public void Owned_Path_Is_Never_Blocked(string path)
    {
        var snapshot = BlockSnapshot.Build(
            [new BlockRule { Pattern = "/*", StatusCode = 410 }],
            Domains(("localhost:44402/sitea", "en-US")));

        Assert.That(snapshot.FindStatus(path, "localhost", 44402), Is.Null);
        Assert.That(snapshot.FindStatus("/other", "localhost", 44402), Is.EqualTo(410));
    }

    /// <summary>
    /// Tests when domain matching is needed.
    /// </summary>
    [Test]
    public void NeedsDomainMatch_Only_For_Scoped_Rules_Or_Path_Domains()
    {
        var hostDomains = Domains(("www.sitea.local:44404", "en-US"), ("www.siteb.local:44404", "nl-NL"));

        Assert.That(BlockSnapshot.Build([Rule("/old", 410)], hostDomains).NeedsDomainMatch, Is.False);
        Assert.That(BlockSnapshot.Build([Rule("/old", 410)], []).NeedsDomainMatch, Is.False);
        Assert.That(BlockSnapshot.Build([Rule("/old", 410, "www.sitea.local:44404")], hostDomains).NeedsDomainMatch, Is.True);
        Assert.That(BlockSnapshot.Build([Rule("/old", 410, culture: "en-US")], hostDomains).NeedsDomainMatch, Is.True);
        Assert.That(BlockSnapshot.Build([Rule("/old", 410)], AliasDomains()).NeedsDomainMatch, Is.True);
    }

    /// <summary>
    /// Tests that a domain-scoped rule on a hostname site matches only that host.
    /// </summary>
    [Test]
    public void Domain_Rule_On_Hostnames()
    {
        var hostDomains = Domains(("www.sitea.local:44404", "en-US"), ("www.siteb.local:44404", "nl-NL"));
        var snapshot = BlockSnapshot.Build([Rule("/old", 410, "www.sitea.local:44404")], hostDomains);

        Assert.That(snapshot.FindStatus("/old", "www.siteA.local", 44404), Is.EqualTo(410));
        Assert.That(snapshot.FindStatus("/old", "www.siteB.local", 44404), Is.Null);
    }
}
