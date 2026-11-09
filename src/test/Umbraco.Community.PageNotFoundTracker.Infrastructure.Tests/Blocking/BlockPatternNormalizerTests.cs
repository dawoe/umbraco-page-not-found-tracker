// Copyright (c) Dave Woestenborghs and contributors. Licensed under the MIT License. See LICENSE in the project root for license information.

using Umbraco.Community.PageNotFoundTracker.Infrastructure.Blocking;

namespace Umbraco.Community.PageNotFoundTracker.Infrastructure.Tests.Blocking;

/// <summary>
/// Unit tests for <see cref="BlockPatternNormalizer"/>.
/// </summary>
[TestFixture]
internal sealed class BlockPatternNormalizerTests
{
    /// <summary>
    /// Case and trailing slash do not matter.
    /// </summary>
    [Test]
    public void Normalize_IgnoresCaseAndTrailingSlash()
    {
        Assert.That(BlockPatternNormalizer.Normalize("/WP-Admin/"), Is.EqualTo(BlockPatternNormalizer.Normalize("/wp-admin")));
    }

    /// <summary>
    /// Repeated slashes collapse and the trailing slash goes.
    /// </summary>
    [Test]
    public void Normalize_CollapsesSlashes()
    {
        Assert.That(BlockPatternNormalizer.Normalize("//a///b/"), Is.EqualTo("/a/b"));
    }

    /// <summary>
    /// Escapes are decoded once, not twice.
    /// </summary>
    [Test]
    public void Normalize_DecodesOnce()
    {
        Assert.That(BlockPatternNormalizer.Normalize("/a%2Eb"), Is.EqualTo("/a.b"));
        Assert.That(BlockPatternNormalizer.Normalize("/a%2541"), Is.EqualTo("/a%41"));
    }

    /// <summary>
    /// Empty and root give a lone slash; a missing leading slash is added.
    /// </summary>
    /// <param name="input">The input.</param>
    /// <param name="expected">The expected value.</param>
    [TestCase("", "/")]
    [TestCase("/", "/")]
    [TestCase("abc", "/abc")]
    public void Normalize_EnsuresLeadingSlash(string input, string expected)
    {
        Assert.That(BlockPatternNormalizer.Normalize(input), Is.EqualTo(expected));
    }
}
