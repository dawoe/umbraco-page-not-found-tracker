// Copyright (c) Dave Woestenborghs and contributors. Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Umbraco.Community.PageNotFoundTracker.Core;
using Umbraco.Community.PageNotFoundTracker.Core.Configuration;
using Umbraco.Community.PageNotFoundTracker.Core.Dashboard;

namespace Umbraco.Community.PageNotFoundTracker.Infrastructure.Dashboard;

/// <summary>
/// Default <see cref="IDashboardAccess"/>: allows the configured user groups, and always the <c>admin</c> group.
/// </summary>
internal sealed class DashboardAccess : IDashboardAccess
{
    private const string AdminAlias = "admin";

    private static readonly string[] DefaultGroups = [AdminAlias, "editor"];

    private readonly HashSet<string> allowed;

    /// <summary>
    /// Initializes a new instance of the <see cref="DashboardAccess"/> class.
    /// The effective set is computed once, because settings are read at startup.
    /// </summary>
    /// <param name="options">The package settings.</param>
    /// <param name="logger">The logger.</param>
    public DashboardAccess(IOptions<PageNotFoundTrackerSettings> options, ILogger<DashboardAccess> logger)
    {
        var configured = (options.Value.DashboardUserGroups ?? [])
            .Select(alias => alias?.Trim())
            .Where(alias => !string.IsNullOrEmpty(alias))
            .Select(alias => alias!)
            .ToList();

        if (configured.Count == 0)
        {
            logger.LogWarning(
                "The setting {Setting} is empty or invalid, falling back to the default user groups: {Groups}.",
                $"{Constants.SectionName}:{nameof(PageNotFoundTrackerSettings.DashboardUserGroups)}",
                string.Join(", ", DefaultGroups));
            configured = [.. DefaultGroups];
        }

        this.allowed = new HashSet<string>(configured, StringComparer.OrdinalIgnoreCase) { AdminAlias };
    }

    /// <inheritdoc />
    public bool IsAllowed(IEnumerable<string> userGroupAliases) =>
        userGroupAliases.Any(alias => alias is not null && this.allowed.Contains(alias.Trim()));
}
