// Copyright (c) Dave Woestenborghs and contributors. Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Umbraco.Community.PageNotFoundTracker.Core.Configuration;
using Umbraco.Community.PageNotFoundTracker.Core.Tracking;
using Umbraco.Community.PageNotFoundTracker.Infrastructure.Tracking;

namespace Umbraco.Community.PageNotFoundTracker.Infrastructure.Tests.Tracking;

/// <summary>
/// Unit tests for <see cref="TrackingQueue.DiscardMatching"/>.
/// </summary>
[TestFixture]
internal sealed class TrackingQueueDiscardMatchingTests
{
    private static readonly DateTime T0 = new(2026, 11, 4, 10, 0, 0, DateTimeKind.Utc);

    /// <summary>
    /// DiscardMatching removes matching hits from the channel, the pending buffer and the in-flight batch, and nothing
    /// else; the hits that stay keep their order and counts.
    /// </summary>
    [Test]
    public void DiscardMatching_removes_matching_hits_from_channel_pending_and_in_flight_only()
    {
        TrackingQueue queue = CreateQueue();
        static bool IsWpAdmin(TrackedUrlHit h) => h.RelativePath.StartsWith("/wp-admin/", StringComparison.Ordinal);

        // Pending (a failed batch goes back to pending) plus the channel.
        queue.TryEnqueue(Hit("/wp-admin/a"));
        queue.TryEnqueue(Hit("/keep-a"));
        queue.CompleteBatch(queue.TakeBatch());
        queue.TryEnqueue(Hit("/wp-admin/b"));
        queue.TryEnqueue(Hit("/wp-admin/b"));
        queue.TryEnqueue(Hit("/keep-b"));

        int removedFromPendingAndChannel = queue.DiscardMatching(IsWpAdmin);

        // In flight (a batch that is being written) plus the channel.
        queue.TryEnqueue(Hit("/wp-admin/e"));
        queue.TakeBatch();
        queue.TryEnqueue(Hit("/wp-admin/c"));
        queue.TryEnqueue(Hit("/keep-c"));

        int removedFromInFlightAndChannel = queue.DiscardMatching(IsWpAdmin);
        queue.TryEnqueue(Hit("/wp-admin/d"));
        IReadOnlyCollection<TrackedUrlHit> batch = queue.TakeBatch();

        Assert.Multiple(() =>
        {
            Assert.That(removedFromPendingAndChannel, Is.EqualTo(3));
            Assert.That(removedFromInFlightAndChannel, Is.EqualTo(2));
            Assert.That(
                batch.Select(h => (h.Url, h.Count)),
                Is.EquivalentTo(new[] { ("/keep-a", 1L), ("/keep-b", 1L), ("/keep-c", 1L), ("/wp-admin/d", 1L) }));
        });
    }

    /// <summary>
    /// A merged pending entry is removed as one entry.
    /// </summary>
    [Test]
    public void DiscardMatching_counts_a_merged_pending_entry_once()
    {
        TrackingQueue queue = CreateQueue();
        queue.TryEnqueue(Hit("/x"));
        queue.TryEnqueue(Hit("/x"));
        queue.CompleteBatch(queue.TakeBatch());

        Assert.Multiple(() =>
        {
            Assert.That(queue.DiscardMatching(h => h.Url == "/x"), Is.EqualTo(1));
            Assert.That(queue.Count, Is.Zero);
        });
    }

    /// <summary>
    /// A predicate that matches nothing removes nothing.
    /// </summary>
    [Test]
    public void DiscardMatching_with_no_match_keeps_everything()
    {
        TrackingQueue queue = CreateQueue();
        queue.TryEnqueue(Hit("/x"));
        queue.TryEnqueue(Hit("/y"));

        Assert.Multiple(() =>
        {
            Assert.That(queue.DiscardMatching(_ => false), Is.Zero);
            Assert.That(queue.TakeBatch().Select(h => h.Url), Is.EquivalentTo(new[] { "/x", "/y" }));
        });
    }

    private static TrackingQueue CreateQueue() =>
        new(
            Options.Create(new PageNotFoundTrackerSettings()),
            new FakeTimeProvider(new DateTimeOffset(T0)),
            NullLogger<TrackingQueue>.Instance);

    private static TrackedUrlHit Hit(string url) =>
        new(url, "hash" + url, url, url, string.Empty, string.Empty, false, 1, T0, T0);
}
