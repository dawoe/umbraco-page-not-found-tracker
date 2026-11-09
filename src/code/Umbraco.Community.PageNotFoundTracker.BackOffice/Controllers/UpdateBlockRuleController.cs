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
/// Changes an existing block rule in place and removes the tracked URLs it now covers.
/// </summary>
/// <param name="ruleService">The block rule service.</param>
/// <param name="persistenceState">The tracking persistence state.</param>
[ApiVersion("1.0")]
[Authorize(Policy = Constants.DashboardAccessPolicy)]
public sealed class UpdateBlockRuleController(IBlockRuleService ruleService, ITrackingPersistenceState persistenceState)
    : TrackedUrlsControllerBase
{
    /// <summary>
    /// Updates the block rule with the given id.
    /// </summary>
    /// <param name="id">The id of the rule.</param>
    /// <param name="request">The request body.</param>
    /// <param name="cancellationToken">The request's cancellation token.</param>
    /// <returns>The outcome, or problem details.</returns>
    [HttpPut("block-rules/{id:int}")]
    [MapToApiVersion("1.0")]
    [ProducesResponseType(typeof(CreateBlockRuleResponseModel), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> UpdateBlockRule(int id, CreateBlockRuleRequestModel? request, CancellationToken cancellationToken)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.Pattern))
        {
            return this.InvalidRequest("Invalid block rule", "The pattern is required.");
        }

        if (await this.EnsurePersistenceAvailableAsync(persistenceState, cancellationToken) is { } unavailable)
        {
            return unavailable;
        }

        CreateBlockRuleOutcome outcome = await ruleService.UpdateAsync(
            id,
            request.Pattern,
            request.StatusCode,
            request.Domain ?? string.Empty,
            request.Culture ?? string.Empty,
            cancellationToken);

        return outcome.Status switch
        {
            CreateBlockRuleStatus.Saved => this.Ok(new CreateBlockRuleResponseModel
            {
                RecordsRemoved = outcome.RecordsRemoved,
                Message = outcome.Message ?? (outcome.RecordsRemoved ? null : BlockingConstants.RecordsNotRemovedMessage),
            }),
            CreateBlockRuleStatus.NotFound => this.Problem(StatusCodes.Status404NotFound, "Block rule not found", "The block rule does not exist (anymore)."),
            CreateBlockRuleStatus.Conflict => this.Problem(StatusCodes.Status409Conflict, "Block rule exists", outcome.Message ?? BlockingConstants.RuleExistsMessage),
            _ => this.InvalidRequest("Invalid block rule", outcome.Message ?? "The block rule is not valid."),
        };
    }

    private ObjectResult Problem(int status, string title, string detail) =>
        this.StatusCode(status, new ProblemDetails { Title = title, Detail = detail, Status = status, Type = "Error" });
}
