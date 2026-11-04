// Copyright (c) Dave Woestenborghs and contributors. Licensed under the MIT License. See LICENSE in the project root for license information.

using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Umbraco.Community.PageNotFoundTracker.BackOffice.Models;
using Umbraco.Community.PageNotFoundTracker.Core.Blocking;
using Umbraco.Community.PageNotFoundTracker.Core.Tracking;

namespace Umbraco.Community.PageNotFoundTracker.BackOffice.Controllers;

/// <summary>
/// Returns the domains and cultures a block rule can be scoped to.
/// </summary>
/// <param name="ruleService">The block rule service.</param>
/// <param name="persistenceState">The tracking persistence state.</param>
[ApiVersion("1.0")]
[Authorize(Policy = Constants.DashboardAccessPolicy)]
public sealed class GetBlockScopeOptionsController(IBlockRuleService ruleService, ITrackingPersistenceState persistenceState)
    : TrackedUrlsControllerBase
{
    /// <summary>
    /// Gets the domains and languages configured in Umbraco.
    /// </summary>
    /// <param name="cancellationToken">The request's cancellation token.</param>
    /// <returns>The options, or problem details.</returns>
    [HttpGet("block-rules/scope-options")]
    [MapToApiVersion("1.0")]
    [ProducesResponseType(typeof(BlockScopeOptionsResponseModel), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> GetBlockScopeOptions(CancellationToken cancellationToken)
    {
        if (await this.EnsurePersistenceAvailableAsync(persistenceState, cancellationToken) is { } unavailable)
        {
            return unavailable;
        }

        BlockScopeOptions options = await ruleService.GetScopeOptionsAsync(cancellationToken);
        return this.Ok(new BlockScopeOptionsResponseModel { Domains = options.Domains, Cultures = options.Cultures });
    }
}
