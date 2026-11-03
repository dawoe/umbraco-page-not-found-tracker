// Copyright (c) Dave Woestenborghs and contributors. Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Umbraco.Community.PageNotFoundTracker.Core.Configuration;
using Umbraco.Community.PageNotFoundTracker.Core.Tracking;
using Umbraco.Community.PageNotFoundTracker.Infrastructure.Tracking;

namespace Umbraco.Community.PageNotFoundTracker.Infrastructure.Tests;

/// <summary>
/// Unit tests for the <see cref="TrackingQueue"/> class.
/// </summary>
[TestFixture]
internal sealed class TrackingQueueTests
{
    private static readonly DateTime T0 = new(2026, 11, 3, 10, 0, 0, DateTimeKind.Utc);

    /// <summary>
    /// Hits with the same identity are merged.
    /// </summary>
    [Test]
    public void TakeBatch_Merges_Same_Identity()
    {
        var queue = CreateQueue(out _, out _);
        queue.TryEnqueue(Hit("/a", "A1", T0.AddMinutes(2)));
        queue.TryEnqueue(Hit("/a", "A0", T0));
        queue.TryEnqueue(Hit("/a", "A2", T0.AddMinutes(5)));
        queue.TryEnqueue(Hit("/b", "B", T0));

        var batch = queue.TakeBatch();

        Assert.That(batch, Has.Count.EqualTo(2));
        var a = batch.Single(h => h.Url == "/a");
        Assert.Multiple(() =>
        {
            Assert.That(a.Count, Is.EqualTo(3));
            Assert.That(a.FirstSeen, Is.EqualTo(T0));
            Assert.That(a.LastSeen, Is.EqualTo(T0.AddMinutes(5)));
            Assert.That(a.DisplayUrl, Is.EqualTo("A0"));
        });
    }

    /// <summary>
    /// A different domain or culture is a different identity.
    /// </summary>
    [Test]
    public void TakeBatch_Keeps_Domain_And_Culture_Apart()
    {
        var queue = CreateQueue(out _, out _);
        queue.TryEnqueue(Hit("/a", "a", T0, "x.com", "en"));
        queue.TryEnqueue(Hit("/a", "a", T0, "y.com", "en"));
        queue.TryEnqueue(Hit("/a", "a", T0, "x.com", "nl"));

        Assert.That(queue.TakeBatch(), Has.Count.EqualTo(3));
    }

    /// <summary>
    /// A full channel drops the hit and warns once per minute.
    /// </summary>
    [Test]
    public void TryEnqueue_Full_Drops_And_Warns_Once_Per_Minute()
    {
        var queue = CreateQueue(out var time, out var logger, capacity: 2);

        Assert.That(queue.TryEnqueue(Hit("/1", "1", T0)), Is.True);
        Assert.That(queue.TryEnqueue(Hit("/2", "2", T0)), Is.True);
        Assert.That(queue.TryEnqueue(Hit("/3", "3", T0)), Is.False);
        Assert.That(queue.TryEnqueue(Hit("/4", "4", T0)), Is.False);
        Assert.That(logger.Warnings, Has.Count.EqualTo(1));
        Assert.That(logger.Warnings[0], Does.Contain("dropped 1 hits since the last warning"));

        time.Advance(TimeSpan.FromMinutes(1));
        Assert.That(queue.TryEnqueue(Hit("/5", "5", T0)), Is.False);

        // The capacity is also 2, so assert on the dropped-count wording, not on the digit alone.
        Assert.That(logger.Warnings, Has.Count.EqualTo(2));
        Assert.That(logger.Warnings[1], Does.Contain("dropped 2 hits since the last warning"));
    }

    /// <summary>
    /// A failed batch is put back and merged with later hits.
    /// </summary>
    [Test]
    public void CompleteBatch_Failure_Restores_And_Merges()
    {
        var queue = CreateQueue(out _, out _);
        queue.TryEnqueue(Hit("/a", "a", T0));
        Assert.That(queue.TakeBatch(), Has.Count.EqualTo(1));

        queue.CompleteBatch(false);
        Assert.That(queue.PendingCount, Is.EqualTo(1));

        queue.TryEnqueue(Hit("/a", "a", T0.AddMinutes(1)));
        var batch = queue.TakeBatch();

        Assert.That(batch, Has.Count.EqualTo(1));
        Assert.That(batch.Single().Count, Is.EqualTo(2));
    }

    /// <summary>
    /// A failed batch over the bound keeps the newest entries.
    /// </summary>
    [Test]
    public void CompleteBatch_Failure_Over_Bound_Keeps_Newest()
    {
        var queue = CreateQueue(out _, out var logger, capacity: 2);
        queue.TryEnqueue(Hit("/old", "old", T0));
        queue.TryEnqueue(Hit("/mid", "mid", T0.AddMinutes(1)));
        queue.TakeBatch();
        queue.CompleteBatch(false);
        queue.TryEnqueue(Hit("/new", "new", T0.AddMinutes(2)));
        Assert.That(queue.TakeBatch(), Has.Count.EqualTo(3));

        queue.CompleteBatch(false);

        Assert.That(queue.PendingCount, Is.EqualTo(2));
        Assert.That(logger.Warnings, Has.Count.EqualTo(1));
        var urls = queue.TakeBatch().Select(h => h.Url).OrderBy(u => u, StringComparer.Ordinal).ToArray();
        Assert.That(urls, Is.EqualTo(new[] { "/mid", "/new" }));
    }

    /// <summary>
    /// A successful batch leaves nothing pending.
    /// </summary>
    [Test]
    public void CompleteBatch_Success_Clears()
    {
        var queue = CreateQueue(out _, out _);
        queue.TryEnqueue(Hit("/a", "a", T0));
        queue.TakeBatch();

        queue.CompleteBatch(true);

        Assert.That(queue.PendingCount, Is.EqualTo(0));
        Assert.That(queue.TakeBatch(), Is.Empty);
    }

    private static TrackedUrlHit Hit(string url, string display, DateTime seen, string domain = "", string culture = "") =>
        new(url, "hash" + url, url, display, domain, culture, false, 1, seen, seen);

    private static TrackingQueue CreateQueue(out FakeTimeProvider time, out CapturingLogger<TrackingQueue> logger, int capacity = 100)
    {
        time = new FakeTimeProvider(new DateTimeOffset(T0));
        logger = new CapturingLogger<TrackingQueue>();
        var settings = Options.Create(new PageNotFoundTrackerSettings { ChannelCapacity = capacity });
        return new TrackingQueue(settings, time, logger);
    }

    /// <summary>
    /// A logger that keeps warning and error messages.
    /// </summary>
    /// <typeparam name="T">The category type.</typeparam>
    internal sealed class CapturingLogger<T> : ILogger<T>
    {
        /// <summary>
        /// Gets the warning messages.
        /// </summary>
        public List<string> Warnings { get; } = [];

        /// <summary>
        /// Gets the error messages.
        /// </summary>
        public List<string> Errors { get; } = [];

        /// <inheritdoc />
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        /// <inheritdoc />
        public bool IsEnabled(LogLevel logLevel) => true;

        /// <inheritdoc />
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (logLevel == LogLevel.Warning)
            {
                this.Warnings.Add(formatter(state, exception));
            }
            else if (logLevel == LogLevel.Error)
            {
                this.Errors.Add(formatter(state, exception));
            }
        }
    }
}
