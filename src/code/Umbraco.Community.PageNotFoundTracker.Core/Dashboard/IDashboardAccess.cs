// Copyright (c) Dave Woestenborghs and contributors. Licensed under the MIT License. See LICENSE in the project root for license information.

namespace Umbraco.Community.PageNotFoundTracker.Core.Dashboard;

/// <summary>
/// Decides which backoffice users may use the dashboard, based on their user group aliases.
/// </summary>
public interface IDashboardAccess
{
    /// <summary>
    /// Determines whether a user with the given user groups may use the dashboard.
    /// </summary>
    /// <param name="userGroupAliases">The aliases of the user's groups.</param>
    /// <returns><c>true</c> when at least one group is allowed; otherwise <c>false</c>.</returns>
    bool IsAllowed(IEnumerable<string> userGroupAliases);
}
