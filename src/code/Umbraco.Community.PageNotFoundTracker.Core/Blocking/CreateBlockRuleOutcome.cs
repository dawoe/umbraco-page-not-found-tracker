// Copyright (c) Dave Woestenborghs and contributors. Licensed under the MIT License. See LICENSE in the project root for license information.

namespace Umbraco.Community.PageNotFoundTracker.Core.Blocking;

/// <summary>
/// The outcome of <see cref="IBlockRuleService.CreateAsync"/>.
/// </summary>
/// <param name="Status">The status.</param>
/// <param name="Message">The message for the editor: why the rule is invalid, or null.</param>
/// <param name="RecordsRemoved">
/// Whether the tracked records the rule covers were removed. <c>false</c> for an invalid rule, and for a saved rule whose
/// delete failed (the rule stays; the failure is logged).
/// </param>
public sealed record CreateBlockRuleOutcome(CreateBlockRuleStatus Status, string? Message, bool RecordsRemoved);
