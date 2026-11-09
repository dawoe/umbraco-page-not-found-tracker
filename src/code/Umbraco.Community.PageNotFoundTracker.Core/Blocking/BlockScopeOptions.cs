// Copyright (c) Dave Woestenborghs and contributors. Licensed under the MIT License. See LICENSE in the project root for license information.

namespace Umbraco.Community.PageNotFoundTracker.Core.Blocking;

/// <summary>
/// The domains and cultures a block rule can be scoped to, as configured in Umbraco.
/// </summary>
/// <param name="Domains">The domain names, lower-cased, distinct and sorted (ordinal).</param>
/// <param name="Cultures">The language ISO codes, sorted (ordinal).</param>
public sealed record BlockScopeOptions(IReadOnlyList<string> Domains, IReadOnlyList<string> Cultures);
