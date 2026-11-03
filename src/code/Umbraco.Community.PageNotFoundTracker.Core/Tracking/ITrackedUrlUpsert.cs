// Copyright (c) Dave Woestenborghs and contributors. Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.EntityFrameworkCore;

namespace Umbraco.Community.PageNotFoundTracker.Core.Tracking;

/// <summary>
/// Provider specific bulk upsert of tracked URLs.
/// </summary>
public interface ITrackedUrlUpsert
{
    /// <summary>
    /// Adds the hits to the stored rows: inserts new identities, and for existing ones increments the count and
    /// updates the last seen time.
    /// </summary>
    /// <param name="dbContext">The database context to write with.</param>
    /// <param name="hits">The merged hits, one per identity.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>
    /// The hits that were not written (empty when all were). The caller keeps them pending for the next flush; the
    /// written hits are not sent again. An implementation that throws is treated as having written none of the batch,
    /// so an implementation that writes in batches must be all-or-nothing when it throws.
    /// </returns>
    Task<IReadOnlyCollection<TrackedUrlHit>> UpsertAsync(DbContext dbContext, IReadOnlyCollection<TrackedUrlHit> hits, CancellationToken cancellationToken);
}
