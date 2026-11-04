// Copyright (c) Dave Woestenborghs and contributors. Licensed under the MIT License. See LICENSE in the project root for license information.

using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Umbraco.Cms.Core;
using Umbraco.Community.PageNotFoundTracker.BackOffice.Models;
using Umbraco.Community.PageNotFoundTracker.Core.Redirects;
using Umbraco.Community.PageNotFoundTracker.Core.Tracking;

namespace Umbraco.Community.PageNotFoundTracker.BackOffice.Controllers;

/// <summary>
/// Creates a redirect for a tracked URL and removes the tracked URL.
/// </summary>
/// <param name="redirectService">The redirect service.</param>
/// <param name="persistenceState">The tracking persistence state.</param>
[ApiVersion("1.0")]
[Authorize(Policy = Constants.DashboardAccessPolicy)]
public sealed class CreateTrackedUrlRedirectController(ITrackedUrlRedirectService redirectService, ITrackingPersistenceState persistenceState)
    : TrackedUrlsControllerBase
{
    /// <summary>
    /// Creates a redirect for the tracked URL with the given id.
    /// </summary>
    /// <param name="id">The id of the tracked URL.</param>
    /// <param name="request">The request body; may be missing.</param>
    /// <param name="cancellationToken">The request's cancellation token.</param>
    /// <returns>The outcome, or problem details.</returns>
    [HttpPost("tracked-urls/{id:int}/redirect")]
    [MapToApiVersion("1.0")]
    [ProducesResponseType(typeof(CreateRedirectResponseModel), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> CreateTrackedUrlRedirect(int id, CreateRedirectRequestModel? request, CancellationToken cancellationToken)
    {
        if (await this.EnsurePersistenceAvailableAsync(persistenceState, cancellationToken) is { } unavailable)
        {
            return unavailable;
        }

        CreateRedirectOutcome outcome = await redirectService.CreateAsync(id, request?.Payload, cancellationToken);

        return outcome.Status switch
        {
            CreateRedirectStatus.Created => this.Ok(new CreateRedirectResponseModel { RecordRemoved = true }),
            CreateRedirectStatus.CreatedRecordNotRemoved => this.Ok(new CreateRedirectResponseModel { RecordRemoved = false, Message = outcome.Message }),
            CreateRedirectStatus.NotFound => this.Problem(StatusCodes.Status404NotFound, "Tracked URL not found", outcome.Message ?? "The tracked URL does not exist (anymore)."),
            CreateRedirectStatus.NotSupported => this.Problem(StatusCodes.Status409Conflict, "Redirect not supported", outcome.Message ?? "A redirect cannot be created for this URL."),
            _ => this.Problem(StatusCodes.Status422UnprocessableEntity, "Redirect not created", outcome.Message ?? "The redirect could not be created."),
        };
    }

    private ObjectResult Problem(int status, string title, string detail) =>
        this.StatusCode(status, new ProblemDetails { Title = title, Detail = detail, Status = status, Type = "Error" });
}
