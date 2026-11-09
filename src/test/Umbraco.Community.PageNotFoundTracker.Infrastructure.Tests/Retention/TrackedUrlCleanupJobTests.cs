// Copyright (c) Dave Woestenborghs and contributors. Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Umbraco.Community.PageNotFoundTracker.Core.Configuration;
using Umbraco.Community.PageNotFoundTracker.Infrastructure.Retention;
using Umbraco.Community.PageNotFoundTracker.Infrastructure.Tests.TestSupport;
using Umbraco.Community.PageNotFoundTracker.Infrastructure.Tracking;

namespace Umbraco.Community.PageNotFoundTracker.Infrastructure.Tests.Retention;

/// <summary>
/// Unit tests for the <see cref="TrackedUrlCleanupJob"/> class.
/// </summary>
[TestFixture]
internal sealed class TrackedUrlCleanupJobTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 9, 12, 0, 0, TimeSpan.Zero);

    private FakeCleaner cleaner = null!;
    private TrackingPersistenceState state = null!;
    private CapturingLogger<TrackedUrlCleanupJob> logger = null!;

    /// <summary>
    /// Creates a cleaner fake, an available persistence state and a capturing logger.
    /// </summary>
    [SetUp]
    public void SetUp()
    {
        this.cleaner = new FakeCleaner();
        this.state = new TrackingPersistenceState();
        this.state.SetAvailable();
        this.logger = new CapturingLogger<TrackedUrlCleanupJob>();
    }

    /// <summary>
    /// The job's name and period are fixed: the name is the key of Umbraco's job row and must never change.
    /// </summary>
    [Test]
    public void Name_and_period_are_fixed()
    {
        TrackedUrlCleanupJob job = this.CreateJob(new PageNotFoundTrackerSettings());

        Assert.Multiple(() =>
        {
            Assert.That(job.Name, Is.EqualTo("PageNotFoundTracker.TrackedUrlCleanup"));
            Assert.That(job.Period, Is.EqualTo(TimeSpan.FromDays(1)));
        });
    }

    /// <summary>
    /// With the clean-up switched off nothing is deleted.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task Disabled_does_nothing()
    {
        await this.CreateJob(new PageNotFoundTrackerSettings { CleanupEnabled = false }).ExecuteAsync(CancellationToken.None);

        Assert.That(this.cleaner.Cutoffs, Is.Empty);
    }

    /// <summary>
    /// With the tracking database unavailable nothing is deleted and nothing is logged as a problem (the migration
    /// service already logged why).
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task Unavailable_persistence_does_nothing()
    {
        this.state = new TrackingPersistenceState();
        this.state.SetUnavailable();

        await this.CreateJob(new PageNotFoundTrackerSettings()).ExecuteAsync(CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(this.cleaner.Cutoffs, Is.Empty);
            Assert.That(this.logger.Warnings, Is.Empty);
            Assert.That(this.logger.Errors, Is.Empty);
        });
    }

    /// <summary>
    /// A run that starts while the database is still being migrated waits for it instead of skipping a day.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task Waits_for_pending_persistence_then_runs()
    {
        this.state = new TrackingPersistenceState();
        Task run = this.CreateJob(new PageNotFoundTrackerSettings()).ExecuteAsync(CancellationToken.None);
        await Task.Delay(50);
        bool ranEarly = run.IsCompleted || this.cleaner.Cutoffs.Count > 0;

        this.state.SetAvailable();
        await run.WaitAsync(TimeSpan.FromSeconds(10));

        Assert.Multiple(() =>
        {
            Assert.That(ranEarly, Is.False);
            Assert.That(this.cleaner.Cutoffs, Has.Count.EqualTo(1));
        });
    }

    /// <summary>
    /// The cut-off is now minus the configured number of days, in UTC, and batches of 1000 are asked for.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task Passes_cutoff_of_now_minus_configured_days()
    {
        await this.CreateJob(new PageNotFoundTrackerSettings { CleanupAfterDays = 30 }).ExecuteAsync(CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(this.cleaner.Cutoffs, Is.EqualTo(new[] { new DateTime(2026, 10, 10, 12, 0, 0, DateTimeKind.Utc) }));
            Assert.That(this.cleaner.Cutoffs[0].Kind, Is.EqualTo(DateTimeKind.Utc));
            Assert.That(this.cleaner.BatchSizes, Is.EqualTo(new[] { 1000 }));
        });
    }

    /// <summary>
    /// Full batches are followed by another one until a batch is not full; the total is logged once.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task Loops_until_a_batch_is_not_full_and_logs_total()
    {
        this.cleaner.Results.Enqueue(_ => 1000);
        this.cleaner.Results.Enqueue(_ => 1000);
        this.cleaner.Results.Enqueue(_ => 3);

        await this.CreateJob(new PageNotFoundTrackerSettings()).ExecuteAsync(CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(this.cleaner.Cutoffs, Has.Count.EqualTo(3));
            Assert.That(this.logger.Informations, Has.Count.EqualTo(1));
            Assert.That(this.logger.Informations[0], Does.Contain("2003"));
        });
    }

    /// <summary>
    /// A run that finds nothing to delete runs one batch and logs nothing at information level.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task Nothing_due_logs_no_information()
    {
        await this.CreateJob(new PageNotFoundTrackerSettings()).ExecuteAsync(CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(this.cleaner.Cutoffs, Has.Count.EqualTo(1));
            Assert.That(this.logger.Informations, Is.Empty);
        });
    }

    /// <summary>
    /// An age larger than the calendar allows does not throw and deletes nothing.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task Huge_age_deletes_nothing_and_does_not_throw()
    {
        TrackedUrlCleanupJob job = this.CreateJob(new PageNotFoundTrackerSettings { CleanupAfterDays = int.MaxValue });

        Assert.DoesNotThrowAsync(() => job.ExecuteAsync(CancellationToken.None));
        Assert.Multiple(() =>
        {
            Assert.That(this.cleaner.Cutoffs, Is.All.EqualTo(DateTime.MinValue));
            Assert.That(this.logger.Errors, Is.Empty);
        });
    }

    /// <summary>
    /// A shutdown in the middle of a run stops between batches, logs what was removed and does not throw.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task Cancellation_stops_between_batches_and_logs_partial_count()
    {
        using var shutdown = new CancellationTokenSource();
        this.cleaner.Results.Enqueue(_ => 1000);
        this.cleaner.Results.Enqueue(_ =>
        {
            shutdown.Cancel();
            return 1000;
        });
        this.cleaner.Results.Enqueue(ct =>
        {
            ct.ThrowIfCancellationRequested();
            return 1000;
        });
        TrackedUrlCleanupJob job = this.CreateJob(new PageNotFoundTrackerSettings());

        Assert.DoesNotThrowAsync(() => job.ExecuteAsync(shutdown.Token));
        Assert.Multiple(() =>
        {
            Assert.That(this.logger.Informations, Has.Count.EqualTo(1));
            Assert.That(this.logger.Informations[0], Does.Contain("2000"));
            Assert.That(this.logger.Errors, Is.Empty);
        });
        await Task.CompletedTask;
    }

    /// <summary>
    /// A failing batch is logged as an error with the count removed so far and never thrown into Umbraco's scheduler.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task Cleaner_failure_is_logged_and_swallowed()
    {
        var failure = new InvalidOperationException("database down");
        this.cleaner.Results.Enqueue(_ => 1000);
        this.cleaner.Results.Enqueue(_ => throw failure);
        TrackedUrlCleanupJob job = this.CreateJob(new PageNotFoundTrackerSettings());

        Assert.DoesNotThrowAsync(() => job.ExecuteAsync(CancellationToken.None));
        Assert.Multiple(() =>
        {
            Assert.That(this.logger.Errors, Has.Count.EqualTo(1));
            Assert.That(this.logger.Errors[0], Does.Contain("1000"));
            Assert.That(this.logger.ErrorExceptions[0], Is.SameAs(failure));
        });
        await Task.CompletedTask;
    }

    private TrackedUrlCleanupJob CreateJob(PageNotFoundTrackerSettings settings) =>
        new(Options.Create(settings), this.state, this.cleaner, new FakeTimeProvider(Now), this.logger);

    private sealed class FakeCleaner : ITrackedUrlCleaner
    {
        public Queue<Func<CancellationToken, int>> Results { get; } = new();

        public List<DateTime> Cutoffs { get; } = [];

        public List<int> BatchSizes { get; } = [];

        public Task<int> DeleteBatchAsync(DateTime cutoff, int batchSize, CancellationToken cancellationToken)
        {
            this.Cutoffs.Add(cutoff);
            this.BatchSizes.Add(batchSize);
            int result = this.Results.Count > 0 ? this.Results.Dequeue()(cancellationToken) : 0;
            return Task.FromResult(result);
        }
    }
}
