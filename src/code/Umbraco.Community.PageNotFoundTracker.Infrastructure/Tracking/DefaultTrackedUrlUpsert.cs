// Copyright (c) Dave Woestenborghs and contributors. Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Umbraco.Community.PageNotFoundTracker.Core.Tracking;

namespace Umbraco.Community.PageNotFoundTracker.Infrastructure.Tracking;

/// <summary>
/// The generic EF Core upsert, used when the database provider has no provider specific one. Per hit it updates the
/// existing row, inserts when there is none, and updates once more when another server inserted the row in between.
/// No surrounding transaction: each statement is atomic on its own, and each hit succeeds or fails on its own, so a
/// failed hit is returned without the written ones being sent again.
/// </summary>
/// <param name="logger">The logger.</param>
internal sealed class DefaultTrackedUrlUpsert(ILogger<DefaultTrackedUrlUpsert> logger) : ITrackedUrlUpsert
{
    /// <inheritdoc />
    public async Task<IReadOnlyCollection<TrackedUrlHit>> UpsertAsync(DbContext dbContext, IReadOnlyCollection<TrackedUrlHit> hits, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(hits);

        DbSet<TrackedUrl> set = dbContext.Set<TrackedUrl>();
        List<TrackedUrlHit> failed = [];
        foreach (TrackedUrlHit hit in hits)
        {
            try
            {
                await UpsertHitAsync(dbContext, set, hit, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Writing the tracked 404 URL {Url} failed; it stays pending for the next flush.", hit.Url);
                failed.Add(hit);
            }
        }

        return failed;
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
