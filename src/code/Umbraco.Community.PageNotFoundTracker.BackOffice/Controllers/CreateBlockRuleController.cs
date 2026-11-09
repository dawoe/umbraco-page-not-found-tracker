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
/// Creates a block rule (or updates the status code of an existing one) and removes the tracked URLs it covers.
/// </summary>
/// <param name="ruleService">The block rule service.</param>
/// <param name="persistenceState">The tracking persistence state.</param>
[ApiVersion("1.0")]
[Authorize(Policy = Constants.DashboardAccessPolicy)]
public sealed class CreateBlockRuleController(IBlockRuleService ruleService, ITrackingPersistenceState persistenceState)
    : TrackedUrlsControllerBase
{
    /// <summary>
    /// Creates a block rule.
    /// </summary>
    /// <param name="request">The request body.</param>
    /// <param name="cancellationToken">The request's cancellation token.</param>
    /// <returns>The outcome, or problem details.</returns>
    [HttpPost("block-rules")]
    [MapToApiVersion("1.0")]
    [ProducesResponseType(typeof(CreateBlockRuleResponseModel), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> CreateBlockRule(CreateBlockRuleRequestModel? request, CancellationToken cancellationToken)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.Pattern))
        {
            return this.InvalidRequest("Invalid block rule", "The pattern is required.");
        }

        if (await this.EnsurePersistenceAvailableAsync(persistenceState, cancellationToken) is { } unavailable)
        {
            return unavailable;
        }

        CreateBlockRuleOutcome outcome = await ruleService.CreateAsync(
            request.Pattern,
            request.StatusCode,
            request.Domain ?? string.Empty,
            request.Culture ?? string.Empty,
            cancellationToken);

        return outcome.Status == CreateBlockRuleStatus.Saved
            ? this.Ok(new CreateBlockRuleResponseModel
            {
                RecordsRemoved = outcome.RecordsRemoved,
                Message = outcome.Message ?? (outcome.RecordsRemoved ? null : BlockingConstants.RecordsNotRemovedMessage),
            })
            : this.InvalidRequest("Invalid block rule", outcome.Message ?? "The block rule is not valid.");
    }
}
