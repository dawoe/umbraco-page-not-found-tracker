// Copyright (c) Dave Woestenborghs and contributors. Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Moq;
using Umbraco.Cms.Core.Cache;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Serialization;
using Umbraco.Community.PageNotFoundTracker.Core.Blocking;
using Umbraco.Community.PageNotFoundTracker.Core.Configuration;
using Umbraco.Community.PageNotFoundTracker.Core.Tracking;
using Umbraco.Community.PageNotFoundTracker.Infrastructure.Blocking;
using Umbraco.Community.PageNotFoundTracker.Infrastructure.Tracking;

namespace Umbraco.Community.PageNotFoundTracker.Infrastructure.Tests.Blocking;

/// <summary>
/// Unit tests for <see cref="BlockRuleCacheRefresher"/>.
/// </summary>
[TestFixture]
internal sealed class BlockRuleCacheRefresherTests
{
    private static readonly DateTime T0 = new(2026, 11, 4, 10, 0, 0, DateTimeKind.Utc);

    private Mock<IBlockSnapshotProvider> snapshotProvider = null!;
    private Mock<ITrackingFlusher> flusher = null!;
    private TrackingQueue queue = null!;
    private int exclusiveCalls;
    private BlockRuleCacheRefresher refresher = null!;

    /// <summary>
    /// Builds the refresher with mocked collaborators and a real queue.
    /// </summary>
    [SetUp]
    public void SetUp()
    {
        this.exclusiveCalls = 0;
        this.snapshotProvider = new Mock<IBlockSnapshotProvider>();
        this.snapshotProvider.Setup(p => p.RefreshAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);
        this.flusher = new Mock<ITrackingFlusher>();
        this.flusher.Setup(f => f.RunExclusiveAsync(It.IsAny<Func<Task>>(), It.IsAny<CancellationToken>()))
            .Returns((Func<Task> action, CancellationToken _) =>
            {
                this.exclusiveCalls++;
                return action();
            });
        this.queue = new TrackingQueue(
            Options.Create(new PageNotFoundTrackerSettings()),
            new FakeTimeProvider(new DateTimeOffset(T0)),
            NullLogger<TrackingQueue>.Instance);

        this.refresher = new BlockRuleCacheRefresher(
            new AppCaches(NoAppCache.Instance, NoAppCache.Instance, new IsolatedCaches(_ => NoAppCache.Instance)),
            Mock.Of<IJsonSerializer>(),
            Mock.Of<IEventAggregator>(),
            Mock.Of<ICacheRefresherNotificationFactory>(),
            this.snapshotProvider.Object,
            this.flusher.Object,
            this.queue,
            NullLogger<BlockRuleCacheRefresher>.Instance);
    }

    /// <summary>
    /// A saved rule rebuilds the snapshot and discards only the queued hits it covers.
    /// </summary>
    [Test]
    public void Saved_payload_rebuilds_snapshot_and_discards_matching_queued_hits_only()
    {
        this.queue.TryEnqueue(Hit("/wp-admin/login"));
        this.queue.TryEnqueue(Hit("/about"));
        this.queue.TryEnqueue(Hit("/wp-admin/x", domain: "other.com"));

        this.refresher.Refresh([new BlockRuleCachePayload("/wp-admin/*", "other.com", string.Empty, BlockRuleChange.Saved)]);

        this.snapshotProvider.Verify(p => p.RefreshAsync(It.IsAny<CancellationToken>()), Times.Once);
        Assert.That(this.exclusiveCalls, Is.EqualTo(1));
        Assert.That(this.Drain(), Is.EquivalentTo(new[] { "/wp-admin/login", "/about" }));
    }

    /// <summary>
    /// A removed rule only rebuilds the snapshot.
    /// </summary>
    [Test]
    public void Removed_payload_rebuilds_without_discarding()
    {
        this.queue.TryEnqueue(Hit("/wp-admin/login"));

        this.refresher.Refresh([new BlockRuleCachePayload("/wp-admin/*", string.Empty, string.Empty, BlockRuleChange.Removed)]);

        this.snapshotProvider.Verify(p => p.RefreshAsync(It.IsAny<CancellationToken>()), Times.Once);
        Assert.That(this.exclusiveCalls, Is.Zero);
        Assert.That(this.Drain(), Is.EquivalentTo(new[] { "/wp-admin/login" }));
    }

    /// <summary>
    /// A failing rebuild or discard never escapes the refresher.
    /// </summary>
    [Test]
    public void Refresher_failure_in_rebuild_does_not_throw()
    {
        this.snapshotProvider.Setup(p => p.RefreshAsync(It.IsAny<CancellationToken>())).ThrowsAsync(new InvalidOperationException("boom"));
        this.flusher.Setup(f => f.RunExclusiveAsync(It.IsAny<Func<Task>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("boom"));

        Assert.DoesNotThrow(() => this.refresher.Refresh([new BlockRuleCachePayload("/a", string.Empty, string.Empty, BlockRuleChange.Saved)]));
    }

    /// <summary>
    /// The payload survives the JSON round trip that carries it to the other servers.
    /// </summary>
    [Test]
    public void Payload_round_trips_through_system_text_json()
    {
        var payload = new BlockRuleCachePayload("/a/*", "example.com", "nl-NL", BlockRuleChange.Saved);

        string json = System.Text.Json.JsonSerializer.Serialize(new[] { payload });
        BlockRuleCachePayload[]? back = System.Text.Json.JsonSerializer.Deserialize<BlockRuleCachePayload[]>(json);

        Assert.That(back, Is.EqualTo(new[] { payload }));
    }

    /// <summary>
    /// The refresher id and name are fixed.
    /// </summary>
    [Test]
    public void Refresher_has_a_fixed_id_and_name()
    {
        Assert.That(BlockRuleCacheRefresher.UniqueId, Is.EqualTo(new Guid("6d1c8a40-3f2b-4e57-9b1d-5a7c0e2f9d31")));
        Assert.That(this.refresher.RefresherUniqueId, Is.EqualTo(BlockRuleCacheRefresher.UniqueId));
        Assert.That(this.refresher.Name, Is.EqualTo("Page Not Found Tracker Block Rules"));
    }

    private static TrackedUrlHit Hit(string path, string domain = "", string culture = "") =>
        new(path, path, path, path, domain, culture, false, 1, T0, T0);

    private List<string> Drain() => this.queue.TakeBatch().Select(h => h.RelativePath).ToList();
}
