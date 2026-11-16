// Copyright (c) Dave Woestenborghs and contributors. Licensed under the MIT License. See LICENSE in the project root for license information.

using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Umbraco.Community.PageNotFoundTracker.BackOffice.Models;
using Umbraco.Community.PageNotFoundTracker.Core.Tracking;

namespace Umbraco.Community.PageNotFoundTracker.BackOffice.Controllers;

/// <summary>
/// Deletes one tracked URL.
/// </summary>
/// <param name="remover">The tracked URL remover.</param>
/// <param name="persistenceState">The tracking persistence state.</param>
[ApiVersion("1.0")]
[Authorize(Policy = Constants.DashboardAccessPolicy)]
public sealed class DeleteTrackedUrlController(ITrackedUrlRemover remover, ITrackingPersistenceState persistenceState)
    : TrackedUrlsControllerBase
{
    /// <summary>
    /// Deletes the tracked URL with the given id.
    /// </summary>
    /// <param name="id">The id of the tracked URL.</param>
    /// <param name="cancellationToken">The request's cancellation token.</param>
    /// <returns>The number deleted (1), or problem details.</returns>
    [HttpDelete("tracked-urls/{id:int}")]
    [MapToApiVersion("1.0")]
    [ProducesResponseType(typeof(DeleteTrackedUrlsResponseModel), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> DeleteTrackedUrl(int id, CancellationToken cancellationToken)
    {
        if (await this.EnsurePersistenceAvailableAsync(persistenceState, cancellationToken) is { } unavailable)
        {
            return unavailable;
        }

        int deleted = await remover.DeleteAsync(new[] { id }, cancellationToken);

        return deleted > 0
            ? this.Ok(new DeleteTrackedUrlsResponseModel { Deleted = 1 })
            : this.StatusCode(
                StatusCodes.Status404NotFound,
                new ProblemDetails
                {
                    Title = "Tracked URL not found",
                    Detail = "The tracked URL does not exist (anymore).",
                    Status = StatusCodes.Status404NotFound,
                    Type = "Error",
                });
    }
}
