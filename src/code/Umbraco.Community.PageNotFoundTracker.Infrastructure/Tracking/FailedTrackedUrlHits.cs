// Copyright (c) Dave Woestenborghs and contributors. Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Collections;
using Umbraco.Community.PageNotFoundTracker.Core.Tracking;

namespace Umbraco.Community.PageNotFoundTracker.Infrastructure.Tracking;

/// <summary>
/// The hits an upsert did not write, plus the first error it ran into, so the flush can log one summary warning with
/// that error attached instead of one warning per hit.
/// </summary>
internal sealed class FailedTrackedUrlHits : IReadOnlyCollection<TrackedUrlHit>
{
    private readonly List<TrackedUrlHit> hits = [];

    /// <summary>
    /// Gets the first error that made a hit fail, or <c>null</c> when none did (for example only cancellation).
    /// </summary>
    public Exception? FirstException { get; private set; }

    /// <inheritdoc />
    public int Count => this.hits.Count;

    /// <summary>
    /// Adds a hit that was not written.
    /// </summary>
    /// <param name="hit">The hit.</param>
    /// <param name="exception">The error that made it fail, if any.</param>
    public void Add(TrackedUrlHit hit, Exception? exception = null)
    {
        this.hits.Add(hit);
        this.FirstException ??= exception;
    }

    /// <inheritdoc />
    public IEnumerator<TrackedUrlHit> GetEnumerator() => this.hits.GetEnumerator();

    /// <inheritdoc />
    IEnumerator IEnumerable.GetEnumerator() => this.GetEnumerator();
}
