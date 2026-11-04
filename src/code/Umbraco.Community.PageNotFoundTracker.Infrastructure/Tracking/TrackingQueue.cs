// Copyright (c) Dave Woestenborghs and contributors. Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Umbraco.Community.PageNotFoundTracker.Core.Configuration;
using Umbraco.Community.PageNotFoundTracker.Core.Tracking;

namespace Umbraco.Community.PageNotFoundTracker.Infrastructure.Tracking;

/// <summary>
/// The in-memory buffer of hits: a bounded channel the requests write to, and a pending buffer that merges hits per
/// identity and keeps a failed batch for the next attempt. The pending buffer, the in-flight batch and the drain are
/// guarded by one lock; dropping a hit on a full channel never takes it.
/// </summary>
internal sealed class TrackingQueue
{
    private const long NeverWarned = long.MinValue;

    private static readonly TimeSpan WarningInterval = TimeSpan.FromMinutes(1);

    private readonly object gate = new();
    private readonly Channel<TrackedUrlHit> channel;
    private readonly int capacity;
    private readonly TimeProvider timeProvider;
    private readonly ILogger<TrackingQueue> logger;
    private readonly Dictionary<(string UrlHash, string Domain, string Culture), TrackedUrlHit> pending = new(new IdentityComparer());
    private TrackedUrlHit[] inFlight = [];
    private long droppedSinceWarning;
    private long lastWarningTimestamp = NeverWarned;

    // With DropWrite a full channel still reports success from TryWrite, so the drop callback (which runs
    // synchronously on the writing thread) tells TryEnqueue what happened.
    [ThreadStatic]
    private static bool droppedByChannel;

    /// <summary>
    /// Initializes a new instance of the <see cref="TrackingQueue"/> class.
    /// </summary>
    /// <param name="settings">The settings.</param>
    /// <param name="timeProvider">The time provider.</param>
    /// <param name="logger">The logger.</param>
    public TrackingQueue(IOptions<PageNotFoundTrackerSettings> settings, TimeProvider timeProvider, ILogger<TrackingQueue> logger)
    {
        this.capacity = settings.Value.EffectiveChannelCapacity;
        this.timeProvider = timeProvider;
        this.logger = logger;
        var options = new BoundedChannelOptions(this.capacity)
        {
            FullMode = BoundedChannelFullMode.DropWrite,
            SingleReader = true,
            SingleWriter = false,
        };
        this.channel = Channel.CreateBounded<TrackedUrlHit>(options, _ => droppedByChannel = true);
    }

    /// <summary>
    /// Gets the number of identities in the pending buffer (not counting the channel or an in-flight batch).
    /// </summary>
    public int PendingCount
    {
        get
        {
            lock (this.gate)
            {
                return this.pending.Count;
            }
        }
    }

    /// <summary>
    /// Gets the number of queued entries that are not written yet: the pending identities, an uncompleted in-flight
    /// batch and the hits still in the channel (not merged, so one identity can count more than once there).
    /// </summary>
    public int Count
    {
        get
        {
            lock (this.gate)
            {
                return this.pending.Count + this.inFlight.Length + this.channel.Reader.Count;
            }
        }
    }

    /// <summary>
    /// Adds a hit to the channel without blocking.
    /// </summary>
    /// <param name="hit">The hit.</param>
    /// <returns><c>true</c> when queued, <c>false</c> when the channel is full and the hit was dropped.</returns>
    public bool TryEnqueue(TrackedUrlHit hit)
    {
        droppedByChannel = false;
        bool written = this.channel.Writer.TryWrite(hit);
        if (written && !droppedByChannel)
        {
            return true;
        }

        droppedByChannel = false;
        this.RegisterDrop();
        return false;
    }

    /// <summary>
    /// Drains the channel into the pending buffer and moves everything pending into a new in-flight batch.
    /// </summary>
    /// <returns>The merged hits, one per identity. Empty when there is nothing to write.</returns>
    public IReadOnlyCollection<TrackedUrlHit> TakeBatch()
    {
        lock (this.gate)
        {
            while (this.channel.Reader.TryRead(out TrackedUrlHit? hit))
            {
                this.Merge(hit);
            }

            // A batch that was never completed is not lost: it merges back before the next one is cut.
            foreach (TrackedUrlHit stale in this.inFlight)
            {
                this.Merge(stale);
            }

            this.inFlight = [.. this.pending.Values];
            this.pending.Clear();
            return this.inFlight;
        }
    }

    /// <summary>
    /// Finishes the in-flight batch. The hits that were not written go back into the pending buffer, which is then
    /// trimmed to the capacity by dropping the entries with the oldest last seen time; the written hits are not
    /// restored, so they are never counted twice.
    /// </summary>
    /// <param name="failed">The hits of the batch that were not written; empty when the whole batch was written.</param>
    public void CompleteBatch(IReadOnlyCollection<TrackedUrlHit> failed)
    {
        ArgumentNullException.ThrowIfNull(failed);

        lock (this.gate)
        {
            if (failed.Count > 0)
            {
                foreach (TrackedUrlHit hit in failed)
                {
                    this.Merge(hit);
                }

                int excess = this.pending.Count - this.capacity;
                if (excess > 0)
                {
                    var oldest = this.pending.OrderBy(p => p.Value.LastSeen).Take(excess).Select(p => p.Key).ToList();
                    foreach (var key in oldest)
                    {
                        this.pending.Remove(key);
                    }

                    this.logger.LogWarning(
                        "The 404 tracking buffer is over its capacity of {Capacity} after a failed write; dropped {Dropped} of the oldest tracked URLs.",
                        this.capacity,
                        excess);
                }
            }

            this.inFlight = [];
        }
    }

    /// <summary>
    /// Discards everything queued: the channel, the pending buffer and an uncompleted in-flight batch. Used when the
    /// persistence becomes unavailable, so hits that can never be written do not stay in memory.
    /// </summary>
    public void Clear()
    {
        lock (this.gate)
        {
            while (this.channel.Reader.TryRead(out _))
            {
            }

            this.pending.Clear();
            this.inFlight = [];
        }
    }

    /// <summary>
    /// Removes every queued hit of one identity from the channel, the pending buffer and an uncompleted in-flight
    /// batch, so a deleted record is not written back. The identity compares case-insensitively, like the merge key.
    /// Takes the queue lock, so it must not run on a request thread.
    /// </summary>
    /// <param name="urlHash">The URL hash.</param>
    /// <param name="domain">The domain, empty when none.</param>
    /// <param name="culture">The culture, empty when none.</param>
    /// <returns>The number of queued entries removed.</returns>
    public int Discard(string urlHash, string domain, string culture)
    {
        ArgumentNullException.ThrowIfNull(urlHash);
        ArgumentNullException.ThrowIfNull(domain);
        ArgumentNullException.ThrowIfNull(culture);

        var comparer = new IdentityComparer();
        var identity = (urlHash, domain, culture);
        return this.DiscardMatching(hit => comparer.Equals((hit.UrlHash, hit.Domain, hit.Culture), identity));
    }

    /// <summary>
    /// Removes every queued hit a predicate selects from the channel, the pending buffer and an uncompleted in-flight
    /// batch, so deleted records are not written back (a new block rule discards the hits it covers). Takes the queue
    /// lock, so it must not run on a request thread.
    /// </summary>
    /// <param name="predicate">Selects the hits to remove; it runs under the queue lock, so keep it cheap.</param>
    /// <returns>The number of queued entries removed (a merged pending entry counts once).</returns>
    public int DiscardMatching(Func<TrackedUrlHit, bool> predicate)
    {
        ArgumentNullException.ThrowIfNull(predicate);

        int removed = 0;
        lock (this.gate)
        {
            // The channel has no removal: drain it, put the others back in order and count the matches.
            List<TrackedUrlHit> keep = [];
            while (this.channel.Reader.TryRead(out TrackedUrlHit? hit))
            {
                if (predicate(hit))
                {
                    removed++;
                }
                else
                {
                    keep.Add(hit);
                }
            }

            foreach (TrackedUrlHit hit in keep)
            {
                this.channel.Writer.TryWrite(hit);
            }

            var pendingKeys = this.pending.Where(p => predicate(p.Value)).Select(p => p.Key).ToList();
            foreach (var key in pendingKeys)
            {
                this.pending.Remove(key);
            }

            removed += pendingKeys.Count;

            TrackedUrlHit[] remaining = [.. this.inFlight.Where(h => !predicate(h))];
            removed += this.inFlight.Length - remaining.Length;
            this.inFlight = remaining;
        }

        return removed;
    }

    private void Merge(TrackedUrlHit hit)
    {
        var key = (hit.UrlHash, hit.Domain, hit.Culture);
        if (!this.pending.TryGetValue(key, out TrackedUrlHit? existing))
        {
            this.pending[key] = hit;
            return;
        }

        TrackedUrlHit earliest = hit.FirstSeen < existing.FirstSeen ? hit : existing;
        this.pending[key] = earliest with
        {
            Count = existing.Count + hit.Count,
            FirstSeen = hit.FirstSeen < existing.FirstSeen ? hit.FirstSeen : existing.FirstSeen,
            LastSeen = hit.LastSeen > existing.LastSeen ? hit.LastSeen : existing.LastSeen,
        };
    }

    // Lock free, because it runs on the request thread: the counter is interlocked, and a compare-and-swap on the
    // last warning time lets exactly one thread write each warning.
    private void RegisterDrop()
    {
        Interlocked.Increment(ref this.droppedSinceWarning);
        long now = this.timeProvider.GetTimestamp();
        long last = Interlocked.Read(ref this.lastWarningTimestamp);
        if (last != NeverWarned && this.timeProvider.GetElapsedTime(last, now) < WarningInterval)
        {
            return;
        }

        if (Interlocked.CompareExchange(ref this.lastWarningTimestamp, now, last) != last)
        {
            // Another thread is writing this warning.
            return;
        }

        long dropped = Interlocked.Exchange(ref this.droppedSinceWarning, 0);
        this.logger.LogWarning(
            "The 404 tracking queue is full (capacity {Capacity}); dropped {Dropped} hits since the last warning.",
            this.capacity,
            dropped);
    }

    private sealed class IdentityComparer : IEqualityComparer<(string UrlHash, string Domain, string Culture)>
    {
        public bool Equals((string UrlHash, string Domain, string Culture) x, (string UrlHash, string Domain, string Culture) y) =>
            string.Equals(x.UrlHash, y.UrlHash, StringComparison.OrdinalIgnoreCase)
            && string.Equals(x.Domain, y.Domain, StringComparison.OrdinalIgnoreCase)
            && string.Equals(x.Culture, y.Culture, StringComparison.OrdinalIgnoreCase);

        public int GetHashCode((string UrlHash, string Domain, string Culture) obj) =>
            HashCode.Combine(
                StringComparer.OrdinalIgnoreCase.GetHashCode(obj.UrlHash),
                StringComparer.OrdinalIgnoreCase.GetHashCode(obj.Domain),
                StringComparer.OrdinalIgnoreCase.GetHashCode(obj.Culture));
    }
}
