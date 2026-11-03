// Copyright (c) Dave Woestenborghs and contributors. Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Text;

namespace Umbraco.Community.PageNotFoundTracker.Infrastructure.Tracking;

/// <summary>
/// Builds the pattern for a "contains" search with <c>EF.Functions.Like(column, pattern, "\\")</c>.
/// </summary>
internal static class LikePattern
{
    /// <summary>
    /// Builds a "contains" pattern: the term is percent-decoded once (a malformed escape stays as typed) and lower-cased,
    /// the LIKE wildcards <c>%</c>, <c>_</c>, <c>[</c> and the escape character <c>\</c> are escaped with <c>\</c>, and
    /// the result is wrapped in <c>%</c>.
    /// </summary>
    /// <param name="search">The search term.</param>
    /// <returns>The pattern.</returns>
    public static string Contains(string search)
    {
        ArgumentNullException.ThrowIfNull(search);

        string decoded = Uri.UnescapeDataString(search).ToLowerInvariant();
        var builder = new StringBuilder(decoded.Length + 2);
        builder.Append('%');
        foreach (char c in decoded)
        {
            if (c is '\\' or '%' or '_' or '[')
            {
                builder.Append('\\');
            }

            builder.Append(c);
        }

        return builder.Append('%').ToString();
    }
}
