// Copyright (c) Dave Woestenborghs and contributors. Licensed under the MIT License. See LICENSE in the project root for license information.

using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Umbraco.Community.PageNotFoundTracker.BackOffice.Models;
using Umbraco.Community.PageNotFoundTracker.Core.Tracking;

namespace Umbraco.Community.PageNotFoundTracker.BackOffice.Controllers;

/// <summary>
/// Deletes a set of tracked URLs by id (the selection on the page).
/// </summary>
/// <param name="remover">The tracked URL remover.</param>
/// <param name="persistenceState">The tracking persistence state.</param>
[ApiVersion("1.0")]
[Authorize(Policy = Constants.DashboardAccessPolicy)]
public sealed class DeleteTrackedUrlsController(ITrackedUrlRemover remover, ITrackingPersistenceState persistenceState)
    : TrackedUrlsControllerBase
{
    /// <summary>
    /// The most distinct ids one request may carry (the largest page size).
    /// </summary>
    internal const int MaxIds = 100;

    /// <summary>
    /// Deletes the tracked URLs with the given ids.
    /// </summary>
    /// <param name="request">The ids: 1 to 100 distinct values, each 1 or more.</param>
    /// <param name="cancellationToken">The request's cancellation token.</param>
    /// <returns>The number deleted, or problem details.</returns>
    [HttpPost("tracked-urls/delete")]
    [MapToApiVersion("1.0")]
    [ProducesResponseType(typeof(DeleteTrackedUrlsResponseModel), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> DeleteTrackedUrls([FromBody] DeleteTrackedUrlsRequestModel request, CancellationToken cancellationToken)
    {
        int[] ids = request?.Ids?.Distinct().ToArray() ?? [];

        if (ids.Length == 0)
        {
            return this.InvalidRequest("No ids", "Provide at least one id.");
        }

        if (ids.Any(id => id < 1))
        {
            return this.InvalidRequest("Invalid id", "Every id must be 1 or more.");
        }

        if (ids.Length > MaxIds)
        {
            return this.InvalidRequest("Too many ids", $"Provide at most {MaxIds} ids.");
        }

        if (await this.EnsurePersistenceAvailableAsync(persistenceState, cancellationToken) is { } unavailable)
        {
            return unavailable;
        }

        int deleted = await remover.DeleteAsync(ids, cancellationToken);
        return this.Ok(new DeleteTrackedUrlsResponseModel { Deleted = deleted });
    }
}
