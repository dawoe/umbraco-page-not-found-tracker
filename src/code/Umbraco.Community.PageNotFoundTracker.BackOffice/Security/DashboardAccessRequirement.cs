// Copyright (c) Dave Woestenborghs and contributors. Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.AspNetCore.Authorization;

namespace Umbraco.Community.PageNotFoundTracker.BackOffice.Security;

/// <summary>
/// Requires the signed-in backoffice user to be in one of the user groups allowed to use the dashboard.
/// </summary>
internal sealed class DashboardAccessRequirement : IAuthorizationRequirement
{
}
