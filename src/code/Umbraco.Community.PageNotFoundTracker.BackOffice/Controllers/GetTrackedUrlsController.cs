// Copyright (c) Dave Woestenborghs and contributors. Licensed under the MIT License. See LICENSE in the project root for license information.

using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Umbraco.Cms.Api.Common.ViewModels.Pagination;
using Umbraco.Cms.Core;
using Umbraco.Community.PageNotFoundTracker.BackOffice.Models;
using Umbraco.Community.PageNotFoundTracker.Core.Tracking;

namespace Umbraco.Community.PageNotFoundTracker.BackOffice.Controllers;

/// <summary>
/// Lists the tracked URLs for the dashboard: one page, filtered, searched and sorted in the database.
/// </summary>
/// <param name="queryService">The tracked URL query service.</param>
/// <param name="persistenceState">The tracking persistence state.</param>
[ApiVersion("1.0")]
[Authorize(Policy = Constants.DashboardAccessPolicy)]
public sealed class GetTrackedUrlsController(ITrackedUrlQueryService queryService, ITrackingPersistenceState persistenceState)
    : TrackedUrlsControllerBase
{
    /// <summary>
    /// The largest page size a caller may request.
    /// </summary>
    internal const int MaxTake = 100;

    /// <summary>
    /// Gets one page of tracked URLs.
    /// </summary>
    /// <param name="cancellationToken">The request's cancellation token.</param>
    /// <param name="skip">The number of rows to skip; 0 or more.</param>
    /// <param name="take">The page size, 1 to 100.</param>
    /// <param name="orderBy">The column to sort on; ties are broken by the identifier.</param>
    /// <param name="direction">The sort direction.</param>
    /// <param name="search">A case-insensitive "contains" term for the URL.</param>
    /// <param name="domain">Only URLs with exactly this domain; cannot be combined with <paramref name="noDomain"/>.</param>
    /// <param name="noDomain">Only URLs without a domain.</param>
    /// <param name="culture">Only URLs with exactly this culture; cannot be combined with <paramref name="noCulture"/>.</param>
    /// <param name="noCulture">Only URLs without a culture.</param>
    /// <returns>The page, or problem details.</returns>
    [HttpGet("tracked-urls")]
    [MapToApiVersion("1.0")]
    [ProducesResponseType(typeof(PagedViewModel<TrackedUrlResponseModel>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> GetTrackedUrls(
        CancellationToken cancellationToken,
        int skip = 0,
        int take = 20,
        TrackedUrlSortColumn orderBy = TrackedUrlSortColumn.Hits,
        Direction direction = Direction.Descending,
        string? search = null,
        string? domain = null,
        bool noDomain = false,
        string? culture = null,
        bool noCulture = false)
    {
        if (skip < 0)
        {
            return this.InvalidRequest("Invalid skip", "Skip must be 0 or more.");
        }

        if (take is < 1 or > MaxTake)
        {
            return this.InvalidRequest("Invalid take", $"Take must be between 1 and {MaxTake}.");
        }

        if (domain is not null && noDomain)
        {
            return this.InvalidRequest("Contradicting domain filter", "Use either domain or noDomain, not both.");
        }

        if (culture is not null && noCulture)
        {
            return this.InvalidRequest("Contradicting culture filter", "Use either culture or noCulture, not both.");
        }

        if (await this.EnsurePersistenceAvailableAsync(persistenceState, cancellationToken) is { } unavailable)
        {
            return unavailable;
        }

        var query = new TrackedUrlQuery(
            skip,
            take,
            orderBy,
            direction == Direction.Descending,
            search,
            ToFilter(domain, noDomain),
            ToFilter(culture, noCulture));
        TrackedUrlPage page = await queryService.GetPageAsync(query, cancellationToken);

        return this.Ok(new PagedViewModel<TrackedUrlResponseModel>
        {
            Total = page.Total,
            Items = page.Items.Select(Map).ToList(),
        });
    }

    private static TrackedUrlFilter ToFilter(string? value, bool empty) =>
        empty ? TrackedUrlFilter.Empty : value is null ? TrackedUrlFilter.Any : TrackedUrlFilter.Exactly(value);

    private static TrackedUrlResponseModel Map(TrackedUrl row) => new()
    {
        Id = row.Id,
        Url = row.DisplayUrl,
        RelativePath = row.RelativePath,
        Domain = row.Domain,
        Culture = row.Culture,
        IsFile = row.IsFile,
        HitCount = row.HitCount,

        // Stored as UTC; providers such as SQLite return the value without its kind.
        FirstSeen = DateTime.SpecifyKind(row.FirstSeen, DateTimeKind.Utc),
        LastSeen = DateTime.SpecifyKind(row.LastSeen, DateTimeKind.Utc),
    };
}
