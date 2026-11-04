// Copyright (c) Dave Woestenborghs and contributors. Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Umbraco.Community.PageNotFoundTracker.Core.Configuration;
using Umbraco.Community.PageNotFoundTracker.Core.Tracking;
using Umbraco.Community.PageNotFoundTracker.Infrastructure.Tracking;

namespace Umbraco.Community.PageNotFoundTracker.Infrastructure.Tests;

/// <summary>
/// Unit tests for <see cref="TrackingQueue.Discard"/>.
/// </summary>
[TestFixture]
internal sealed class TrackingQueueDiscardTests
{
    private static readonly DateTime T0 = new(2026, 11, 4, 10, 0, 0, DateTimeKind.Utc);

    /// <summary>
    /// Discard removes the identity from the channel, the pending buffer and the in-flight copy, and nothing else.
    /// </summary>
    [Test]
    public void Discard_removes_channel_pending_and_in_flight_hits_for_the_identity_only()
    {
        var queue = new TrackingQueue(
            Options.Create(new PageNotFoundTrackerSettings()),
            new FakeTimeProvider(new DateTimeOffset(T0)),
            NullLogger<TrackingQueue>.Instance);

        // Pending: all three identities.
        queue.TryEnqueue(Hit("/x", "www.a.com"));
        queue.TryEnqueue(Hit("/y", "www.a.com"));
        queue.TryEnqueue(Hit("/x", "www.b.com"));
        queue.CompleteBatch(queue.TakeBatch());

        // Channel: /x again, with a different case in the domain.
        queue.TryEnqueue(Hit("/x", "WWW.A.COM"));

        int removed = queue.Discard("hash/x", "www.a.com", string.Empty);

        Assert.That(removed, Is.EqualTo(2));

        // In flight: cut a batch, then discard /y from it; the stale batch merges back on the next cut.
        queue.TakeBatch();
        Assert.That(queue.Discard("hash/y", "www.a.com", string.Empty), Is.EqualTo(1));

        var batch = queue.TakeBatch();
        Assert.That(batch.Select(h => (h.Url, h.Domain)), Is.EqualTo(new[] { ("/x", "www.b.com") }));
    }

    private static TrackedUrlHit Hit(string url, string domain) =>
        new(url, "hash" + url, url, url, domain, string.Empty, false, 1, T0, T0);
}
