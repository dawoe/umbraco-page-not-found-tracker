// Copyright (c) Dave Woestenborghs and contributors. Licensed under the MIT License. See LICENSE in the project root for license information.

using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Umbraco.Community.PageNotFoundTracker.BackOffice.Models;
using Umbraco.Community.PageNotFoundTracker.Core.Tracking;

namespace Umbraco.Community.PageNotFoundTracker.BackOffice.Controllers;

/// <summary>
/// Returns the values the dashboard's domain and culture filters can offer.
/// </summary>
/// <param name="queryService">The tracked URL query service.</param>
/// <param name="persistenceState">The tracking persistence state.</param>
[ApiVersion("1.0")]
[Authorize(Policy = Constants.DashboardAccessPolicy)]
public sealed class GetTrackedUrlFilterOptionsController(ITrackedUrlQueryService queryService, ITrackingPersistenceState persistenceState)
    : TrackedUrlsControllerBase
{
    /// <summary>
    /// Gets the distinct domains and cultures of the tracked URLs, and whether any have none.
    /// </summary>
    /// <param name="cancellationToken">The request's cancellation token.</param>
    /// <returns>The filter options, or problem details.</returns>
    [HttpGet("tracked-urls/filter-options")]
    [MapToApiVersion("1.0")]
    [ProducesResponseType(typeof(TrackedUrlFilterOptionsResponseModel), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> GetTrackedUrlFilterOptions(CancellationToken cancellationToken)
    {
        if (await this.EnsurePersistenceAvailableAsync(persistenceState, cancellationToken) is { } unavailable)
        {
            return unavailable;
        }

        TrackedUrlFilterOptions options = await queryService.GetFilterOptionsAsync(cancellationToken);
        return this.Ok(new TrackedUrlFilterOptionsResponseModel
        {
            Domains = options.Domains,
            Cultures = options.Cultures,
            HasEmptyDomain = options.HasEmptyDomain,
            HasEmptyCulture = options.HasEmptyCulture,
        });
    }
}
