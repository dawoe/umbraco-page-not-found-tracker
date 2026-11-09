// Copyright (c) Dave Woestenborghs and contributors. Licensed under the MIT License. See LICENSE in the project root for license information.

using Moq;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.PublishedCache;
using Umbraco.Cms.Core.Services;
using Umbraco.Community.PageNotFoundTracker.Core.Blocking;
using Umbraco.Community.PageNotFoundTracker.Infrastructure.Blocking;
using Umbraco.Community.PageNotFoundTracker.Infrastructure.Tests.TestSupport;
using Umbraco.Community.PageNotFoundTracker.Infrastructure.Tracking;

namespace Umbraco.Community.PageNotFoundTracker.Infrastructure.Tests.Blocking;

/// <summary>
/// Unit tests for <see cref="BlockSnapshotProvider"/>.
/// </summary>
[TestFixture]
internal sealed class BlockSnapshotProviderTests
{
    private static readonly TimeSpan TestTimeout = TimeSpan.FromSeconds(10);

    private CapturingLogger<BlockSnapshotProvider> logger = null!;
    private Mock<IRuntimeState> runtimeState = null!;
    private TrackingPersistenceState persistenceState = null!;

    /// <summary>
    /// Creates fresh collaborators (NUnit shares one fixture instance) and sets the runtime level to
    /// <see cref="RuntimeLevel.Run"/>.
    /// </summary>
    [SetUp]
    public void SetUp()
    {
        this.logger = new CapturingLogger<BlockSnapshotProvider>();
        this.runtimeState = new Mock<IRuntimeState>();
        this.persistenceState = new TrackingPersistenceState();
        this.runtimeState.Setup(r => r.Level).Returns(RuntimeLevel.Run);
    }

    /// <summary>
    /// A request during the initial load waits for it and gets the loaded snapshot.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task Request_during_initial_load_waits_and_gets_the_loaded_snapshot()
    {
        this.persistenceState.SetAvailable();
        var gate = new TaskCompletionSource<BlockSnapshot>(TaskCreationOptions.RunContinuationsAsynchronously);
        var provider = this.CreateProvider(TimeSpan.FromSeconds(30));
        provider.LoadAsync = _ => gate.Task;

        Task<bool> refresh = provider.RefreshAsync(CancellationToken.None);
        Task<BlockSnapshot> request = provider.GetAsync(CancellationToken.None).AsTask();
        await Task.Delay(50);
        Assert.That(request.IsCompleted, Is.False);

        BlockSnapshot loaded = Snapshot("/old", 410);
        gate.SetResult(loaded);

        Assert.That(await request.WaitAsync(TestTimeout), Is.SameAs(loaded));
        Assert.That(await refresh.WaitAsync(TestTimeout), Is.True);
        Assert.That(this.logger.Warnings, Is.Empty);
    }

    /// <summary>
    /// An initial load that takes longer than the wait lets the request pass through with the empty snapshot.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task Initial_load_taking_longer_than_the_wait_passes_through_with_empty()
    {
        this.persistenceState.SetAvailable();
        var provider = this.CreateProvider(TimeSpan.FromMilliseconds(50));
        provider.LoadAsync = _ => new TaskCompletionSource<BlockSnapshot>().Task;
        _ = provider.RefreshAsync(CancellationToken.None);

        BlockSnapshot result = await provider.GetAsync(CancellationToken.None).AsTask().WaitAsync(TestTimeout);

        Assert.That(result, Is.SameAs(BlockSnapshot.Empty));
        Assert.That(this.logger.Warnings, Has.Count.EqualTo(1));
    }

    /// <summary>
    /// After a failed initial load requests pass through at once, and a later successful load serves the rules.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task Failed_initial_load_passes_through_at_once()
    {
        this.persistenceState.SetAvailable();
        var provider = this.CreateProvider(TimeSpan.FromSeconds(30));
        provider.LoadAsync = _ => throw new InvalidOperationException("database down");

        Assert.That(await provider.RefreshAsync(CancellationToken.None), Is.False);
        ValueTask<BlockSnapshot> request = provider.GetAsync(CancellationToken.None);

        Assert.That(request.IsCompleted, Is.True);
        Assert.That(await request, Is.SameAs(BlockSnapshot.Empty));
        Assert.That(this.logger.Errors, Has.Count.EqualTo(1));

        BlockSnapshot loaded = Snapshot("/old", 410);
        provider.LoadAsync = _ => Task.FromResult(loaded);
        Assert.That(await provider.RefreshAsync(CancellationToken.None), Is.True);
        Assert.That(await provider.GetAsync(CancellationToken.None), Is.SameAs(loaded));
    }

    /// <summary>
    /// A refresh swaps the snapshot while a reader that already holds the old one keeps using it.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task Refresh_swaps_while_a_reader_still_sees_the_old_snapshot()
    {
        this.persistenceState.SetAvailable();
        var provider = this.CreateProvider(TimeSpan.FromSeconds(30));
        BlockSnapshot first = Snapshot("/old", 410);
        provider.LoadAsync = _ => Task.FromResult(first);
        await provider.RefreshAsync(CancellationToken.None);

        BlockSnapshot reader = await provider.GetAsync(CancellationToken.None);

        var gate = new TaskCompletionSource<BlockSnapshot>(TaskCreationOptions.RunContinuationsAsynchronously);
        provider.LoadAsync = _ => gate.Task;
        Task<bool> refresh = provider.RefreshAsync(CancellationToken.None);

        // While the new snapshot is being built, requests are served from the old one at once.
        ValueTask<BlockSnapshot> during = provider.GetAsync(CancellationToken.None);
        Assert.That(during.IsCompleted, Is.True);
        Assert.That(await during, Is.SameAs(first));

        BlockSnapshot second = Snapshot("/new", 403);
        gate.SetResult(second);
        Assert.That(await refresh.WaitAsync(TestTimeout), Is.True);

        Assert.That(await provider.GetAsync(CancellationToken.None), Is.SameAs(second));
        Assert.That(reader, Is.SameAs(first));
        Assert.That(reader.FindStatus("/old", "localhost", 44401), Is.EqualTo(410));
        Assert.That(reader.FindStatus("/new", "localhost", 44401), Is.Null);
    }

    /// <summary>
    /// A failed rebuild keeps the old snapshot and does not throw.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task Failed_rebuild_keeps_the_old_snapshot()
    {
        this.persistenceState.SetAvailable();
        var provider = this.CreateProvider(TimeSpan.FromSeconds(30));
        BlockSnapshot first = Snapshot("/old", 410);
        provider.LoadAsync = _ => Task.FromResult(first);
        await provider.RefreshAsync(CancellationToken.None);

        provider.LoadAsync = _ => Task.FromException<BlockSnapshot>(new InvalidOperationException("boom"));

        Assert.That(await provider.RefreshAsync(CancellationToken.None), Is.False);
        Assert.That(await provider.GetAsync(CancellationToken.None), Is.SameAs(first));
        Assert.That(this.logger.Errors, Has.Count.EqualTo(1));
    }

    /// <summary>
    /// With unavailable persistence a request gets the empty snapshot at once and nothing is loaded.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task Unavailable_persistence_returns_empty_without_waiting()
    {
        this.persistenceState.SetUnavailable();
        var provider = this.CreateProvider(TimeSpan.FromSeconds(30));
        var loads = 0;
        provider.LoadAsync = _ =>
        {
            loads++;
            return Task.FromResult(Snapshot("/old", 410));
        };

        ValueTask<BlockSnapshot> request = provider.GetAsync(CancellationToken.None);

        Assert.That(request.IsCompleted, Is.True);
        Assert.That(await request, Is.SameAs(BlockSnapshot.Empty));
        Assert.That(await provider.RefreshAsync(CancellationToken.None), Is.False);
        Assert.That(loads, Is.Zero);
    }

    /// <summary>
    /// Below <see cref="RuntimeLevel.Run"/> (for example during the install) a request gets the empty snapshot at once.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task Runtime_below_run_returns_empty_without_waiting()
    {
        this.runtimeState.Setup(r => r.Level).Returns(RuntimeLevel.Install);
        var provider = this.CreateProvider(TimeSpan.FromSeconds(30));

        ValueTask<BlockSnapshot> request = provider.GetAsync(CancellationToken.None);

        Assert.That(request.IsCompleted, Is.True);
        Assert.That(await request, Is.SameAs(BlockSnapshot.Empty));
    }

    /// <summary>
    /// A caller's cancellation ends the wait with an <see cref="OperationCanceledException"/>.
    /// </summary>
    [Test]
    public void Caller_cancellation_ends_the_wait()
    {
        this.persistenceState.SetAvailable();
        var provider = this.CreateProvider(TimeSpan.FromSeconds(30));
        provider.LoadAsync = _ => new TaskCompletionSource<BlockSnapshot>().Task;
        _ = provider.RefreshAsync(CancellationToken.None);

        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));

        Assert.That(async () => await provider.GetAsync(cts.Token), Throws.InstanceOf<OperationCanceledException>());
    }

    private static BlockSnapshot Snapshot(string pattern, int status)
    {
        return BlockSnapshot.Build(
            [new BlockRule { Pattern = BlockPatternNormalizer.Normalize(pattern), StatusCode = status }],
            []);
    }

    private BlockSnapshotProvider CreateProvider(TimeSpan initialLoadWait)
    {
        return new BlockSnapshotProvider(
            Mock.Of<IServiceProvider>(),
            Mock.Of<IDomainCache>(),
            this.runtimeState.Object,
            this.persistenceState,
            TimeProvider.System,
            this.logger,
            new BlockSnapshotTimings(initialLoadWait, TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(1)));
    }
}
