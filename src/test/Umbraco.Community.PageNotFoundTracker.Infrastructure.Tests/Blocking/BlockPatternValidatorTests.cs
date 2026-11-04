// Copyright (c) Dave Woestenborghs and contributors. Licensed under the MIT License. See LICENSE in the project root for license information.

using Umbraco.Community.PageNotFoundTracker.Infrastructure.Blocking;

namespace Umbraco.Community.PageNotFoundTracker.Infrastructure.Tests.Blocking;

/// <summary>
/// Unit tests for <see cref="BlockPatternValidator"/>.
/// </summary>
[TestFixture]
internal sealed class BlockPatternValidatorTests
{
    /// <summary>
    /// Patterns without a literal character are rejected.
    /// </summary>
    /// <param name="pattern">The pattern.</param>
    [TestCase("*")]
    [TestCase("/*")]
    [TestCase("/**")]
    [TestCase("//*/")]
    public void Validate_RejectsNoLiteral(string pattern)
    {
        Assert.That(BlockPatternValidator.Validate(pattern, 410), Is.Not.Null);
    }

    /// <summary>
    /// Patterns that cover Umbraco-owned paths are rejected and the message names the path.
    /// </summary>
    /// <param name="pattern">The pattern.</param>
    /// <param name="path">The owned path named in the message.</param>
    [TestCase("/um*", "/umbraco")]
    [TestCase("/*umbraco*", "/umbraco")]
    [TestCase("/umbraco", "/umbraco")]
    [TestCase("/umbraco/x*", "/umbraco")]
    [TestCase("/app_plugins/*", "/App_Plugins")]
    public void Validate_RejectsOwnedPaths(string pattern, string path)
    {
        string? message = BlockPatternValidator.Validate(pattern, 410);

        Assert.That(message, Is.Not.Null);
        Assert.That(message, Does.Contain(path));
    }

    /// <summary>
    /// Ordinary patterns are accepted.
    /// </summary>
    /// <param name="pattern">The pattern.</param>
    [TestCase("/*.php")]
    [TestCase("/wp-admin/*")]
    [TestCase("/old-page")]
    public void Validate_AcceptsValid(string pattern)
    {
        Assert.That(BlockPatternValidator.Validate(pattern, 410), Is.Null);
    }

    /// <summary>
    /// Length is limited to 800.
    /// </summary>
    [Test]
    public void Validate_RejectsTooLong()
    {
        Assert.That(BlockPatternValidator.Validate("/" + new string('a', 800), 410), Is.Not.Null);
        Assert.That(BlockPatternValidator.Validate("/" + new string('a', 799), 410), Is.Null);
    }

    /// <summary>
    /// Status must be 400 to 499.
    /// </summary>
    /// <param name="status">The status code.</param>
    /// <param name="valid">Whether it is valid.</param>
    [TestCase(399, false)]
    [TestCase(400, true)]
    [TestCase(410, true)]
    [TestCase(499, true)]
    [TestCase(500, false)]
    public void Validate_StatusRange(int status, bool valid)
    {
        Assert.That(BlockPatternValidator.Validate("/abc", status) is null, Is.EqualTo(valid));
    }
}
