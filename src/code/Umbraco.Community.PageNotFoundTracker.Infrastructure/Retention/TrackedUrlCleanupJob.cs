// Copyright (c) Dave Woestenborghs and contributors. Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Umbraco.Cms.Infrastructure.BackgroundJobs;
using Umbraco.Community.PageNotFoundTracker.Core.Configuration;
using Umbraco.Community.PageNotFoundTracker.Core.Tracking;

namespace Umbraco.Community.PageNotFoundTracker.Infrastructure.Retention;

/// <summary>
/// Removes tracked URLs whose last hit is older than <see cref="PageNotFoundTrackerSettings.CleanupAfterDays"/>, once a
/// day. Umbraco's distributed job table makes one server run it at a time. A run deletes everything due, in batches,
/// and never throws into Umbraco's scheduler.
/// </summary>
internal sealed class TrackedUrlCleanupJob : IDistributedBackgroundJob
{
    /// <summary>
    /// The job's name: the key of its row in Umbraco's <c>umbracoDistributedJob</c> table. Never change it.
    /// </summary>
    internal const string JobName = "PageNotFoundTracker.TrackedUrlCleanup";

    /// <summary>
    /// The maximum number of rows deleted per statement.
    /// </summary>
    internal const int BatchSize = 1000;

    private readonly PageNotFoundTrackerSettings settings;
    private readonly ITrackingPersistenceState persistenceState;
    private readonly ITrackedUrlCleaner cleaner;
    private readonly TimeProvider timeProvider;
    private readonly ILogger<TrackedUrlCleanupJob> logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="TrackedUrlCleanupJob"/> class.
    /// </summary>
    /// <param name="settings">The settings.</param>
    /// <param name="persistenceState">The tracking persistence state (the gate).</param>
    /// <param name="cleaner">The batch deleter.</param>
    /// <param name="timeProvider">The time provider.</param>
    /// <param name="logger">The logger.</param>
    public TrackedUrlCleanupJob(
        IOptions<PageNotFoundTrackerSettings> settings,
        ITrackingPersistenceState persistenceState,
        ITrackedUrlCleaner cleaner,
        TimeProvider timeProvider,
        ILogger<TrackedUrlCleanupJob> logger)
    {
        this.settings = settings.Value;
        this.persistenceState = persistenceState;
        this.cleaner = cleaner;
        this.timeProvider = timeProvider;
        this.logger = logger;
    }

    /// <inheritdoc />
    public string Name => JobName;

    /// <inheritdoc />
    public TimeSpan Period => TimeSpan.FromDays(1);

    /// <inheritdoc />
    public Task ExecuteAsync() => this.ExecuteAsync(CancellationToken.None);

    /// <inheritdoc />
    public async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        if (!this.settings.CleanupEnabled)
        {
            this.logger.LogDebug("Clean-up of tracked 404 URLs is switched off.");
            return;
        }

        try
        {
            if (!await this.persistenceState.WaitUntilReadyAsync(cancellationToken))
            {
                // Unavailable: the migration service already logged why.
                return;
            }
        }
        catch (Exception) when (cancellationToken.IsCancellationRequested)
        {
            return;
        }

        DateTime cutoff = this.GetCutoff();
        int removed = 0;
        try
        {
            // Continue while a batch selected a full set of due rows, even when fewer were deleted (a row seen again or
            // removed in between): rows behind it may still be due. Cannot loop forever, a row seen again is no longer due.
            CleanupBatch batch;
            do
            {
                cancellationToken.ThrowIfCancellationRequested();
                batch = await this.cleaner.DeleteBatchAsync(cutoff, BatchSize, cancellationToken);
                removed += batch.Deleted;
            }
            while (batch.Selected >= BatchSize);

            this.logger.Log(
                removed > 0 ? LogLevel.Information : LogLevel.Debug,
                "Removed {Count} tracked 404 URLs last seen before {Cutoff:u}.",
                removed,
                cutoff);
        }
        catch (Exception) when (cancellationToken.IsCancellationRequested)
        {
            // A shutdown, whatever the provider threw for it: SqlClient reports a cancelled command as a SqlException
            // ("Operation cancelled by user"), not as an OperationCanceledException.
            this.logger.Log(
                removed > 0 ? LogLevel.Information : LogLevel.Debug,
                "Clean-up of tracked 404 URLs stopped by shutdown after removing {Count}.",
                removed);
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "Clean-up of tracked 404 URLs failed after removing {Count}.", removed);
        }
    }

    private DateTime GetCutoff()
    {
        DateTime now = this.timeProvider.GetUtcNow().UtcDateTime;
        int days = this.settings.EffectiveCleanupAfterDays;

        // An age beyond the calendar would underflow DateTime: nothing can be that old, so nothing is deleted.
        return days >= (now - DateTime.MinValue).TotalDays
            ? DateTime.SpecifyKind(DateTime.MinValue, DateTimeKind.Utc)
            : now.AddDays(-days);
    }
}
