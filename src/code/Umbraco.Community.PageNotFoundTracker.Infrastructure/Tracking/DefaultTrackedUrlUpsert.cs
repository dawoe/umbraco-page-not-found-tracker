// Copyright (c) Dave Woestenborghs and contributors. Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.EntityFrameworkCore;
using Umbraco.Community.PageNotFoundTracker.Core.Tracking;

namespace Umbraco.Community.PageNotFoundTracker.Infrastructure.Tracking;

/// <summary>
/// The generic EF Core upsert, used when the database provider has no provider specific one. Per hit it updates the
/// existing row, inserts when there is none, and updates once more when another server inserted the row in between.
/// No surrounding transaction: each statement is atomic on its own.
/// </summary>
internal sealed class DefaultTrackedUrlUpsert : ITrackedUrlUpsert
{
    /// <inheritdoc />
    public async Task UpsertAsync(DbContext dbContext, IReadOnlyCollection<TrackedUrlHit> hits, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(hits);

        DbSet<TrackedUrl> set = dbContext.Set<TrackedUrl>();
        foreach (TrackedUrlHit hit in hits)
        {
            if (await UpdateAsync(set, hit, cancellationToken) > 0)
            {
                continue;
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
                // Another server inserted the same identity between the update and the insert.
                dbContext.Entry(entity).State = EntityState.Detached;
                if (await UpdateAsync(set, hit, cancellationToken) == 0)
                {
                    throw;
                }

                continue;
            }

            // Keep the change tracker small over a large batch.
            dbContext.Entry(entity).State = EntityState.Detached;
        }
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
