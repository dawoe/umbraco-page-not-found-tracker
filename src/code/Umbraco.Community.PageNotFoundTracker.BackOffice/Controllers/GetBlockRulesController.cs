// Copyright (c) Dave Woestenborghs and contributors. Licensed under the MIT License. See LICENSE in the project root for license information.

using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Umbraco.Cms.Api.Common.ViewModels.Pagination;
using Umbraco.Cms.Core;
using Umbraco.Community.PageNotFoundTracker.BackOffice.Models;
using Umbraco.Community.PageNotFoundTracker.Core.Blocking;
using Umbraco.Community.PageNotFoundTracker.Core.Tracking;

namespace Umbraco.Community.PageNotFoundTracker.BackOffice.Controllers;

/// <summary>
/// Lists the block rules: one page, filtered, searched and sorted in the database.
/// </summary>
/// <param name="ruleService">The block rule service.</param>
/// <param name="persistenceState">The tracking persistence state.</param>
[ApiVersion("1.0")]
[Authorize(Policy = Constants.DashboardAccessPolicy)]
public sealed class GetBlockRulesController(IBlockRuleService ruleService, ITrackingPersistenceState persistenceState)
    : TrackedUrlsControllerBase
{
    /// <summary>
    /// Gets one page of block rules.
    /// </summary>
    /// <param name="cancellationToken">The request's cancellation token.</param>
    /// <param name="skip">The number of rows to skip; 0 or more.</param>
    /// <param name="take">The page size, 1 to 100.</param>
    /// <param name="orderBy">The column to sort on; ties are broken by the identifier.</param>
    /// <param name="direction">The sort direction.</param>
    /// <param name="search">A case-insensitive "contains" term for the pattern.</param>
    /// <param name="domain">Only rules with exactly this domain; cannot be combined with <paramref name="noDomain"/>.</param>
    /// <param name="noDomain">Only rules without a domain.</param>
    /// <param name="culture">Only rules with exactly this culture; cannot be combined with <paramref name="noCulture"/>.</param>
    /// <param name="noCulture">Only rules without a culture.</param>
    /// <returns>The page, or problem details.</returns>
    [HttpGet("block-rules")]
    [MapToApiVersion("1.0")]
    [ProducesResponseType(typeof(PagedViewModel<BlockRuleResponseModel>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> GetBlockRules(
        CancellationToken cancellationToken,
        int skip = 0,
        int take = 20,
        BlockRuleSortColumn orderBy = BlockRuleSortColumn.Created,
        Direction direction = Direction.Descending,
        string? search = null,
        string? domain = null,
        bool noDomain = false,
        string? culture = null,
        bool noCulture = false)
    {
        if (skip < 0)
        {
            return this.InvalidRequest("Invalid skip", "Skip must be 0 or more.");
        }

        if (take is < 1 or > GetTrackedUrlsController.MaxTake)
        {
            return this.InvalidRequest("Invalid take", $"Take must be between 1 and {GetTrackedUrlsController.MaxTake}.");
        }

        if (domain is not null && noDomain)
        {
            return this.InvalidRequest("Contradicting domain filter", "Use either domain or noDomain, not both.");
        }

        if (culture is not null && noCulture)
        {
            return this.InvalidRequest("Contradicting culture filter", "Use either culture or noCulture, not both.");
        }

        if (await this.EnsurePersistenceAvailableAsync(persistenceState, cancellationToken) is { } unavailable)
        {
            return unavailable;
        }

        var query = new BlockRuleQuery(
            skip,
            take,
            orderBy,
            direction == Direction.Descending,
            search,
            ToFilter(domain, noDomain),
            ToFilter(culture, noCulture));
        BlockRulePage page = await ruleService.QueryAsync(query, cancellationToken);

        return this.Ok(new PagedViewModel<BlockRuleResponseModel>
        {
            Total = page.Total,
            Items = page.Items.Select(Map).ToList(),
        });
    }

    private static TrackedUrlFilter ToFilter(string? value, bool empty) =>
        empty ? TrackedUrlFilter.Empty : value is null ? TrackedUrlFilter.Any : TrackedUrlFilter.Exactly(value);

    private static BlockRuleResponseModel Map(BlockRule rule) => new()
    {
        Id = rule.Id,
        Pattern = rule.DisplayPattern,
        StatusCode = rule.StatusCode,
        Domain = rule.Domain,
        Culture = rule.Culture,

        // Stored as UTC; providers such as SQLite return the value without its kind.
        Created = DateTime.SpecifyKind(rule.Created, DateTimeKind.Utc),
    };
}
