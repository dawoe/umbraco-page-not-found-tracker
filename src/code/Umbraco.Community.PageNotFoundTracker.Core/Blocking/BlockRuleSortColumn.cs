// Copyright (c) Dave Woestenborghs and contributors. Licensed under the MIT License. See LICENSE in the project root for license information.

namespace Umbraco.Community.PageNotFoundTracker.Core.Blocking;

/// <summary>
/// The columns the block rule list can be sorted on.
/// </summary>
public enum BlockRuleSortColumn
{
    /// <summary>The creation time (the default).</summary>
    Created = 0,

    /// <summary>The normalised pattern.</summary>
    Pattern,

    /// <summary>The response status code.</summary>
    StatusCode,

    /// <summary>The domain.</summary>
    Domain,

    /// <summary>The culture.</summary>
    Culture,
}
