// Copyright (c) Dave Woestenborghs and contributors. Licensed under the MIT License. See LICENSE in the project root for license information.

using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Umbraco.Community.PageNotFoundTracker.Core.Blocking;
using Umbraco.Community.PageNotFoundTracker.Core.Tracking;

namespace Umbraco.Community.PageNotFoundTracker.BackOffice.Controllers;

/// <summary>
/// Removes a block rule.
/// </summary>
/// <param name="ruleService">The block rule service.</param>
/// <param name="persistenceState">The tracking persistence state.</param>
[ApiVersion("1.0")]
[Authorize(Policy = Constants.DashboardAccessPolicy)]
public sealed class DeleteBlockRuleController(IBlockRuleService ruleService, ITrackingPersistenceState persistenceState)
    : TrackedUrlsControllerBase
{
    /// <summary>
    /// Removes the block rule with the given id.
    /// </summary>
    /// <param name="id">The id of the rule.</param>
    /// <param name="cancellationToken">The request's cancellation token.</param>
    /// <returns>An empty 200, or problem details.</returns>
    [HttpDelete("block-rules/{id:int}")]
    [MapToApiVersion("1.0")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> DeleteBlockRule(int id, CancellationToken cancellationToken)
    {
        if (await this.EnsurePersistenceAvailableAsync(persistenceState, cancellationToken) is { } unavailable)
        {
            return unavailable;
        }

        return await ruleService.DeleteAsync(id, cancellationToken)
            ? this.Ok()
            : this.StatusCode(
                StatusCodes.Status404NotFound,
                new ProblemDetails
                {
                    Title = "Block rule not found",
                    Detail = "The block rule does not exist (anymore).",
                    Status = StatusCodes.Status404NotFound,
                    Type = "Error",
                });
    }
}
