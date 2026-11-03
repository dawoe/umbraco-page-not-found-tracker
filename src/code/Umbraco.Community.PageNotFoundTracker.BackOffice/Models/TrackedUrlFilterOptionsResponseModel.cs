// Copyright (c) Dave Woestenborghs and contributors. Licensed under the MIT License. See LICENSE in the project root for license information.

namespace Umbraco.Community.PageNotFoundTracker.BackOffice.Models;

/// <summary>
/// The values the domain and culture filters of the dashboard can offer.
/// </summary>
public sealed class TrackedUrlFilterOptionsResponseModel
{
    /// <summary>
    /// Gets the distinct non-empty domains, sorted ordinal.
    /// </summary>
    public IEnumerable<string> Domains { get; init; } = [];

    /// <summary>
    /// Gets the distinct non-empty cultures, sorted ordinal.
    /// </summary>
    public IEnumerable<string> Cultures { get; init; } = [];

    /// <summary>
    /// Gets a value indicating whether any tracked URL has no domain (offer the "no domain" choice).
    /// </summary>
    public bool HasEmptyDomain { get; init; }

    /// <summary>
    /// Gets a value indicating whether any tracked URL has no culture (offer the "no culture" choice).
    /// </summary>
    public bool HasEmptyCulture { get; init; }
}
