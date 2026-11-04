// Copyright (c) Dave Woestenborghs and contributors. Licensed under the MIT License. See LICENSE in the project root for license information.

namespace Umbraco.Community.PageNotFoundTracker.BackOffice.Models;

/// <summary>
/// A tracked URL as the Management API returns it.
/// </summary>
public sealed class TrackedUrlResponseModel
{
    /// <summary>
    /// Gets the identifier.
    /// </summary>
    public int Id { get; init; }

    /// <summary>
    /// Gets the URL as shown to editors (the first-seen spelling).
    /// </summary>
    public string Url { get; init; } = string.Empty;

    /// <summary>
    /// Gets the path relative to the matched domain. Not a list column; row actions use it.
    /// </summary>
    public string RelativePath { get; init; } = string.Empty;

    /// <summary>
    /// Gets the matched domain, empty when none.
    /// </summary>
    public string Domain { get; init; } = string.Empty;

    /// <summary>
    /// Gets the culture, empty when none.
    /// </summary>
    public string Culture { get; init; } = string.Empty;

    /// <summary>
    /// Gets a value indicating whether the request was for a file.
    /// </summary>
    public bool IsFile { get; init; }

    /// <summary>
    /// Gets the number of times the URL was requested.
    /// </summary>
    public long HitCount { get; init; }

    /// <summary>
    /// Gets the UTC time of the first hit (serialised as ISO 8601 with <c>Z</c>).
    /// </summary>
    public DateTime FirstSeen { get; init; }

    /// <summary>
    /// Gets the UTC time of the last hit (serialised as ISO 8601 with <c>Z</c>).
    /// </summary>
    public DateTime LastSeen { get; init; }

    /// <summary>
    /// Gets a value indicating whether the active redirect provider can create a redirect for this URL.
    /// </summary>
    public bool CanCreateRedirect { get; init; }
}
