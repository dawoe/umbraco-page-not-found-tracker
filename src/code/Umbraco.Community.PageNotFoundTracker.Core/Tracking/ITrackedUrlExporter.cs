// Copyright (c) Dave Woestenborghs and contributors. Licensed under the MIT License. See LICENSE in the project root for license information.

namespace Umbraco.Community.PageNotFoundTracker.Core.Tracking;

/// <summary>
/// Reads every tracked URL that matches a query, for the CSV export of the dashboard. Replaceable: register another
/// implementation with <c>AddUnique</c> in a composer that runs after the package's composer.
/// </summary>
/// <remarks>
/// An implementation must read lazily and must not buffer the whole result: the export streams the rows to the
/// response as they arrive, so a large table never sits in memory. The order must follow the query and end in
/// ascending <see cref="TrackedUrl.Id"/>, so the result is the same rows in the same order as paging through the
/// list.
/// </remarks>
public interface ITrackedUrlExporter
{
    /// <summary>
    /// Streams every tracked URL that matches the query. Filtering and ordering happen in the database.
    /// </summary>
    /// <param name="query">The query.</param>
    /// <param name="cancellationToken">The cancellation token; cancelling it ends the read.</param>
    /// <returns>The rows, in order.</returns>
    IAsyncEnumerable<TrackedUrl> StreamAsync(TrackedUrlExportQuery query, CancellationToken cancellationToken);
}
