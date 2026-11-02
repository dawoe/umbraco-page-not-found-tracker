// Copyright (c) Dave Woestenborghs and contributors. Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Collections.Concurrent;
using Umbraco.Community.PageNotFoundTracker.Infrastructure.NotFound;

namespace Umbraco.Community.PageNotFoundTracker.Integration.Tests;

/// <summary>
/// A <see cref="INotFoundRecorder"/> that keeps every occurrence it receives, so tests can assert the signal for spec 3.
/// </summary>
internal sealed class CapturingNotFoundRecorder : INotFoundRecorder
{
    private readonly ConcurrentQueue<NotFoundOccurrence> occurrences = new();

    /// <summary>
    /// Gets the recorded occurrences in the order they were recorded.
    /// </summary>
    public IReadOnlyList<NotFoundOccurrence> Occurrences => this.occurrences.ToList();

    /// <inheritdoc />
    public Task RecordAsync(NotFoundOccurrence occurrence, CancellationToken cancellationToken)
    {
        this.occurrences.Enqueue(occurrence);
        return Task.CompletedTask;
    }
}
