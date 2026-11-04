// Copyright (c) Dave Woestenborghs and contributors. Licensed under the MIT License. See LICENSE in the project root for license information.

namespace Umbraco.Community.PageNotFoundTracker.Core.Blocking;

/// <summary>
/// What happened to a block rule.
/// </summary>
public enum BlockRuleChange
{
    /// <summary>The rule was created or its status code updated.</summary>
    Saved,

    /// <summary>The rule was removed.</summary>
    Removed,
}
