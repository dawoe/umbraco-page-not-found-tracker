// Copyright (c) Dave Woestenborghs and contributors. Licensed under the MIT License. See LICENSE in the project root for license information.

namespace Umbraco.Community.PageNotFoundTracker.Core.Blocking;

/// <summary>
/// Lists, creates, updates and removes block rules. Replaceable: register another implementation after the package's composer.
/// </summary>
public interface IBlockRuleService
{
    /// <summary>
    /// Gets one page of block rules. Filtering, ordering, paging and counting happen in the database.
    /// </summary>
    /// <param name="query">The query.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The page.</returns>
    Task<BlockRulePage> QueryAsync(BlockRuleQuery query, CancellationToken cancellationToken);

    /// <summary>
    /// Creates a rule, or updates the status code of the existing rule with the same normalised pattern, domain and
    /// culture. Then the rule sets are refreshed and the tracked records and queued hits the rule covers are removed.
    /// </summary>
    /// <param name="pattern">The pattern as entered (a path with <c>*</c> as wildcard).</param>
    /// <param name="statusCode">The response status code (400 to 499).</param>
    /// <param name="domain">The domain name, empty for all domains.</param>
    /// <param name="culture">
    /// The culture, empty for all cultures. Case does not matter: it is stored as the matching Umbraco language ISO code
    /// (else the .NET culture name, else lower-cased), so <c>en-us</c> and <c>en-US</c> are the same rule.
    /// </param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>
    /// The outcome: <see cref="CreateBlockRuleStatus.Saved"/> or <see cref="CreateBlockRuleStatus.Invalid"/> only.
    /// </returns>
    Task<CreateBlockRuleOutcome> CreateAsync(string pattern, int statusCode, string domain, string culture, CancellationToken cancellationToken);

    /// <summary>
    /// Changes an existing rule in place: its pattern, status code, domain and culture; the id and the creation date stay.
    /// The input is normalised, canonicalised and validated as by <see cref="CreateAsync"/>, before the database is read,
    /// so an invalid request changes nothing. Then the rule sets are refreshed and the tracked records and queued hits the
    /// changed rule covers are removed.
    /// </summary>
    /// <param name="id">The rule id.</param>
    /// <param name="pattern">The pattern as entered (a path with <c>*</c> as wildcard).</param>
    /// <param name="statusCode">The response status code (400 to 499).</param>
    /// <param name="domain">The domain name, empty for all domains.</param>
    /// <param name="culture">The culture, empty for all cultures; canonicalised as by <see cref="CreateAsync"/>.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>
    /// The outcome: <see cref="CreateBlockRuleStatus.Saved"/>, <see cref="CreateBlockRuleStatus.Invalid"/>,
    /// <see cref="CreateBlockRuleStatus.NotFound"/> (no rule with that id) or <see cref="CreateBlockRuleStatus.Conflict"/>
    /// (another rule already has the new pattern, domain and culture; the message is
    /// <see cref="BlockingConstants.RuleExistsMessage"/>).
    /// </returns>
    Task<CreateBlockRuleOutcome> UpdateAsync(int id, string pattern, int statusCode, string domain, string culture, CancellationToken cancellationToken);

    /// <summary>
    /// Removes a rule and refreshes the rule sets.
    /// </summary>
    /// <param name="id">The rule id.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns><c>true</c> when the rule was removed, <c>false</c> when there is no rule with that id.</returns>
    Task<bool> DeleteAsync(int id, CancellationToken cancellationToken);

    /// <summary>
    /// Gets the domains and cultures configured in Umbraco, for the rule dialog.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The options.</returns>
    Task<BlockScopeOptions> GetScopeOptionsAsync(CancellationToken cancellationToken);
}
