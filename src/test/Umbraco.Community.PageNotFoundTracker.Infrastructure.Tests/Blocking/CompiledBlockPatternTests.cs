// Copyright (c) Dave Woestenborghs and contributors. Licensed under the MIT License. See LICENSE in the project root for license information.

using Umbraco.Community.PageNotFoundTracker.Infrastructure.Blocking;

namespace Umbraco.Community.PageNotFoundTracker.Infrastructure.Tests.Blocking;

/// <summary>
/// Unit tests for <see cref="CompiledBlockPattern"/>.
/// </summary>
[TestFixture]
internal sealed class CompiledBlockPatternTests
{
    /// <summary>
    /// Matching table. A pattern ending in <c>/*</c> also matches the path before it, because a request to
    /// <c>/wp-admin/</c> normalises to <c>/wp-admin</c>.
    /// </summary>
    /// <param name="pattern">The pattern.</param>
    /// <param name="path">The path.</param>
    /// <param name="expected">Whether it matches.</param>
    [TestCase("/wp-admin/*", "/wp-admin/", true)]
    [TestCase("/wp-admin/*", "/wp-admin/a/b/c", true)]
    [TestCase("/wp-admin/*", "/wp-admin", true)]
    [TestCase("/wp-admin/*", "/x/wp-admin/a", false)]
    [TestCase("/wp-admin/*", "/wp-administrator", false)]
    [TestCase("/*.php", "/a/b/x.php", true)]
    [TestCase("/*.php", "/x.php5", false)]
    [TestCase("/a*c*e", "/abcde", true)]
    [TestCase("/a*c*e", "/ace", true)]
    [TestCase("/a*c*e", "/ae", false)]
    [TestCase("/ab*bc", "/abc", false)]
    [TestCase("/ab*bc", "/abbc", true)]
    [TestCase("/old", "/old", true)]
    [TestCase("/old", "/old/x", false)]
    [TestCase("/old", "/OLD", true)]
    public void IsMatch_Table(string pattern, string path, bool expected)
    {
        Assert.That(CompiledBlockPattern.Compile(pattern).IsMatch(path), Is.EqualTo(expected));
    }

    /// <summary>
    /// The literal prefix length counts characters before the first star.
    /// </summary>
    [Test]
    public void LiteralPrefixLength_CountsBeforeFirstStar()
    {
        Assert.That(CompiledBlockPattern.Compile("/wp-*").LiteralPrefixLength, Is.EqualTo(4));
        Assert.That(CompiledBlockPattern.Compile("/abc").LiteralPrefixLength, Is.EqualTo(4));
    }
}
