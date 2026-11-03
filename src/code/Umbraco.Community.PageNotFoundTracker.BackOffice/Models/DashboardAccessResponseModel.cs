// Copyright (c) Dave Woestenborghs and contributors. Licensed under the MIT License. See LICENSE in the project root for license information.

namespace Umbraco.Community.PageNotFoundTracker.BackOffice.Models;

/// <summary>
/// Whether the current backoffice user may use the dashboard.
/// </summary>
public sealed class DashboardAccessResponseModel
{
    /// <summary>
    /// Gets a value indicating whether the current user is in one of the allowed user groups.
    /// </summary>
    public bool Allowed { get; init; }
}
