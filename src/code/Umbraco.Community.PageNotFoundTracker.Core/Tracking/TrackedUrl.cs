// Copyright (c) Dave Woestenborghs and contributors. Licensed under the MIT License. See LICENSE in the project root for license information.

namespace Umbraco.Community.PageNotFoundTracker.Core.Tracking;

/// <summary>
/// A URL that ended in a 404, stored in the <see cref="TrackingConstants.TableName"/> table.
/// </summary>
public class TrackedUrl
{
    /// <summary>
    /// Gets or sets the identifier.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Gets or sets the normalised URL (maximum 2000 characters).
    /// </summary>
    public string Url { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the SHA-256 hash (hex, 64 characters) of the full normalised URL.
    /// </summary>
    public string UrlHash { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the path relative to the domain (maximum 800 characters).
    /// </summary>
    public string RelativePath { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the URL as shown to editors (maximum 2000 characters).
    /// </summary>
    public string DisplayUrl { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the matched domain, empty when none (maximum 255 characters).
    /// </summary>
    public string Domain { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the culture, empty when none (maximum 50 characters).
    /// </summary>
    public string Culture { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a value indicating whether the request was for a file.
    /// </summary>
    public bool IsFile { get; set; }

    /// <summary>
    /// Gets or sets the number of times the URL was requested.
    /// </summary>
    public int HitCount { get; set; }

    /// <summary>
    /// Gets or sets the UTC time of the first hit.
    /// </summary>
    public DateTime FirstSeen { get; set; }

    /// <summary>
    /// Gets or sets the UTC time of the last hit.
    /// </summary>
    public DateTime LastSeen { get; set; }
}
