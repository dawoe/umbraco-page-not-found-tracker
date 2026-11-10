// Copyright (c) Dave Woestenborghs and contributors. Licensed under the MIT License. See LICENSE in the project root for license information.

using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Umbraco.Community.PageNotFoundTracker.BackOffice.Models;
using Umbraco.Community.PageNotFoundTracker.Core.Tracking;

namespace Umbraco.Community.PageNotFoundTracker.BackOffice.Controllers;

/// <summary>
/// Deletes every tracked URL that matches a search and filters (or all of them, when asked to).
/// </summary>
/// <param name="remover">The tracked URL remover.</param>
/// <param name="persistenceState">The tracking persistence state.</param>
[ApiVersion("1.0")]
[Authorize(Policy = Constants.DashboardAccessPolicy)]
public sealed class DeleteMatchingTrackedUrlsController(ITrackedUrlRemover remover, ITrackingPersistenceState persistenceState)
    : TrackedUrlsControllerBase
{
    /// <summary>
    /// Deletes the tracked URLs that match the request.
    /// </summary>
    /// <param name="request">The search and filters; with none of them <c>All</c> must be set, and with any of them it must not.</param>
    /// <param name="cancellationToken">The request's cancellation token.</param>
    /// <returns>The number deleted, or problem details.</returns>
    [HttpPost("tracked-urls/delete-matching")]
    [MapToApiVersion("1.0")]
    [ProducesResponseType(typeof(DeleteTrackedUrlsResponseModel), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> DeleteMatchingTrackedUrls([FromBody] DeleteMatchingTrackedUrlsRequestModel request, CancellationToken cancellationToken)
    {
        if (request is null)
        {
            return this.InvalidRequest("Invalid request", "A request body is required.");
        }

        // An empty string means "no value", as an empty query parameter does in the list.
        string? domain = string.IsNullOrEmpty(request.Domain) ? null : request.Domain;
        string? culture = string.IsNullOrEmpty(request.Culture) ? null : request.Culture;

        if (domain is not null && request.NoDomain)
        {
            return this.InvalidRequest("Contradicting domain filter", "Use either domain or noDomain, not both.");
        }

        if (culture is not null && request.NoCulture)
        {
            return this.InvalidRequest("Contradicting culture filter", "Use either culture or noCulture, not both.");
        }

        bool hasFilter = !string.IsNullOrWhiteSpace(request.Search) || domain is not null || request.NoDomain || culture is not null || request.NoCulture;

        if (request.All && hasFilter)
        {
            return this.InvalidRequest("Contradicting request", "All cannot be combined with a search or filter.");
        }

        if (!request.All && !hasFilter)
        {
            return this.InvalidRequest("No filter", "Provide a search or filter, or set all to delete every tracked URL.");
        }

        if (await this.EnsurePersistenceAvailableAsync(persistenceState, cancellationToken) is { } unavailable)
        {
            return unavailable;
        }

        var criteria = new TrackedUrlCriteria(request.Search, ToFilter(domain, request.NoDomain), ToFilter(culture, request.NoCulture));
        int deleted = await remover.DeleteMatchingAsync(criteria, cancellationToken);
        return this.Ok(new DeleteTrackedUrlsResponseModel { Deleted = deleted });
    }
}
