// Copyright (c) Dave Woestenborghs and contributors. Licensed under the MIT License. See LICENSE in the project root for license information.

namespace Umbraco.Community.PageNotFoundTracker.BackOffice.Models;

/// <summary>
/// The body of a delete matching tracked URLs request: the same search and filters as the list.
/// </summary>
public sealed class DeleteMatchingTrackedUrlsRequestModel
{
    /// <summary>
    /// Gets the search term; blank means no search.
    /// </summary>
    public string? Search { get; init; }

    /// <summary>
    /// Gets the exact domain to match; cannot be combined with <see cref="NoDomain"/>.
    /// </summary>
    public string? Domain { get; init; }

    /// <summary>
    /// Gets a value indicating whether only URLs without a domain match.
    /// </summary>
    public bool NoDomain { get; init; }

    /// <summary>
    /// Gets the exact culture to match; cannot be combined with <see cref="NoCulture"/>.
    /// </summary>
    public string? Culture { get; init; }

    /// <summary>
    /// Gets a value indicating whether only URLs without a culture match.
    /// </summary>
    public bool NoCulture { get; init; }

    /// <summary>
    /// Gets a value indicating whether every tracked URL is deleted. Required when there is no filter, and not allowed
    /// together with one.
    /// </summary>
    public bool All { get; init; }
}
