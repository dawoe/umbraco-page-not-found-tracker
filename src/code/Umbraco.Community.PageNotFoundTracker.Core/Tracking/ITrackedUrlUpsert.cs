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
    /// <returns>A task that completes when the hits are written.</returns>
    Task UpsertAsync(DbContext dbContext, IReadOnlyCollection<TrackedUrlHit> hits, CancellationToken cancellationToken);
}
