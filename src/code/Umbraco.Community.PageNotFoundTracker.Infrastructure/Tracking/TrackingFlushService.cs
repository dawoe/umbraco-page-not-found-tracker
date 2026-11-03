// Copyright (c) Dave Woestenborghs and contributors. Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Umbraco.Cms.Core.Configuration.Models;
using Umbraco.Community.PageNotFoundTracker.Core.Configuration;
using Umbraco.Community.PageNotFoundTracker.Core.Tracking;

namespace Umbraco.Community.PageNotFoundTracker.Infrastructure.Tracking;

/// <summary>
/// Writes the queued hits to the database on a timer, once the tracking persistence is available, and once more when
/// the application stops.
/// </summary>
internal sealed class TrackingFlushService : BackgroundService, ITrackingFlusher
{
    private readonly SemaphoreSlim flushLock = new(1, 1);
    private readonly DefaultTrackedUrlUpsert defaultUpsert;
    private readonly TrackingQueue queue;
    private readonly ITrackingPersistenceState persistenceState;
    private readonly TrackingDatabaseProviderSelector providerSelector;
    private readonly IOptionsMonitor<ConnectionStrings> connectionStrings;
    private readonly IServiceProvider serviceProvider;
    private readonly PageNotFoundTrackerSettings settings;
    private readonly TimeProvider timeProvider;
    private readonly ILogger<TrackingFlushService> logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="TrackingFlushService"/> class.
    /// </summary>
    /// <param name="queue">The queue of hits.</param>
    /// <param name="persistenceState">The persistence state (the gate).</param>
    /// <param name="providerSelector">The database provider selector.</param>
    /// <param name="connectionStrings">Umbraco's connection string options.</param>
    /// <param name="serviceProvider">The service provider; the context factory is resolved from it at flush time only.</param>
    /// <param name="defaultUpsert">The generic upsert, used when the provider has none of its own.</param>
    /// <param name="settings">The settings.</param>
    /// <param name="timeProvider">The time provider.</param>
    /// <param name="logger">The logger.</param>
    public TrackingFlushService(
        TrackingQueue queue,
        ITrackingPersistenceState persistenceState,
        TrackingDatabaseProviderSelector providerSelector,
        IOptionsMonitor<ConnectionStrings> connectionStrings,
        IServiceProvider serviceProvider,
        DefaultTrackedUrlUpsert defaultUpsert,
        IOptions<PageNotFoundTrackerSettings> settings,
        TimeProvider timeProvider,
        ILogger<TrackingFlushService> logger)
    {
        this.queue = queue;
        this.persistenceState = persistenceState;
        this.providerSelector = providerSelector;
        this.connectionStrings = connectionStrings;
        this.serviceProvider = serviceProvider;
        this.defaultUpsert = defaultUpsert;
        this.settings = settings.Value;
        this.timeProvider = timeProvider;
        this.logger = logger;
    }

    /// <inheritdoc />
    public async Task FlushAsync(CancellationToken cancellationToken)
    {
        if (this.persistenceState.Status != TrackingPersistenceStatus.Available)
        {
            return;
        }

        try
        {
            await this.flushLock.WaitAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        try
        {
            await this.FlushBatchAsync(cancellationToken);
        }
        finally
        {
            this.flushLock.Release();
        }
    }

    /// <inheritdoc />
    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await base.StopAsync(cancellationToken);
        if (!this.settings.TrackingEnabled)
        {
            return;
        }

        await this.FlushAsync(cancellationToken);

        // Whatever is still queued now (the final flush was cancelled, ran past the shutdown deadline or failed) is
        // lost with the process.
        int lost = this.queue.Count;
        if (lost > 0)
        {
            this.logger.LogWarning(
                "{Count} tracked 404 URLs could not be written before shutdown and are lost.",
                lost);
        }
    }

    /// <inheritdoc />
    public override void Dispose()
    {
        base.Dispose();
        this.flushLock.Dispose();
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!this.settings.TrackingEnabled)
        {
            return;
        }

        try
        {
            if (!await this.persistenceState.WaitUntilReadyAsync(stoppingToken))
            {
                return;
            }

            using var timer = new PeriodicTimer(this.settings.EffectiveFlushInterval, this.timeProvider);
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                await this.FlushAsync(stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Stopping: StopAsync runs the final flush.
        }
    }

    private async Task FlushBatchAsync(CancellationToken cancellationToken)
    {
        IReadOnlyCollection<TrackedUrlHit> batch = this.queue.TakeBatch();

        // Until the upsert reports otherwise the whole batch counts as not written (also when it throws).
        IReadOnlyCollection<TrackedUrlHit> failed = batch;
        try
        {
            if (batch.Count == 0)
            {
                return;
            }

            ITrackingDatabaseProvider? provider = this.providerSelector.Select(this.connectionStrings.CurrentValue.ProviderName);
            ITrackedUrlUpsert upsert = provider?.Upsert ?? this.defaultUpsert;
            IDbContextFactory<PageNotFoundTrackerDbContext> factory =
                this.serviceProvider.GetRequiredService<IDbContextFactory<PageNotFoundTrackerDbContext>>();
            await using PageNotFoundTrackerDbContext dbContext = await factory.CreateDbContextAsync(cancellationToken);
            failed = await upsert.UpsertAsync(dbContext, batch, cancellationToken);
            if (failed.Count == 0)
            {
                return;
            }

            if (cancellationToken.IsCancellationRequested)
            {
                this.logger.LogDebug(
                    "Writing tracked 404 URLs was cancelled; {Failed} of {Count} stay pending.",
                    failed.Count,
                    batch.Count);
            }
            else
            {
                this.logger.LogWarning(
                    (failed as FailedTrackedUrlHits)?.FirstException,
                    "{Failed} of {Count} tracked 404 URLs could not be written; they stay pending for the next flush.",
                    failed.Count,
                    batch.Count);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            this.logger.LogDebug("Writing {Count} tracked 404 URLs was cancelled; they stay pending.", batch.Count);
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "Writing {Count} tracked 404 URLs failed; they stay pending for the next flush.", batch.Count);
        }
        finally
        {
            this.queue.CompleteBatch(failed);
        }
    }
}
