// Copyright (c) Dave Woestenborghs and contributors. Licensed under the MIT License. See LICENSE in the project root for license information.

namespace Umbraco.Community.PageNotFoundTracker.Core.Tracking;

/// <summary>
/// One merged identity (URL, domain and culture) with the number of times it was hit. The unit the flush and the
/// upsert work with.
/// </summary>
/// <param name="Url">The normalised URL.</param>
/// <param name="UrlHash">The SHA-256 hash (hex) of the full normalised URL.</param>
/// <param name="RelativePath">The path relative to the domain, truncated to the column size.</param>
/// <param name="DisplayUrl">The URL as shown to editors.</param>
/// <param name="Domain">The matched domain, empty when none.</param>
/// <param name="Culture">The culture, empty when none.</param>
/// <param name="IsFile">Whether the request was for a file.</param>
/// <param name="Count">The number of hits.</param>
/// <param name="FirstSeen">The UTC time of the first hit.</param>
/// <param name="LastSeen">The UTC time of the last hit.</param>
public sealed record TrackedUrlHit(
    string Url,
    string UrlHash,
    string RelativePath,
    string DisplayUrl,
    string Domain,
    string Culture,
    bool IsFile,
    long Count,
    DateTime FirstSeen,
    DateTime LastSeen);
