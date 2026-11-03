// Copyright (c) Dave Woestenborghs and contributors. Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Umbraco.Community.PageNotFoundTracker.Core.Configuration;
using Umbraco.Community.PageNotFoundTracker.Core.Tracking;
using Umbraco.Community.PageNotFoundTracker.Infrastructure.NotFound;

namespace Umbraco.Community.PageNotFoundTracker.Infrastructure.Tracking;

/// <summary>
/// The <see cref="INotFoundRecorder"/> that normalises an occurrence and queues it in memory. Never awaits I/O and
/// never throws.
/// </summary>
internal sealed class NotFoundTracker : INotFoundRecorder
{
    private readonly TrackingQueue queue;
    private readonly ITrackingPersistenceState persistenceState;
    private readonly IOptions<PageNotFoundTrackerSettings> settings;
    private readonly TimeProvider timeProvider;
    private readonly ILogger<NotFoundTracker> logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="NotFoundTracker"/> class.
    /// </summary>
    /// <param name="queue">The in-memory queue.</param>
    /// <param name="persistenceState">The persistence state.</param>
    /// <param name="settings">The settings.</param>
    /// <param name="timeProvider">The time provider.</param>
    /// <param name="logger">The logger.</param>
    public NotFoundTracker(
        TrackingQueue queue,
        ITrackingPersistenceState persistenceState,
        IOptions<PageNotFoundTrackerSettings> settings,
        TimeProvider timeProvider,
        ILogger<NotFoundTracker> logger)
    {
        this.queue = queue;
        this.persistenceState = persistenceState;
        this.settings = settings;
        this.timeProvider = timeProvider;
        this.logger = logger;
    }

    /// <inheritdoc />
    public Task RecordAsync(NotFoundOccurrence occurrence, CancellationToken cancellationToken)
    {
        try
        {
            PageNotFoundTrackerSettings current = this.settings.Value;
            if (!current.TrackingEnabled || this.persistenceState.Status == TrackingPersistenceStatus.Unavailable)
            {
                return Task.CompletedTask;
            }

            this.queue.TryEnqueue(TrackedUrlNormalizer.Normalize(occurrence, current.IncludeQueryString, this.timeProvider.GetUtcNow().UtcDateTime));
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "Tracking a 404 request failed.");
        }

        return Task.CompletedTask;
    }
}
