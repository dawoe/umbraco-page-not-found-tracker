// Copyright (c) Dave Woestenborghs and contributors. Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Text;

namespace Umbraco.Community.PageNotFoundTracker.Infrastructure.Blocking;

/// <summary>
/// Translates a normalised block pattern to a SQL <c>LIKE</c> pattern (escape character backslash).
/// </summary>
internal static class BlockLikePattern
{
    /// <summary>
    /// Translates a pattern. Callers match <c>column LIKE Like ESCAPE backslash OR column = Exact</c>.
    /// </summary>
    /// <param name="normalisedPattern">The normalised pattern.</param>
    /// <returns>The LIKE pattern and, for a pattern ending in <c>/*</c>, the exact path before that suffix (otherwise null).</returns>
    public static (string Like, string? Exact) ToLike(string normalisedPattern)
    {
        var builder = new StringBuilder(normalisedPattern.Length + 4);
        foreach (char c in normalisedPattern)
        {
            switch (c)
            {
                case '*':
                    builder.Append('%');
                    break;
                case '\\':
                case '%':
                case '_':
                case '[':
                    builder.Append('\\').Append(c);
                    break;
                default:
                    builder.Append(c);
                    break;
            }
        }

        string? exact = normalisedPattern.EndsWith("/*", StringComparison.Ordinal) ? normalisedPattern[..^2] : null;
        return (builder.ToString(), exact);
    }
}
