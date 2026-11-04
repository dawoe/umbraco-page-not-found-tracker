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
