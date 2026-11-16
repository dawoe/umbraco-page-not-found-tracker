// Copyright (c) Dave Woestenborghs and contributors. Licensed under the MIT License. See LICENSE in the project root for license information.

using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Umbraco.Cms.Core;
using Umbraco.Community.PageNotFoundTracker.BackOffice.Export;
using Umbraco.Community.PageNotFoundTracker.Core.Tracking;

namespace Umbraco.Community.PageNotFoundTracker.BackOffice.Controllers;

/// <summary>
/// Exports every tracked URL matching the search, filters and sort as a CSV file that Excel opens by double-click. The
/// rows are streamed from the database to the response, so a large table never sits in memory.
/// </summary>
/// <param name="exporter">The tracked URL exporter.</param>
/// <param name="persistenceState">The tracking persistence state.</param>
/// <param name="timeProvider">The time provider, for the file name.</param>
/// <param name="logger">The logger.</param>
[ApiVersion("1.0")]
[Authorize(Policy = Constants.DashboardAccessPolicy)]
public sealed class ExportTrackedUrlsController(
    ITrackedUrlExporter exporter,
    ITrackingPersistenceState persistenceState,
    TimeProvider timeProvider,
    ILogger<ExportTrackedUrlsController> logger)
    : TrackedUrlsControllerBase
{
    /// <summary>
    /// Exports the tracked URLs as CSV.
    /// </summary>
    /// <param name="cancellationToken">The request's cancellation token.</param>
    /// <param name="orderBy">The column to sort on; ties are broken by the identifier.</param>
    /// <param name="direction">The sort direction.</param>
    /// <param name="search">A case-insensitive "contains" term for the URL.</param>
    /// <param name="domain">Only URLs with exactly this domain; cannot be combined with <paramref name="noDomain"/>.</param>
    /// <param name="noDomain">Only URLs without a domain.</param>
    /// <param name="culture">Only URLs with exactly this culture; cannot be combined with <paramref name="noCulture"/>.</param>
    /// <param name="noCulture">Only URLs without a culture.</param>
    /// <returns>Problem details before any byte is written; otherwise a result that streams the CSV to the response.</returns>
    [HttpGet("tracked-urls/export")]
    [MapToApiVersion("1.0")]
    [ProducesResponseType(typeof(FileResult), StatusCodes.Status200OK, "text/csv")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> ExportTrackedUrls(
        CancellationToken cancellationToken,
        TrackedUrlSortColumn orderBy = TrackedUrlSortColumn.Hits,
        Direction direction = Direction.Descending,
        string? search = null,
        string? domain = null,
        bool noDomain = false,
        string? culture = null,
        bool noCulture = false)
    {
        if (this.ValidateFilters(domain, noDomain, culture, noCulture) is { } contradiction)
        {
            return contradiction;
        }

        if (await this.EnsurePersistenceAvailableAsync(persistenceState, cancellationToken) is { } unavailable)
        {
            return unavailable;
        }

        var query = new TrackedUrlExportQuery(
            orderBy,
            direction == Direction.Descending,
            search,
            ToFilter(domain, noDomain),
            ToFilter(culture, noCulture));

        return new TrackedUrlCsvResult(exporter, query, timeProvider.GetUtcNow(), logger);
    }
}
