// Copyright (c) Dave Woestenborghs and contributors. Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Moq;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Configuration.Models;
using Umbraco.Cms.Core.Services;
using Umbraco.Community.PageNotFoundTracker.Core.Tracking;
using Umbraco.Community.PageNotFoundTracker.Infrastructure.Tracking;

namespace Umbraco.Community.PageNotFoundTracker.Infrastructure.Tests;

/// <summary>
/// Unit tests for the <see cref="TrackingMigrationService"/> class.
/// </summary>
[TestFixture]
internal sealed class TrackingMigrationServiceTests
{
    private const string ProviderName = "Microsoft.Data.Sqlite";

    private static readonly TimeSpan WaitTimeout = TimeSpan.FromSeconds(5);

    private TimerAwareTimeProvider time = null!;
    private TrackingPersistenceState state = null!;
    private Mock<IRuntimeState> runtimeState = null!;
    private Mock<IDbContextFactory<PageNotFoundTrackerDbContext>> factory = null!;
    private CapturingLogger logger = null!;
    private int migrateCalls;
    private TrackingMigrationService? service;

    /// <summary>
    /// Creates the shared fakes.
    /// </summary>
    [SetUp]
    public void SetUp()
    {
        this.time = new TimerAwareTimeProvider(DateTimeOffset.UtcNow);
        this.state = new TrackingPersistenceState();
        this.runtimeState = new Mock<IRuntimeState>();
        this.runtimeState.Setup(r => r.Level).Returns(RuntimeLevel.Run);
        this.factory = new Mock<IDbContextFactory<PageNotFoundTrackerDbContext>>();
        this.factory.Setup(f => f.CreateDbContextAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => new PageNotFoundTrackerDbContext(new DbContextOptionsBuilder<PageNotFoundTrackerDbContext>().Options));
        this.logger = new CapturingLogger();
        this.migrateCalls = 0;
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
    /// An unknown provider logs one error naming it, disables tracking and never resolves the context factory.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task Unknown_Provider_Sets_Unavailable_Without_Resolving_Factory()
    {
        var serviceProvider = new Mock<IServiceProvider>(MockBehavior.Strict);
        this.service = this.CreateService("Some.Unknown", serviceProvider.Object);

        await this.service.StartAsync(CancellationToken.None);

        Assert.That(await this.state.WaitUntilReadyAsync(CancellationToken.None).WaitAsync(WaitTimeout), Is.False);
        Assert.That(this.state.Status, Is.EqualTo(TrackingPersistenceStatus.Unavailable));
        Assert.That(this.logger.Errors, Has.Count.EqualTo(1));
        Assert.That(this.logger.Errors[0], Does.Contain("Some.Unknown"));
        Assert.That(this.migrateCalls, Is.EqualTo(0));
        serviceProvider.VerifyNoOtherCalls();
    }

    /// <summary>
    /// Nothing happens while Umbraco installs; once it runs the migration runs once and the state becomes available.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task Waits_For_Run_Then_Migrates_Once()
    {
        RuntimeLevel level = RuntimeLevel.Install;
        this.runtimeState.Setup(r => r.Level).Returns(() => level);
        this.service = this.CreateService(ProviderName);

        await this.service.StartAsync(CancellationToken.None);
        await this.time.TimerCreated.WaitAsync(WaitTimeout);

        Assert.That(this.migrateCalls, Is.EqualTo(0));
        Assert.That(this.state.Status, Is.EqualTo(TrackingPersistenceStatus.Pending));

        level = RuntimeLevel.Run;
        this.time.Advance(TimeSpan.FromSeconds(1));

        Assert.That(await this.state.WaitUntilReadyAsync(CancellationToken.None).WaitAsync(WaitTimeout), Is.True);
        Assert.That(this.migrateCalls, Is.EqualTo(1));
        Assert.That(this.state.Status, Is.EqualTo(TrackingPersistenceStatus.Available));
    }

    /// <summary>
    /// A failing migration is logged and disables tracking.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task Failing_Migration_Sets_Unavailable()
    {
        this.service = this.CreateService(ProviderName, migrate: (_, _) => throw new InvalidOperationException("boom"));

        await this.service.StartAsync(CancellationToken.None);

        Assert.That(await this.state.WaitUntilReadyAsync(CancellationToken.None).WaitAsync(WaitTimeout), Is.False);
        Assert.That(this.state.Status, Is.EqualTo(TrackingPersistenceStatus.Unavailable));
        Assert.That(this.logger.Errors, Has.Count.EqualTo(1));
    }

    /// <summary>
    /// Stopping before Umbraco runs leaves the state pending.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task Cancellation_Before_Run_Leaves_State_Pending()
    {
        this.runtimeState.Setup(r => r.Level).Returns(RuntimeLevel.Install);
        this.service = this.CreateService(ProviderName);

        await this.service.StartAsync(CancellationToken.None);
        await this.time.TimerCreated.WaitAsync(WaitTimeout);
        await this.service.StopAsync(CancellationToken.None);

        Assert.That(this.state.Status, Is.EqualTo(TrackingPersistenceStatus.Pending));
        Assert.That(this.migrateCalls, Is.EqualTo(0));
    }

    private TrackingMigrationService CreateService(
        string providerName,
        IServiceProvider? serviceProvider = null,
        Func<PageNotFoundTrackerDbContext, CancellationToken, Task>? migrate = null)
    {
        var connectionStrings = new Mock<IOptionsMonitor<ConnectionStrings>>();
        connectionStrings.Setup(m => m.CurrentValue).Returns(new ConnectionStrings { ProviderName = providerName });
        serviceProvider ??= new ServiceCollection().AddSingleton(this.factory.Object).BuildServiceProvider();

        var result = new TrackingMigrationService(
            this.runtimeState.Object,
            this.state,
            new TrackingDatabaseProviderSelector([new FakeProvider(ProviderName)]),
            connectionStrings.Object,
            serviceProvider,
            this.time,
            this.logger);
        result.MigrateAsync = migrate ?? ((_, _) =>
        {
            Interlocked.Increment(ref this.migrateCalls);
            return Task.CompletedTask;
        });
        return result;
    }

    private sealed class FakeProvider(string providerName) : ITrackingDatabaseProvider
    {
        public string ProviderName { get; } = providerName;

        public ITrackedUrlUpsert? Upsert => null;

        public void ConfigureDbContext(DbContextOptionsBuilder options, string connectionString)
        {
        }
    }

    private sealed class CapturingLogger : ILogger<TrackingMigrationService>
    {
        private readonly object gate = new();
        private readonly List<string> errors = [];

        public IReadOnlyList<string> Errors
        {
            get
            {
                lock (this.gate)
                {
                    return [.. this.errors];
                }
            }
        }

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => NullLogger.Instance.BeginScope(state);

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (logLevel == LogLevel.Error)
            {
                lock (this.gate)
                {
                    this.errors.Add(formatter(state, exception));
                }
            }
        }
    }

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
}
