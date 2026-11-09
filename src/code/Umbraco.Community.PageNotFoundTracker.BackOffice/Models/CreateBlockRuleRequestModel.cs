// Copyright (c) Dave Woestenborghs and contributors. Licensed under the MIT License. See LICENSE in the project root for license information.

namespace Umbraco.Community.PageNotFoundTracker.BackOffice.Models;

/// <summary>
/// The body of a create block rule request.
/// </summary>
public sealed class CreateBlockRuleRequestModel
{
    /// <summary>
    /// Gets the pattern: a path with <c>*</c> as wildcard.
    /// </summary>
    public string? Pattern { get; init; }

    /// <summary>
    /// Gets the response status code, 400 to 499.
    /// </summary>
    public int StatusCode { get; init; }

    /// <summary>
    /// Gets the domain the rule applies to; empty or absent for all.
    /// </summary>
    public string? Domain { get; init; }

    /// <summary>
    /// Gets the culture the rule applies to; empty or absent for all.
    /// </summary>
    public string? Culture { get; init; }
}
