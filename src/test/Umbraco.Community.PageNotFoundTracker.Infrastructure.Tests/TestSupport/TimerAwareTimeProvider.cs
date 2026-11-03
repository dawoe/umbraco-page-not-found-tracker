// Copyright (c) Dave Woestenborghs and contributors. Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.Time.Testing;

namespace Umbraco.Community.PageNotFoundTracker.Infrastructure.Tests.TestSupport;

/// <summary>
/// A fake time provider that signals when a timer is created. <c>BackgroundService</c> runs <c>ExecuteAsync</c>
/// on the thread pool (.NET 10), so a test waits for the timer before advancing time.
/// </summary>
/// <param name="startDateTime">The start time.</param>
internal sealed class TimerAwareTimeProvider(DateTimeOffset startDateTime) : FakeTimeProvider(startDateTime)
{
    private readonly TaskCompletionSource timerCreated = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>
    /// Gets a task that completes when the first timer is created.
    /// </summary>
    public Task TimerCreated => this.timerCreated.Task;

    /// <inheritdoc />
    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        ITimer timer = base.CreateTimer(callback, state, dueTime, period);
        this.timerCreated.TrySetResult();
        return timer;
    }
}
