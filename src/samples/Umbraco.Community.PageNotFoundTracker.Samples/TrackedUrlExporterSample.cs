// Copyright (c) Dave Woestenborghs and contributors. Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Runtime.CompilerServices;
using Umbraco.Community.PageNotFoundTracker.Core.Tracking;

namespace Umbraco.Community.PageNotFoundTracker.Samples;

// docs:begin exporter-class

/// <summary>
/// Sample tracked URL exporter. The dashboard export asks it for every row that matches the query and writes each row
/// to the CSV as it arrives, so it must read lazily and never collect the rows first.
/// </summary>
public sealed class TrackedUrlExporterSample : ITrackedUrlExporter
{
    /// <inheritdoc />
    public async IAsyncEnumerable<TrackedUrl> StreamAsync(
        TrackedUrlExportQuery query,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        // Read from your own store here. Apply query.Search (a case-insensitive "contains" on the URL), query.Domain
        // and query.Culture, and order by query.SortColumn and query.Descending, ending in ascending Id so the order is
        // the same on every call. Yield each row as it is read.
        await Task.CompletedTask;
        cancellationToken.ThrowIfCancellationRequested();

        yield return new TrackedUrl
        {
            Id = 1,
            Url = "/old-page",
            DisplayUrl = "/Old-Page",
            RelativePath = "/old-page",
            HitCount = 3,
            FirstSeen = DateTime.UtcNow.AddDays(-2),
            LastSeen = DateTime.UtcNow,
        };
    }
}

// docs:end exporter-class
