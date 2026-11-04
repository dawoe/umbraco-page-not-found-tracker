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
    /// gets the tracking normaliser's slash rules (runs of <c>/</c> collapsed to one, one trailing <c>/</c> dropped unless
    /// the term is a lone <c>/</c>) so a URL copied as displayed matches the stored URL, the LIKE wildcards <c>%</c>,
    /// <c>_</c>, <c>[</c> and the escape character <c>\</c> are escaped with <c>\</c>, and the result is wrapped in
    /// <c>%</c>.
    /// </summary>
    /// <param name="search">The search term.</param>
    /// <returns>The pattern.</returns>
    public static string Contains(string search)
    {
        ArgumentNullException.ThrowIfNull(search);

        string decoded = ApplySlashRules(Uri.UnescapeDataString(search).ToLowerInvariant());
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

    /// <summary>
    /// Collapses runs of <c>/</c> to one and drops one trailing <c>/</c>, keeping a lone <c>/</c>; the same path rules
    /// as <see cref="TrackedUrlNormalizer"/>, without prepending a <c>/</c> (a term is a fragment, not a path).
    /// </summary>
    /// <param name="term">The decoded, lower-cased term.</param>
    /// <returns>The term with the slash rules applied.</returns>
    private static string ApplySlashRules(string term)
    {
        var builder = new StringBuilder(term.Length);
        foreach (char c in term)
        {
            if (c == '/' && builder.Length > 0 && builder[^1] == '/')
            {
                continue;
            }

            builder.Append(c);
        }

        if (builder.Length > 1 && builder[^1] == '/')
        {
            builder.Length--;
        }

        return builder.ToString();
    }
}
