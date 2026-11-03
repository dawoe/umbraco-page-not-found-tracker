// Copyright (c) Dave Woestenborghs and contributors. Licensed under the MIT License. See LICENSE in the project root for license information.

using Umbraco.Cms.Core.Routing;
using Umbraco.Community.PageNotFoundTracker.Core.Tracking;
using Umbraco.Community.PageNotFoundTracker.Infrastructure.NotFound;
using Umbraco.Community.PageNotFoundTracker.Infrastructure.Tracking;

namespace Umbraco.Community.PageNotFoundTracker.Infrastructure.Tests;

/// <summary>
/// Unit tests for the <see cref="TrackedUrlNormalizer"/> class.
/// </summary>
[TestFixture]
internal sealed class TrackedUrlNormalizerTests
{
    private static readonly DateTime Now = new(2026, 11, 3, 10, 0, 0, DateTimeKind.Utc);

    /// <summary>
    /// Paths are normalised to a canonical form.
    /// </summary>
    /// <param name="input">The requested path.</param>
    /// <param name="expected">The expected normalised URL.</param>
    [TestCase("/About/", "/about")]
    [TestCase("/about", "/about")]
    [TestCase("/a//b///", "/a/b")]
    [TestCase("/", "/")]
    [TestCase("//", "/")]
    [TestCase("/Café", "/café")]
    [TestCase("/caf%C3%A9", "/caf%c3%a9")]
    [TestCase("/a%2Fb", "/a%2fb")]
    public void Normalize_Produces_Canonical_Path(string input, string expected)
    {
        TrackedUrlHit hit = TrackedUrlNormalizer.Normalize(Occurrence(input), false, Now);

        Assert.That(hit.Url, Is.EqualTo(expected));
        Assert.That(hit.Count, Is.EqualTo(1));
        Assert.That(hit.FirstSeen, Is.EqualTo(Now));
        Assert.That(hit.LastSeen, Is.EqualTo(Now));
    }

    /// <summary>
    /// Equivalent paths share a hash and the hash is lower-case hex SHA-256.
    /// </summary>
    [Test]
    public void Normalize_Hashes_The_Normalised_Url()
    {
        TrackedUrlHit a = TrackedUrlNormalizer.Normalize(Occurrence("/About/"), false, Now);
        TrackedUrlHit b = TrackedUrlNormalizer.Normalize(Occurrence("/about"), false, Now);

        Assert.That(a.UrlHash, Is.EqualTo(b.UrlHash));
        Assert.That(a.UrlHash, Has.Length.EqualTo(64));
        Assert.That(a.UrlHash, Does.Match("^[0-9a-f]{64}$"));
    }

    /// <summary>
    /// The normaliser does not percent-decode (the call points pass a path that is already decoded), so an encoded
    /// percent sign stays a different URL from the character it would decode to twice.
    /// </summary>
    [Test]
    public void Normalize_Does_Not_Decode_Twice()
    {
        TrackedUrlHit encoded = TrackedUrlNormalizer.Normalize(Occurrence("/a%41"), false, Now);
        TrackedUrlHit plain = TrackedUrlNormalizer.Normalize(Occurrence("/aA"), false, Now);

        Assert.That(encoded.Url, Is.EqualTo("/a%41"));
        Assert.That(encoded.UrlHash, Is.Not.EqualTo(plain.UrlHash));
    }

    /// <summary>
    /// A culture longer than its 50 character column is truncated.
    /// </summary>
    [Test]
    public void Normalize_Truncates_Long_Culture()
    {
        TrackedUrlHit hit = TrackedUrlNormalizer.Normalize(Occurrence("/x", "x.com", new string('c', 60)), false, Now);

        Assert.That(hit.Culture, Is.EqualTo(new string('c', 50)));
    }

    /// <summary>
    /// An invalid escape sequence does not throw.
    /// </summary>
    [Test]
    public void Normalize_Does_Not_Throw_On_Invalid_Escape()
    {
        TrackedUrlHit? hit = null;

        Assert.DoesNotThrow(() => hit = TrackedUrlNormalizer.Normalize(Occurrence("/%E0%A4%A"), false, Now));
        Assert.That(hit!.Url, Is.EqualTo("/%e0%a4%a"));
    }

    /// <summary>
    /// With the query string off, the query is dropped from both URLs.
    /// </summary>
    [Test]
    public void Normalize_Drops_Query_When_Off()
    {
        TrackedUrlHit hit = TrackedUrlNormalizer.Normalize(Occurrence("/x?b=1"), false, Now);

        Assert.That(hit.Url, Is.EqualTo("/x"));
        Assert.That(hit.DisplayUrl, Is.EqualTo("/x"));
    }

    /// <summary>
    /// With the query string on, the query is lower-cased and part of the identity.
    /// </summary>
    [Test]
    public void Normalize_Keeps_Lower_Cased_Query_When_On()
    {
        TrackedUrlHit one = TrackedUrlNormalizer.Normalize(Occurrence("/x?B=1"), true, Now);
        TrackedUrlHit two = TrackedUrlNormalizer.Normalize(Occurrence("/x?b=2"), true, Now);

        Assert.That(one.Url, Is.EqualTo("/x?b=1"));
        Assert.That(one.DisplayUrl, Is.EqualTo("/x?B=1"));
        Assert.That(one.UrlHash, Is.Not.EqualTo(two.UrlHash));
    }

    /// <summary>
    /// An empty query adds nothing.
    /// </summary>
    [Test]
    public void Normalize_Ignores_Empty_Query()
    {
        TrackedUrlHit hit = TrackedUrlNormalizer.Normalize(Occurrence("/x?"), true, Now);

        Assert.That(hit.Url, Is.EqualTo("/x"));
    }

    /// <summary>
    /// The domain prefix is removed from the relative path.
    /// </summary>
    [Test]
    public void Normalize_Strips_Domain_Path_Prefix()
    {
        TrackedUrlHit hit = TrackedUrlNormalizer.Normalize(
            Occurrence("/siteA/About/", "localhost:44402/sitea", "en-US"),
            false,
            Now);

        Assert.That(hit.Domain, Is.EqualTo("localhost:44402/sitea"));
        Assert.That(hit.Culture, Is.EqualTo("en-US"));
        Assert.That(hit.Url, Is.EqualTo("/sitea/about"));
        Assert.That(hit.RelativePath, Is.EqualTo("/about"));
    }

    /// <summary>
    /// The relative path is the root when only the prefix remains.
    /// </summary>
    [Test]
    public void Normalize_Relative_Path_Is_Root_When_Only_Prefix_Remains()
    {
        TrackedUrlHit hit = TrackedUrlNormalizer.Normalize(Occurrence("/siteA", "localhost:44402/sitea", null), false, Now);

        Assert.That(hit.RelativePath, Is.EqualTo("/"));
    }

    /// <summary>
    /// A path that only starts with the prefix text keeps it.
    /// </summary>
    [Test]
    public void Normalize_Keeps_Path_That_Only_Shares_Prefix_Text()
    {
        TrackedUrlHit hit = TrackedUrlNormalizer.Normalize(Occurrence("/siteab/x", "localhost:44402/sitea", null), false, Now);

        Assert.That(hit.RelativePath, Is.EqualTo("/siteab/x"));
    }

    /// <summary>
    /// A domain without a path and with a scheme gives no prefix.
    /// </summary>
    [Test]
    public void Normalize_Domain_Without_Path_Has_No_Prefix()
    {
        TrackedUrlHit hit = TrackedUrlNormalizer.Normalize(
            Occurrence("/About/Us", "https://www.sitea.local:44404/", null),
            false,
            Now);

        Assert.That(hit.RelativePath, Is.EqualTo("/about/us"));
        Assert.That(hit.Domain, Is.EqualTo("https://www.sitea.local:44404/"));
    }

    /// <summary>
    /// Without a domain, domain and culture are empty strings.
    /// </summary>
    [Test]
    public void Normalize_Without_Domain_Uses_Empty_Strings()
    {
        TrackedUrlHit hit = TrackedUrlNormalizer.Normalize(Occurrence("/x"), false, Now);

        Assert.That(hit.Domain, Is.EqualTo(string.Empty));
        Assert.That(hit.Culture, Is.EqualTo(string.Empty));
    }

    /// <summary>
    /// The domain name is lower-cased.
    /// </summary>
    [Test]
    public void Normalize_Lower_Cases_Domain()
    {
        TrackedUrlHit hit = TrackedUrlNormalizer.Normalize(Occurrence("/x", "WWW.SiteA.local:44404", null), false, Now);

        Assert.That(hit.Domain, Is.EqualTo("www.sitea.local:44404"));
    }

    /// <summary>
    /// The file flag is carried over.
    /// </summary>
    [Test]
    public void Normalize_Carries_File_Flag()
    {
        TrackedUrlHit hit = TrackedUrlNormalizer.Normalize(Occurrence("/a.png") with { IsFile = true }, false, Now);

        Assert.That(hit.IsFile, Is.True);
    }

    /// <summary>
    /// Long values are truncated, but the hash covers the full URL.
    /// </summary>
    [Test]
    public void Normalize_Truncates_Long_Values_And_Hashes_Full_Url()
    {
        string path3000 = "/" + new string('a', 2999);
        string path2999 = "/" + new string('a', 2998);

        TrackedUrlHit longHit = TrackedUrlNormalizer.Normalize(Occurrence(path3000), false, Now);
        TrackedUrlHit shorterHit = TrackedUrlNormalizer.Normalize(Occurrence(path2999), false, Now);

        Assert.That(longHit.Url, Has.Length.EqualTo(2000));
        Assert.That(longHit.DisplayUrl, Has.Length.EqualTo(2000));
        Assert.That(longHit.RelativePath, Has.Length.EqualTo(800));
        Assert.That(longHit.UrlHash, Is.Not.EqualTo(shorterHit.UrlHash));
    }

    /// <summary>
    /// A domain longer than the column is truncated to 255 characters; the relative path still uses the full domain.
    /// </summary>
    [Test]
    public void Normalize_Truncates_Long_Domain()
    {
        string domainName = new string('d', 300) + "/sitea";

        TrackedUrlHit hit = TrackedUrlNormalizer.Normalize(Occurrence("/siteA/x", domainName, null), false, Now);

        Assert.That(hit.Domain, Has.Length.EqualTo(255));
        Assert.That(hit.Domain, Is.EqualTo(new string('d', 255)));
        Assert.That(hit.RelativePath, Is.EqualTo("/x"));
    }

    private static NotFoundOccurrence Occurrence(string pathAndQuery, string? domainName = null, string? culture = null)
    {
        Domain? domain = domainName is null ? null : new Domain(1, domainName, 1000, culture, false, 0);
        return new NotFoundOccurrence(pathAndQuery, domain, culture, false, true);
    }
}
