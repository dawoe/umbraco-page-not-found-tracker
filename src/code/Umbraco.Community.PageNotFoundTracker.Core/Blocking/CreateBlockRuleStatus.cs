// Copyright (c) Dave Woestenborghs and contributors. Licensed under the MIT License. See LICENSE in the project root for license information.

namespace Umbraco.Community.PageNotFoundTracker.Core.Blocking;

/// <summary>
/// The result of creating or updating a block rule. <see cref="IBlockRuleService.CreateAsync"/> returns
/// <see cref="Saved"/> or <see cref="Invalid"/> only; <see cref="IBlockRuleService.UpdateAsync"/> returns any of them.
/// </summary>
public enum CreateBlockRuleStatus
{
    /// <summary>The rule was created or changed, or the existing rule's status code was updated.</summary>
    Saved,

    /// <summary>The pattern, status code, domain or culture is not valid; nothing was saved.</summary>
    Invalid,

    /// <summary>No rule with that id; nothing changed. Only from <see cref="IBlockRuleService.UpdateAsync"/>.</summary>
    NotFound,

    /// <summary>
    /// Another rule has the new pattern, domain and culture; nothing changed. Only from
    /// <see cref="IBlockRuleService.UpdateAsync"/>.
    /// </summary>
    Conflict,
}
