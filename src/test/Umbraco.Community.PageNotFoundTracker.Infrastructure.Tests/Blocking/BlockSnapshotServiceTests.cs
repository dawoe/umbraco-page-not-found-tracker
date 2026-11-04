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
/// Unit tests for <see cref="BlockSnapshotService"/>.
/// </summary>
[TestFixture]
internal sealed class BlockSnapshotServiceTests
{
    private static readonly TimeSpan TestTimeout = TimeSpan.FromSeconds(10);

    private static readonly BlockSnapshotTimings FastTimings =
        new(TimeSpan.FromSeconds(5), TimeSpan.FromMilliseconds(20), TimeSpan.FromMilliseconds(20));

    private Mock<IRuntimeState> runtimeState = null!;
    private TrackingPersistenceState persistenceState = null!;
    private CapturingLogger<BlockSnapshotService> serviceLogger = null!;
    private BlockSnapshotProvider provider = null!;

    /// <summary>
    /// Creates fresh collaborators (NUnit shares one fixture instance) and the provider.
    /// </summary>
    [SetUp]
    public void SetUp()
    {
        this.runtimeState = new Mock<IRuntimeState>();
        this.persistenceState = new TrackingPersistenceState();
        this.serviceLogger = new CapturingLogger<BlockSnapshotService>();
        this.runtimeState.Setup(r => r.Level).Returns(RuntimeLevel.Run);
        this.provider = new BlockSnapshotProvider(
            Mock.Of<IServiceProvider>(),
            Mock.Of<IDomainCache>(),
            this.runtimeState.Object,
            this.persistenceState,
            TimeProvider.System,
            new CapturingLogger<BlockSnapshotProvider>(),
            FastTimings);
    }

    /// <summary>
    /// A failed initial load is retried in the background until it succeeds.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task Failed_initial_load_is_retried_in_the_background()
    {
        this.persistenceState.SetAvailable();
        BlockSnapshot loaded = Snapshot();
        var attempts = 0;
        var succeeded = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        this.provider.LoadAsync = _ =>
        {
            if (Interlocked.Increment(ref attempts) < 3)
            {
                throw new InvalidOperationException("database down");
            }

            succeeded.TrySetResult();
            return Task.FromResult(loaded);
        };

        using var service = this.CreateService();
        await service.StartAsync(CancellationToken.None);
        await succeeded.Task.WaitAsync(TestTimeout);
        await service.StopAsync(CancellationToken.None);

        Assert.That(attempts, Is.EqualTo(3));
        Assert.That(await this.provider.GetAsync(CancellationToken.None), Is.SameAs(loaded));
    }

    /// <summary>
    /// The service waits for <see cref="RuntimeLevel.Run"/> and for the persistence gate before loading.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task Waits_for_run_and_the_persistence_gate()
    {
        this.runtimeState.Setup(r => r.Level).Returns(RuntimeLevel.Install);
        BlockSnapshot loaded = Snapshot();
        var loads = 0;
        this.provider.LoadAsync = _ =>
        {
            Interlocked.Increment(ref loads);
            return Task.FromResult(loaded);
        };

        using var service = this.CreateService();
        await service.StartAsync(CancellationToken.None);
        await Task.Delay(100);
        Assert.That(loads, Is.Zero);

        this.runtimeState.Setup(r => r.Level).Returns(RuntimeLevel.Run);
        await Task.Delay(100);
        Assert.That(loads, Is.Zero);

        this.persistenceState.SetAvailable();
        await WaitUntil(() => Volatile.Read(ref loads) == 1);
        await service.StopAsync(CancellationToken.None);

        Assert.That(await this.provider.GetAsync(CancellationToken.None), Is.SameAs(loaded));
    }

    /// <summary>
    /// With unavailable persistence nothing is loaded, a warning is logged and requests get the empty snapshot.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task Unavailable_persistence_stays_on_empty()
    {
        this.persistenceState.SetUnavailable();
        var loads = 0;
        this.provider.LoadAsync = _ =>
        {
            Interlocked.Increment(ref loads);
            return Task.FromResult(Snapshot());
        };

        using var service = this.CreateService();
        await service.StartAsync(CancellationToken.None);
        await WaitUntil(() => this.serviceLogger.Warnings.Count == 1);
        await service.StopAsync(CancellationToken.None);

        Assert.That(loads, Is.Zero);
        Assert.That(await this.provider.GetAsync(CancellationToken.None), Is.SameAs(BlockSnapshot.Empty));
    }

    private static BlockSnapshot Snapshot()
    {
        return BlockSnapshot.Build([new BlockRule { Pattern = "/old", StatusCode = 410 }], []);
    }

    private static async Task WaitUntil(Func<bool> condition)
    {
        DateTime end = DateTime.UtcNow + TestTimeout;
        while (!condition())
        {
            if (DateTime.UtcNow > end)
            {
                Assert.Fail("Condition not met in time.");
            }

            await Task.Delay(10);
        }
    }

    private BlockSnapshotService CreateService()
    {
        return new BlockSnapshotService(
            this.runtimeState.Object,
            this.persistenceState,
            this.provider,
            TimeProvider.System,
            this.serviceLogger,
            FastTimings);
    }
}
