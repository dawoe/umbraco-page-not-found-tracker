// Copyright (c) Dave Woestenborghs and contributors. Licensed under the MIT License. See LICENSE in the project root for license information.

namespace Umbraco.Community.PageNotFoundTracker.BackOffice.Models;

/// <summary>
/// The domains and cultures a block rule can be scoped to.
/// </summary>
public sealed class BlockScopeOptionsResponseModel
{
    /// <summary>
    /// Gets the configured domain names, lower-cased and sorted.
    /// </summary>
    public IEnumerable<string> Domains { get; init; } = [];

    /// <summary>
    /// Gets the language ISO codes, sorted.
    /// </summary>
    public IEnumerable<string> Cultures { get; init; } = [];
}
