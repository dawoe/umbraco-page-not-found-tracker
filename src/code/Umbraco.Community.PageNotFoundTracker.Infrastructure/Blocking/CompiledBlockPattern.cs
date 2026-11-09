// Copyright (c) Dave Woestenborghs and contributors. Licensed under the MIT License. See LICENSE in the project root for license information.

namespace Umbraco.Community.PageNotFoundTracker.Infrastructure.Blocking;

/// <summary>
/// A normalised block pattern prepared for matching. <c>*</c> matches any run of characters, slashes included.
/// A pattern ending in <c>/*</c> also matches the path before that suffix, because a request to <c>/x/</c> normalises
/// to <c>/x</c>.
/// </summary>
internal sealed class CompiledBlockPattern
{
    private readonly string[] parts;
    private readonly string? exactForTrailingStar;

    private CompiledBlockPattern(string pattern)
    {
        this.parts = pattern.Split('*');
        int firstStar = pattern.IndexOf('*', StringComparison.Ordinal);
        this.LiteralPrefixLength = firstStar < 0 ? pattern.Length : firstStar;
        this.exactForTrailingStar = pattern.EndsWith("/*", StringComparison.Ordinal) ? pattern[..^2] : null;
    }

    /// <summary>
    /// Gets the number of characters before the first <c>*</c> (the whole length without a wildcard).
    /// </summary>
    public int LiteralPrefixLength { get; }

    /// <summary>
    /// Compiles a normalised pattern.
    /// </summary>
    /// <param name="normalisedPattern">The normalised pattern.</param>
    /// <returns>The compiled pattern.</returns>
    public static CompiledBlockPattern Compile(string normalisedPattern)
    {
        return new CompiledBlockPattern(normalisedPattern);
    }

    /// <summary>
    /// Determines whether a normalised path matches the pattern (ordinal, ignoring case).
    /// </summary>
    /// <param name="normalisedPath">The normalised path.</param>
    /// <returns><c>true</c> when it matches.</returns>
    public bool IsMatch(string normalisedPath)
    {
        if (this.exactForTrailingStar is not null
            && string.Equals(normalisedPath, this.exactForTrailingStar, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (this.parts.Length == 1)
        {
            return string.Equals(normalisedPath, this.parts[0], StringComparison.OrdinalIgnoreCase);
        }

        string first = this.parts[0];
        string last = this.parts[^1];
        if (!normalisedPath.StartsWith(first, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        int position = first.Length;
        int limit = normalisedPath.Length - last.Length;
        if (limit < position || !normalisedPath.EndsWith(last, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        ReadOnlySpan<char> searchable = normalisedPath.AsSpan(0, limit);
        for (int i = 1; i < this.parts.Length - 1; i++)
        {
            string part = this.parts[i];
            if (part.Length == 0)
            {
                continue;
            }

            int found = searchable[position..].IndexOf(part, StringComparison.OrdinalIgnoreCase);
            if (found < 0)
            {
                return false;
            }

            position += found + part.Length;
        }

        return true;
    }
}
