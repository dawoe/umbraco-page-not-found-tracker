// Copyright (c) Dave Woestenborghs and contributors. Licensed under the MIT License. See LICENSE in the project root for license information.

using Umbraco.Community.PageNotFoundTracker.Infrastructure.Tracking;

namespace Umbraco.Community.PageNotFoundTracker.Infrastructure.Tests;

/// <summary>
/// Unit tests for the <see cref="LikePattern"/> class.
/// </summary>
[TestFixture]
internal sealed class LikePatternTests
{
    /// <summary>
    /// The LIKE wildcards and the escape character are escaped so they match literally.
    /// </summary>
    /// <param name="input">The search term.</param>
    /// <param name="expected">The expected pattern.</param>
    [TestCase("%", "%\\%%")]
    [TestCase("_", "%\\_%")]
    [TestCase("[", "%\\[%")]
    [TestCase("\\", "%\\\\%")]
    [TestCase("a%b_c[d\\e", "%a\\%b\\_c\\[d\\\\e%")]
    public void Contains_EscapesWildcardsAndEscapeCharacter(string input, string expected)
    {
        Assert.That(LikePattern.Contains(input), Is.EqualTo(expected));
    }

    /// <summary>
    /// The term is percent-decoded once, never twice.
    /// </summary>
    [Test]
    public void Contains_DecodesOnce_ThenEscapes()
    {
        Assert.That(LikePattern.Contains("%2541"), Is.EqualTo("%\\%41%"));
    }

    /// <summary>
    /// The term is lower-cased.
    /// </summary>
    [Test]
    public void Contains_LowerCases()
    {
        Assert.That(LikePattern.Contains("ABC"), Is.EqualTo("%abc%"));
    }

    /// <summary>
    /// A malformed escape does not throw and stays as typed.
    /// </summary>
    [Test]
    public void Contains_MalformedEscape_DoesNotThrow()
    {
        string? result = null;
        Assert.DoesNotThrow(() => result = LikePattern.Contains("%E0%A4%A"));
        Assert.That(result, Does.StartWith("%").And.EndsWith("%"));
    }

    /// <summary>
    /// A long term is accepted.
    /// </summary>
    [Test]
    public void Contains_ThreeThousandCharacters_IsAccepted()
    {
        string result = LikePattern.Contains(new string('a', 3000));

        Assert.That(result, Has.Length.EqualTo(3002));
    }
}
