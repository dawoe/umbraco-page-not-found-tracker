// Copyright (c) Dave Woestenborghs and contributors. Licensed under the MIT License. See LICENSE in the project root for license information.

namespace Umbraco.Community.PageNotFoundTracker.BackOffice;

/// <summary>
/// Constants for the back office project.
/// </summary>
internal static class Constants
{
    /// <summary>
    /// The name of the Management API, also the name of its OpenAPI document
    /// (<c>/umbraco/swagger/page-not-found-tracker-management-api/swagger.json</c>).
    /// </summary>
    public const string ApiName = "page-not-found-tracker-management-api";

    /// <summary>
    /// The versioned route of the Management API below the backoffice path: <c>/umbraco/pagenotfoundtracker/api/v1</c>.
    /// </summary>
    public const string ApiRoute = "pagenotfoundtracker/api/v{version:apiVersion}";

    /// <summary>
    /// The OpenAPI group (tag) of the tracked URL operations.
    /// </summary>
    public const string ApiGroupName = "Tracked URL";

    /// <summary>
    /// The name of the authorization policy that lets only the configured user groups use the dashboard API.
    /// </summary>
    public const string DashboardAccessPolicy = "PageNotFoundTracker.DashboardAccess";
}
