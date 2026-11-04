// Copyright (c) Dave Woestenborghs and contributors. Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Umbraco.Community.PageNotFoundTracker.Core.Dashboard;

namespace Umbraco.Community.PageNotFoundTracker.BackOffice.Security;

/// <summary>
/// Evaluates <see cref="DashboardAccessRequirement"/> with <see cref="IDashboardAccess"/>, the same rule the dashboard
/// condition uses (through the access endpoint).
/// </summary>
/// <remarks>
/// The user's group aliases are the role claims (<see cref="ClaimTypes.Role"/>) of the backoffice identity: Umbraco
/// sets the identity user's roles to its group aliases (<c>BackOfficeIdentityUser.SetGroups</c>), configures
/// <see cref="ClaimTypes.Role"/> as the role claim type, and copies every claim into the access token.
/// </remarks>
/// <param name="dashboardAccess">The access rule.</param>
internal sealed class DashboardAccessHandler(IDashboardAccess dashboardAccess) : AuthorizationHandler<DashboardAccessRequirement>
{
    /// <summary>
    /// Gets the user group aliases of a backoffice user: the values of its role claims.
    /// </summary>
    /// <param name="user">The user.</param>
    /// <returns>The group aliases.</returns>
    internal static IEnumerable<string> GetUserGroupAliases(ClaimsPrincipal user) =>
        user.FindAll(ClaimTypes.Role).Select(claim => claim.Value);

    /// <inheritdoc />
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, DashboardAccessRequirement requirement)
    {
        if (context.User.Identity?.IsAuthenticated is true && dashboardAccess.IsAllowed(GetUserGroupAliases(context.User)))
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}
