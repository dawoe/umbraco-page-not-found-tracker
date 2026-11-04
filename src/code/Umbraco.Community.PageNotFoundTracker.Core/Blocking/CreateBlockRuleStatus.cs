// Copyright (c) Dave Woestenborghs and contributors. Licensed under the MIT License. See LICENSE in the project root for license information.

namespace Umbraco.Community.PageNotFoundTracker.Core.Blocking;

/// <summary>
/// The result of creating a block rule.
/// </summary>
public enum CreateBlockRuleStatus
{
    /// <summary>The rule was created, or the existing rule's status code was updated.</summary>
    Saved,

    /// <summary>The pattern, status code, domain or culture is not valid; nothing was saved.</summary>
    Invalid,
}
