// Copyright (c) Dave Woestenborghs and contributors. Licensed under the MIT License. See LICENSE in the project root for license information.

namespace Umbraco.Community.PageNotFoundTracker.Core.Blocking;

/// <summary>
/// A block rule change sent to every server, so each rebuilds its rule set and, for a saved rule, discards its own
/// queued hits the rule covers.
/// </summary>
/// <param name="Pattern">The normalised pattern.</param>
/// <param name="Domain">The rule's domain, empty for all domains.</param>
/// <param name="Culture">The rule's culture, empty for all cultures.</param>
/// <param name="Change">What happened to the rule.</param>
public sealed record BlockRuleCachePayload(string Pattern, string Domain, string Culture, BlockRuleChange Change);
