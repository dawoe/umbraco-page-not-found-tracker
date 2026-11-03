// Copyright (c) Dave Woestenborghs and contributors. Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Umbraco.Community.PageNotFoundTracker.Core.Tracking;

namespace Umbraco.Community.PageNotFoundTracker.Infrastructure.Tracking;

/// <summary>
/// The generic EF Core upsert, used when the database provider has no provider specific one. Per hit it updates the
/// existing row, inserts when there is none, and updates once more when another server inserted the row in between.
/// No surrounding transaction: each statement is atomic on its own, and each hit succeeds or fails on its own, so a
/// failed hit is returned without the written ones being sent again. Per-hit failures are logged at debug only (the
/// flush logs one summary warning); after <see cref="MaxConsecutiveFailures"/> failures in a row the database is
/// treated as down and the rest of the batch is returned unattempted. Cancellation does not throw: the current and
/// the unattempted hits are returned.
/// </summary>
/// <param name="logger">The logger.</param>
internal sealed class DefaultTrackedUrlUpsert(ILogger<DefaultTrackedUrlUpsert> logger) : ITrackedUrlUpsert
{
    /// <summary>
    /// The number of consecutive failed hits after which the rest of the batch is not attempted.
    /// </summary>
    internal const int MaxConsecutiveFailures = 3;

    /// <inheritdoc />
    public async Task<IReadOnlyCollection<TrackedUrlHit>> UpsertAsync(DbContext dbContext, IReadOnlyCollection<TrackedUrlHit> hits, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(hits);

        DbSet<TrackedUrl> set = dbContext.Set<TrackedUrl>();
        var failed = new FailedTrackedUrlHits();
        TrackedUrlHit[] all = [.. hits];
        int consecutiveFailures = 0;
        for (int index = 0; index < all.Length; index++)
        {
            TrackedUrlHit hit = all[index];
            if (cancellationToken.IsCancellationRequested)
            {
                AddRemaining(failed, all, index);
                return failed;
            }

            try
            {
                await UpsertHitAsync(dbContext, set, hit, cancellationToken);
                consecutiveFailures = 0;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // The current hit counts as not written; the hits before it are written and are not returned.
                AddRemaining(failed, all, index);
                return failed;
            }
            catch (Exception ex)
            {
                logger.LogDebug(ex, "The tracked 404 URL {Url} could not be written; it stays pending for the next flush.", hit.Url);
                failed.Add(hit, ex);
                if (++consecutiveFailures >= MaxConsecutiveFailures && index + 1 < all.Length)
                {
                    logger.LogDebug(
                        "Stopped writing tracked 404 URLs after {Failures} consecutive failures; {Remaining} were not attempted.",
                        consecutiveFailures,
                        all.Length - index - 1);
                    AddRemaining(failed, all, index + 1);
                    return failed;
                }
            }
        }

        return failed;
    }

    private static void AddRemaining(FailedTrackedUrlHits failed, TrackedUrlHit[] all, int start)
    {
        for (int i = start; i < all.Length; i++)
        {
            failed.Add(all[i]);
        }
    }

    private static async Task UpsertHitAsync(DbContext dbContext, DbSet<TrackedUrl> set, TrackedUrlHit hit, CancellationToken cancellationToken)
    {
        if (await UpdateAsync(set, hit, cancellationToken) > 0)
        {
            return;
        }

        var entity = new TrackedUrl
        {
            Url = hit.Url,
            UrlHash = hit.UrlHash,
            RelativePath = hit.RelativePath,
            DisplayUrl = hit.DisplayUrl,
            Domain = hit.Domain,
            Culture = hit.Culture,
            IsFile = hit.IsFile,
            HitCount = hit.Count,
            FirstSeen = hit.FirstSeen,
            LastSeen = hit.LastSeen,
        };
        set.Add(entity);
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // Another server inserted the same identity between the update and the insert, or the row is invalid.
            dbContext.Entry(entity).State = EntityState.Detached;
            if (await UpdateAsync(set, hit, cancellationToken) == 0)
            {
                throw;
            }

            return;
        }
        catch
        {
            // Never leave a failed insert tracked: the next hit's SaveChanges would try it again.
            dbContext.Entry(entity).State = EntityState.Detached;
            throw;
        }

        // Keep the change tracker small over a large batch.
        dbContext.Entry(entity).State = EntityState.Detached;
    }

    private static Task<int> UpdateAsync(DbSet<TrackedUrl> set, TrackedUrlHit hit, CancellationToken cancellationToken)
    {
        string urlHash = hit.UrlHash;
        string domain = hit.Domain;
        string culture = hit.Culture;
        int count = hit.Count;
        DateTime lastSeen = hit.LastSeen;

        return set
            .Where(e => e.UrlHash == urlHash && e.Domain == domain && e.Culture == culture)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(e => e.HitCount, e => e.HitCount + count)
                    .SetProperty(e => e.LastSeen, e => e.LastSeen > lastSeen ? e.LastSeen : lastSeen),
                cancellationToken);
    }
}
