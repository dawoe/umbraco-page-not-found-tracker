// Copyright (c) Dave Woestenborghs and contributors. Licensed under the MIT License. See LICENSE in the project root for license information.

using Asp.Versioning;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Umbraco.Community.PageNotFoundTracker.BackOffice.Models;
using Umbraco.Community.PageNotFoundTracker.BackOffice.Security;
using Umbraco.Community.PageNotFoundTracker.Core.Dashboard;

namespace Umbraco.Community.PageNotFoundTracker.BackOffice.Controllers;

/// <summary>
/// Tells the backoffice whether the current user may use the dashboard. Open to every signed-in backoffice user, so the
/// dashboard condition and the API policy share one rule (<see cref="IDashboardAccess"/>).
/// </summary>
/// <param name="dashboardAccess">The access rule.</param>
[ApiVersion("1.0")]
public sealed class GetDashboardAccessController(IDashboardAccess dashboardAccess) : TrackedUrlsControllerBase
{
    /// <summary>
    /// Gets whether the current user is in one of the user groups allowed to use the dashboard.
    /// </summary>
    /// <returns>The answer.</returns>
    [HttpGet("access")]
    [MapToApiVersion("1.0")]
    [ProducesResponseType(typeof(DashboardAccessResponseModel), StatusCodes.Status200OK)]
    public IActionResult GetDashboardAccess() =>
        this.Ok(new DashboardAccessResponseModel
        {
            Allowed = dashboardAccess.IsAllowed(DashboardAccessHandler.GetUserGroupAliases(this.User)),
        });
}
