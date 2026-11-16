// Copyright (c) Dave Woestenborghs and contributors. Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Umbraco.Cms.Api.Common.Attributes;
using Umbraco.Cms.Api.Management.Controllers;
using Umbraco.Cms.Web.Common.Routing;
using Umbraco.Community.PageNotFoundTracker.Core.Tracking;

namespace Umbraco.Community.PageNotFoundTracker.BackOffice.Controllers;

/// <summary>
/// Base for the Page Not Found Tracker Management API controllers: routed under
/// <c>/umbraco/pagenotfoundtracker/api/v{version}</c>, part of the package's own OpenAPI document and protected by the
/// backoffice authentication (the <see cref="ManagementApiControllerBase"/> requires a signed-in backoffice user).
/// </summary>
[BackOfficeRoute(Constants.ApiRoute)]
[MapToApi(Constants.ApiName)]
[ApiExplorerSettings(GroupName = Constants.ApiGroupName)]
public abstract class TrackedUrlsControllerBase : ManagementApiControllerBase
{
    /// <summary>
    /// Ensures the tracking database can be read. While it is still pending (startup) this waits until it is ready.
    /// </summary>
    /// <param name="persistenceState">The tracking persistence state.</param>
    /// <param name="cancellationToken">The request's cancellation token.</param>
    /// <returns><c>null</c> when the database can be read; otherwise a 503 result with problem details.</returns>
    protected async Task<IActionResult?> EnsurePersistenceAvailableAsync(ITrackingPersistenceState persistenceState, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(persistenceState);

        if (persistenceState.Status == TrackingPersistenceStatus.Pending)
        {
            await persistenceState.WaitUntilReadyAsync(cancellationToken);
        }

        if (persistenceState.Status == TrackingPersistenceStatus.Available)
        {
            return null;
        }

        return this.StatusCode(
            StatusCodes.Status503ServiceUnavailable,
            new ProblemDetails
            {
                Title = "Tracking database unavailable",
                Detail = "The tracked URLs cannot be read: the database is not supported or tracking could not set up its tables. See the log for details.",
                Status = StatusCodes.Status503ServiceUnavailable,
                Type = "Error",
            });
    }

    /// <summary>
    /// Maps a value and its "none" flag to a filter: the empty filter when the flag is set, the exact value when there
    /// is one, otherwise no filter.
    /// </summary>
    /// <param name="value">The value, or null.</param>
    /// <param name="empty">Whether rows without a value are asked for.</param>
    /// <returns>The filter.</returns>
    protected static TrackedUrlFilter ToFilter(string? value, bool empty) =>
        empty ? TrackedUrlFilter.Empty : value is null ? TrackedUrlFilter.Any : TrackedUrlFilter.Exactly(value);

    /// <summary>
    /// Rejects a domain or culture filter that contradicts itself (a value together with its "none" flag).
    /// </summary>
    /// <param name="domain">The domain, or null.</param>
    /// <param name="noDomain">Whether URLs without a domain are asked for.</param>
    /// <param name="culture">The culture, or null.</param>
    /// <param name="noCulture">Whether URLs without a culture are asked for.</param>
    /// <returns><c>null</c> when the filters are consistent; otherwise a 400 result.</returns>
    protected IActionResult? ValidateFilters(string? domain, bool noDomain, string? culture, bool noCulture)
    {
        if (domain is not null && noDomain)
        {
            return this.InvalidRequest("Contradicting domain filter", "Use either domain or noDomain, not both.");
        }

        if (culture is not null && noCulture)
        {
            return this.InvalidRequest("Contradicting culture filter", "Use either culture or noCulture, not both.");
        }

        return null;
    }

    /// <summary>
    /// Creates a 400 result with problem details.
    /// </summary>
    /// <param name="title">The title.</param>
    /// <param name="detail">The detail.</param>
    /// <returns>The result.</returns>
    protected IActionResult InvalidRequest(string title, string detail) =>
        this.BadRequest(new ProblemDetails
        {
            Title = title,
            Detail = detail,
            Status = StatusCodes.Status400BadRequest,
            Type = "Error",
        });
}
