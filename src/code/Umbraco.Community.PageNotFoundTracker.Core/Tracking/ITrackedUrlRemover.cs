// Copyright (c) Dave Woestenborghs and contributors. Licensed under the MIT License. See LICENSE in the project root for license information.

namespace Umbraco.Community.PageNotFoundTracker.Core.Tracking;

/// <summary>
/// Deletes a tracked URL record. Replaceable: register another implementation after the package composer.
/// </summary>
public interface ITrackedUrlRemover
{
    /// <summary>
    /// Deletes the record and discards the hits still queued for its identity, so a pending flush cannot bring it back.
    /// </summary>
    /// <param name="url">The tracked URL to delete.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns><c>true</c> when a row was deleted, <c>false</c> when it was already gone.</returns>
    Task<bool> DeleteAsync(TrackedUrl url, CancellationToken cancellationToken);
}
