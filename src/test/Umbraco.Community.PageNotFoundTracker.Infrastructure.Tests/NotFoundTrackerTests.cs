// Copyright (c) Dave Woestenborghs and contributors. Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Umbraco.Community.PageNotFoundTracker.Core.Configuration;
using Umbraco.Community.PageNotFoundTracker.Core.Tracking;
using Umbraco.Community.PageNotFoundTracker.Infrastructure.NotFound;
using Umbraco.Community.PageNotFoundTracker.Infrastructure.Tests.TestSupport;
using Umbraco.Community.PageNotFoundTracker.Infrastructure.Tracking;

namespace Umbraco.Community.PageNotFoundTracker.Infrastructure.Tests;

/// <summary>
/// Unit tests for the <see cref="NotFoundTracker"/> class.
/// </summary>
[TestFixture]
internal sealed class NotFoundTrackerTests
{
    /// <summary>
    /// Nothing is queued when tracking is disabled.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RecordAsync_Disabled_Queues_Nothing()
    {
        var (tracker, queue) = Create(new PageNotFoundTrackerSettings { TrackingEnabled = false }, TrackingPersistenceStatus.Available);

        await tracker.RecordAsync(Occurrence("/missing"), CancellationToken.None);

        Assert.That(queue.TakeBatch(), Is.Empty);
    }

    /// <summary>
    /// Nothing is queued when persistence is unavailable.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RecordAsync_Unavailable_Queues_Nothing()
    {
        var (tracker, queue) = Create(new PageNotFoundTrackerSettings(), TrackingPersistenceStatus.Unavailable);

        await tracker.RecordAsync(Occurrence("/missing"), CancellationToken.None);

        Assert.That(queue.TakeBatch(), Is.Empty);
    }

    /// <summary>
    /// A hit is queued while persistence is pending.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RecordAsync_Pending_Queues()
    {
        var (tracker, queue) = Create(new PageNotFoundTrackerSettings(), TrackingPersistenceStatus.Pending);

        await tracker.RecordAsync(Occurrence("/Missing/"), CancellationToken.None);

        var hit = queue.TakeBatch().Single();
        Assert.That(hit.Url, Is.EqualTo("/missing"));
    }

    /// <summary>
    /// Odd input never throws.
    /// </summary>
    /// <param name="path">The raw path and query.</param>
    [TestCase("")]
    [TestCase("%%%?\u0000")]
    public void RecordAsync_Odd_Input_Does_Not_Throw(string path)
    {
        var (tracker, _) = Create(new PageNotFoundTrackerSettings(), TrackingPersistenceStatus.Available);

        Task? task = null;
        Assert.DoesNotThrow(() => task = tracker.RecordAsync(Occurrence(path), CancellationToken.None));
        Assert.That(task!.IsCompleted, Is.True);
    }

    private static NotFoundOccurrence Occurrence(string path) => new(path, null, null, false, true);

    private static (NotFoundTracker Tracker, TrackingQueue Queue) Create(
        PageNotFoundTrackerSettings settings,
        TrackingPersistenceStatus status)
    {
        var options = Options.Create(settings);
        var time = new FakeTimeProvider();
        var queue = new TrackingQueue(options, time, new CapturingLogger<TrackingQueue>());
        var state = new TrackingPersistenceState();
        if (status == TrackingPersistenceStatus.Available)
        {
            state.SetAvailable();
        }
        else if (status == TrackingPersistenceStatus.Unavailable)
        {
            state.SetUnavailable();
        }

        var tracker = new NotFoundTracker(queue, state, options, time, new CapturingLogger<NotFoundTracker>());
        return (tracker, queue);
    }
}
