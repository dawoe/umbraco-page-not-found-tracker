// Copyright (c) Dave Woestenborghs and contributors. Licensed under the MIT License. See LICENSE in the project root for license information.

namespace Umbraco.Community.PageNotFoundTracker.BackOffice.Models;

/// <summary>
/// A block rule as the Management API returns it.
/// </summary>
public sealed class BlockRuleResponseModel
{
    /// <summary>
    /// Gets the identifier.
    /// </summary>
    public int Id { get; init; }

    /// <summary>
    /// Gets the pattern as entered by the editor.
    /// </summary>
    public string Pattern { get; init; } = string.Empty;

    /// <summary>
    /// Gets the response status code.
    /// </summary>
    public int StatusCode { get; init; }

    /// <summary>
    /// Gets the domain the rule applies to, empty for all.
    /// </summary>
    public string Domain { get; init; } = string.Empty;

    /// <summary>
    /// Gets the culture the rule applies to, empty for all.
    /// </summary>
    public string Culture { get; init; } = string.Empty;

    /// <summary>
    /// Gets the UTC time the rule was created (serialised as ISO 8601 with <c>Z</c>).
    /// </summary>
    public DateTime Created { get; init; }
}
