// Copyright (c) Dave Woestenborghs and contributors. Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Moq;
using Umbraco.Cms.Core.Configuration.Models;
using Umbraco.Community.PageNotFoundTracker.Core.Configuration;
using Umbraco.Community.PageNotFoundTracker.Core.Tracking;
using Umbraco.Community.PageNotFoundTracker.Infrastructure.Tracking;

namespace Umbraco.Community.PageNotFoundTracker.Infrastructure.Tests;

/// <summary>
/// Unit tests for the <see cref="TrackingFlushService"/> class.
/// </summary>
[TestFixture]
internal sealed class TrackingFlushServiceTests
{
    private const string ProviderName = "Microsoft.Data.Sqlite";

    private static readonly DateTime T0 = new(2026, 11, 3, 10, 0, 0, DateTimeKind.Utc);
    private static readonly TimeSpan WaitTimeout = TimeSpan.FromSeconds(5);

    private TimerAwareTimeProvider time = null!;
    private TrackingQueue queue = null!;
    private TrackingPersistenceState state = null!;
    private RecordingUpsert upsert = null!;
    private Mock<IDbContextFactory<PageNotFoundTrackerDbContext>> factory = null!;
    private TrackingFlushService? service;

    /// <summary>
    /// Creates the shared fakes.
    /// </summary>
    [SetUp]
    public void SetUp()
    {
        this.time = new TimerAwareTimeProvider(new DateTimeOffset(T0));
        this.queue = new TrackingQueue(
            Options.Create(new PageNotFoundTrackerSettings()),
            this.time,
            NullLogger<TrackingQueue>.Instance);
        this.state = new TrackingPersistenceState();
        this.upsert = new RecordingUpsert();
        this.factory = new Mock<IDbContextFactory<PageNotFoundTrackerDbContext>>();
        this.factory.Setup(f => f.CreateDbContextAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => new PageNotFoundTrackerDbContext(new DbContextOptionsBuilder<PageNotFoundTrackerDbContext>().Options));
        this.factory.Setup(f => f.CreateDbContext())
            .Returns(() => new PageNotFoundTrackerDbContext(new DbContextOptionsBuilder<PageNotFoundTrackerDbContext>().Options));
    }

    /// <summary>
    /// Stops the service.
    /// </summary>
    /// <returns>A task.</returns>
    [TearDown]
    public async Task TearDown()
    {
        if (this.service is not null)
        {
            await this.service.StopAsync(CancellationToken.None);
            this.service.Dispose();
            this.service = null;
        }
    }

    /// <summary>
    /// Nothing is written before the interval elapses; one write after it.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task Flushes_Once_After_Interval()
    {
        this.state.SetAvailable();
        this.service = this.CreateService();
        await this.service.StartAsync(CancellationToken.None);
        await this.time.TimerCreated.WaitAsync(WaitTimeout);
        this.queue.TryEnqueue(Hit("/a"));

        this.time.Advance(TimeSpan.FromMinutes(1) - TimeSpan.FromSeconds(1));
        await Task.Delay(100);
        Assert.That(this.upsert.Calls, Is.Empty);

        this.time.Advance(TimeSpan.FromSeconds(1));
        await this.upsert.WaitForCallsAsync(1, WaitTimeout);

        Assert.That(this.upsert.Calls, Has.Count.EqualTo(1));
        Assert.That(this.upsert.Calls[0].Single().Url, Is.EqualTo("/a"));
    }

    /// <summary>
    /// A 30 second interval flushes twice in one minute.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task Interval_Of_30_Seconds_Flushes_Twice_Per_Minute()
    {
        this.state.SetAvailable();
        this.service = this.CreateService(new PageNotFoundTrackerSettings { FlushInterval = TimeSpan.FromSeconds(30) });
        await this.service.StartAsync(CancellationToken.None);
        await this.time.TimerCreated.WaitAsync(WaitTimeout);

        this.queue.TryEnqueue(Hit("/a"));
        this.time.Advance(TimeSpan.FromSeconds(30));
        await this.upsert.WaitForCallsAsync(1, WaitTimeout);

        this.queue.TryEnqueue(Hit("/b"));
        this.time.Advance(TimeSpan.FromSeconds(30));
        await this.upsert.WaitForCallsAsync(2, WaitTimeout);

        Assert.That(this.upsert.Calls.Select(c => c.Single().Url), Is.EqualTo(new[] { "/a", "/b" }));
    }

    /// <summary>
    /// A failed write keeps the hits pending and sends them with the next flush.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task Failed_Upsert_Keeps_Hits_For_Next_Flush()
    {
        this.state.SetAvailable();
        this.service = this.CreateService();
        this.upsert.FailNext = true;
        this.queue.TryEnqueue(Hit("/a"));

        await this.service.FlushAsync(CancellationToken.None);

        Assert.That(this.upsert.Calls, Has.Count.EqualTo(1));
        Assert.That(this.queue.PendingCount, Is.EqualTo(1));

        this.queue.TryEnqueue(Hit("/a"));
        await this.service.FlushAsync(CancellationToken.None);

        Assert.That(this.upsert.Calls, Has.Count.EqualTo(2));
        Assert.That(this.upsert.Calls[1].Single().Count, Is.EqualTo(2));
        Assert.That(this.queue.PendingCount, Is.EqualTo(0));
    }

    /// <summary>
    /// A cancelled write keeps the hits pending (the batch is always completed).
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task Cancelled_Upsert_Keeps_Hits_Pending()
    {
        this.state.SetAvailable();
        this.service = this.CreateService();
        this.upsert.CancelNext = true;
        this.queue.TryEnqueue(Hit("/a"));

        await this.service.FlushAsync(CancellationToken.None);

        Assert.That(this.queue.PendingCount, Is.EqualTo(1));
    }

    /// <summary>
    /// An empty batch does not touch the database.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task Empty_Batch_Touches_No_Database()
    {
        this.state.SetAvailable();
        this.service = this.CreateService();

        await this.service.FlushAsync(CancellationToken.None);

        Assert.That(this.upsert.Calls, Is.Empty);
        this.factory.Verify(f => f.CreateDbContextAsync(It.IsAny<CancellationToken>()), Times.Never);
        this.factory.Verify(f => f.CreateDbContext(), Times.Never);
    }

    /// <summary>
    /// Stopping the service flushes the pending hits.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task StopAsync_Flushes_Pending_Hits()
    {
        this.state.SetAvailable();
        var flushService = this.CreateService();
        await flushService.StartAsync(CancellationToken.None);
        this.queue.TryEnqueue(Hit("/a"));

        await flushService.StopAsync(CancellationToken.None);
        flushService.Dispose();

        Assert.That(this.upsert.Calls, Has.Count.EqualTo(1));
        Assert.That(this.upsert.Calls[0].Single().Url, Is.EqualTo("/a"));
    }

    /// <summary>
    /// Disabled tracking writes nothing and the background loop ends.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task Disabled_Tracking_Writes_Nothing_And_Completes()
    {
        this.state.SetAvailable();
        this.service = this.CreateService(new PageNotFoundTrackerSettings { TrackingEnabled = false });
        await this.service.StartAsync(CancellationToken.None);
        this.queue.TryEnqueue(Hit("/a"));

        await this.service.ExecuteTask!.WaitAsync(WaitTimeout);
        this.time.Advance(TimeSpan.FromMinutes(5));
        await Task.Delay(100);

        Assert.That(this.upsert.Calls, Is.Empty);
    }

    /// <summary>
    /// An unavailable persistence (the gate returns false) writes nothing and the background loop ends.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task Unavailable_Persistence_Writes_Nothing()
    {
        this.state.SetUnavailable();
        this.service = this.CreateService();
        await this.service.StartAsync(CancellationToken.None);
        this.queue.TryEnqueue(Hit("/a"));

        await this.service.ExecuteTask!.WaitAsync(WaitTimeout);
        this.time.Advance(TimeSpan.FromMinutes(5));
        await this.service.FlushAsync(CancellationToken.None);

        Assert.That(this.upsert.Calls, Is.Empty);
    }

    private static TrackedUrlHit Hit(string url) =>
        new(url, "hash" + url, url, url, string.Empty, string.Empty, false, 1, T0, T0);

    private TrackingFlushService CreateService(PageNotFoundTrackerSettings? settings = null)
    {
        var connectionStrings = new Mock<IOptionsMonitor<ConnectionStrings>>();
        connectionStrings.Setup(m => m.CurrentValue).Returns(new ConnectionStrings { ProviderName = ProviderName });
        var serviceProvider = new ServiceCollection()
            .AddSingleton(this.factory.Object)
            .BuildServiceProvider();

        return new TrackingFlushService(
            this.queue,
            this.state,
            new TrackingDatabaseProviderSelector([new FakeProvider(ProviderName, this.upsert)]),
            connectionStrings.Object,
            serviceProvider,
            Options.Create(settings ?? new PageNotFoundTrackerSettings()),
            this.time,
            NullLogger<TrackingFlushService>.Instance);
    }

    private sealed class FakeProvider(string providerName, ITrackedUrlUpsert upsert) : ITrackingDatabaseProvider
    {
        public string ProviderName { get; } = providerName;

        public ITrackedUrlUpsert? Upsert { get; } = upsert;

        public void ConfigureDbContext(DbContextOptionsBuilder options, string connectionString)
        {
        }
    }

    /// <summary>
    /// A fake time provider that signals when a timer is created. <c>BackgroundService</c> runs <c>ExecuteAsync</c>
    /// on the thread pool (.NET 10), so the test waits for the periodic timer before advancing time.
    /// </summary>
    private sealed class TimerAwareTimeProvider(DateTimeOffset startDateTime) : FakeTimeProvider(startDateTime)
    {
        private readonly TaskCompletionSource timerCreated = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task TimerCreated => this.timerCreated.Task;

        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            ITimer timer = base.CreateTimer(callback, state, dueTime, period);
            this.timerCreated.TrySetResult();
            return timer;
        }
    }

    private sealed class RecordingUpsert : ITrackedUrlUpsert
    {
        private readonly object gate = new();
        private readonly List<TrackedUrlHit[]> calls = [];

        public bool FailNext { get; set; }

        public bool CancelNext { get; set; }

        public IReadOnlyList<TrackedUrlHit[]> Calls
        {
            get
            {
                lock (this.gate)
                {
                    return [.. this.calls];
                }
            }
        }

        public Task UpsertAsync(DbContext dbContext, IReadOnlyCollection<TrackedUrlHit> hits, CancellationToken cancellationToken)
        {
            lock (this.gate)
            {
                this.calls.Add([.. hits]);
            }

            if (this.FailNext)
            {
                this.FailNext = false;
                throw new InvalidOperationException("Database down.");
            }

            if (this.CancelNext)
            {
                this.CancelNext = false;
                throw new OperationCanceledException();
            }

            return Task.CompletedTask;
        }

        public async Task WaitForCallsAsync(int count, TimeSpan timeout)
        {
            var deadline = DateTime.UtcNow + timeout;
            while (this.Calls.Count < count)
            {
                if (DateTime.UtcNow > deadline)
                {
                    Assert.Fail($"Expected {count} upsert calls, got {this.Calls.Count}.");
                }

                await Task.Delay(10);
            }
        }
    }
}
