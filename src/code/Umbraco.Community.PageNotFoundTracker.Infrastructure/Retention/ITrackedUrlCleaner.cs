// Copyright (c) Dave Woestenborghs and contributors. Licensed under the MIT License. See LICENSE in the project root for license information.

namespace Umbraco.Community.PageNotFoundTracker.Infrastructure.Retention;

/// <summary>
/// Deletes tracked URLs whose last hit is older than a cut-off, one batch at a time.
/// </summary>
internal interface ITrackedUrlCleaner
{
    /// <summary>
    /// Deletes at most <paramref name="batchSize"/> tracked URLs whose <c>LastSeen</c> is before <paramref name="cutoff"/>.
    /// </summary>
    /// <param name="cutoff">The UTC cut-off; rows last seen strictly before it are deleted.</param>
    /// <param name="batchSize">The maximum number of rows to delete.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The number of rows deleted.</returns>
    Task<int> DeleteBatchAsync(DateTime cutoff, int batchSize, CancellationToken cancellationToken);
}
