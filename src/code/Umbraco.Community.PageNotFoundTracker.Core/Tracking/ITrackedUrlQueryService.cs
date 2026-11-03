// Copyright (c) Dave Woestenborghs and contributors. Licensed under the MIT License. See LICENSE in the project root for license information.

namespace Umbraco.Community.PageNotFoundTracker.Core.Tracking;

/// <summary>
/// Reads the tracked URLs for the backoffice. Replaceable: register another implementation after the package's composer.
/// </summary>
public interface ITrackedUrlQueryService
{
    /// <summary>
    /// Gets one page of tracked URLs. Filtering, ordering, paging and counting happen in the database.
    /// </summary>
    /// <param name="query">The query.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The page.</returns>
    Task<TrackedUrlPage> GetPageAsync(TrackedUrlQuery query, CancellationToken cancellationToken);

    /// <summary>
    /// Gets the values the domain and culture filters can offer.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The options.</returns>
    Task<TrackedUrlFilterOptions> GetFilterOptionsAsync(CancellationToken cancellationToken);
}
