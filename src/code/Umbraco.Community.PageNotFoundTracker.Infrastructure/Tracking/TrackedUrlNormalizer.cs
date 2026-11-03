// Copyright (c) Dave Woestenborghs and contributors. Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Security.Cryptography;
using System.Text;
using Umbraco.Community.PageNotFoundTracker.Core.Tracking;
using Umbraco.Community.PageNotFoundTracker.Infrastructure.NotFound;

namespace Umbraco.Community.PageNotFoundTracker.Infrastructure.Tracking;

/// <summary>
/// Turns a <see cref="NotFoundOccurrence"/> into a normalised <see cref="TrackedUrlHit"/>.
/// </summary>
internal static class TrackedUrlNormalizer
{
    private const int MaxUrlLength = 2000;
    private const int MaxRelativePathLength = 800;

    /// <summary>
    /// Normalises an occurrence into a hit with a count of one.
    /// </summary>
    /// <param name="occurrence">The 404 occurrence.</param>
    /// <param name="includeQueryString">Whether the query string is part of the identity.</param>
    /// <param name="utcNow">The UTC time of the hit.</param>
    /// <returns>The normalised hit.</returns>
    public static TrackedUrlHit Normalize(NotFoundOccurrence occurrence, bool includeQueryString, DateTime utcNow)
    {
        string raw = occurrence.PathAndQuery ?? string.Empty;
        int queryIndex = raw.IndexOf('?');
        string rawPath = queryIndex >= 0 ? raw[..queryIndex] : raw;
        string rawQuery = queryIndex >= 0 ? raw[(queryIndex + 1)..] : string.Empty;

        string path = NormalizePath(rawPath);
        string url = includeQueryString && rawQuery.Length > 0
            ? path + "?" + rawQuery.ToLowerInvariant()
            : path;

        string displayUrl = includeQueryString ? raw : rawPath;
        string domain = occurrence.Domain?.Name?.ToLowerInvariant() ?? string.Empty;

        return new TrackedUrlHit(
            Truncate(url, MaxUrlLength),
            Hash(url),
            Truncate(RelativeTo(path, domain), MaxRelativePathLength),
            Truncate(displayUrl, MaxUrlLength),
            domain,
            occurrence.Culture ?? string.Empty,
            occurrence.IsFile,
            1,
            utcNow,
            utcNow);
    }

    private static string NormalizePath(string rawPath)
    {
        string path;
        try
        {
            path = Uri.UnescapeDataString(rawPath);
        }
        catch (Exception ex) when (ex is UriFormatException or ArgumentException)
        {
            path = rawPath;
        }

        var builder = new StringBuilder(path.Length + 1);
        builder.Append('/');
        foreach (char c in path)
        {
            if (c == '/' && builder[^1] == '/')
            {
                continue;
            }

            builder.Append(c);
        }

        if (builder.Length > 1 && builder[^1] == '/')
        {
            builder.Length--;
        }

        return builder.ToString().ToLowerInvariant();
    }

    private static string RelativeTo(string path, string domainName)
    {
        string name = domainName;
        int schemeIndex = name.IndexOf("://", StringComparison.Ordinal);
        if (schemeIndex >= 0)
        {
            name = name[(schemeIndex + 3)..];
        }

        int slashIndex = name.IndexOf('/');
        if (slashIndex < 0)
        {
            return path;
        }

        string prefix = NormalizePath(name[slashIndex..]);
        if (prefix == "/")
        {
            return path;
        }

        if (path == prefix)
        {
            return "/";
        }

        return path.StartsWith(prefix + "/", StringComparison.Ordinal) ? path[prefix.Length..] : path;
    }

    private static string Hash(string url)
    {
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(url)));
    }

    private static string Truncate(string value, int maxLength)
    {
        return value.Length <= maxLength ? value : value[..maxLength];
    }
}
