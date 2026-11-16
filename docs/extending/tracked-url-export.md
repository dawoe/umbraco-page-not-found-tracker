# Tracked URL export

The **Export** button on the *Page not found* dashboard downloads every tracked URL that the current search, filters and
sort match as a CSV file (see [The dashboards](../dashboards.md#export)). The rows come from an **exporter**. The
default one reads the package's own table. Replace it when the tracked URLs live somewhere else, for example in a
reporting database.

The CSV itself (columns, separator, encoding) is not an extension point: it is fixed so that the file opens in Excel.

## The contract

`ITrackedUrlExporter` (namespace `Umbraco.Community.PageNotFoundTracker.Core.Tracking`):

| Member | Meaning |
|---|---|
| `IAsyncEnumerable<TrackedUrl> StreamAsync(TrackedUrlExportQuery query, CancellationToken cancellationToken)` | Return every tracked URL that matches the query, in order |

`TrackedUrlExportQuery` has `SortColumn`, `Descending`, `Search`, `Domain` and `Culture`. It is the list query without
paging.

- **Search** is a case-insensitive "contains" on the URL; a blank search means none. A non-blank search is not trimmed.
- **Domain** and **Culture** are a `TrackedUrlFilter`: `Any`, `Empty` (rows without a value) or `Exactly(value)`.
- **Order** follows `SortColumn` and `Descending` and must end in ascending `Id`, so two exports of the same data give
  the same file and a row is never repeated or skipped.

## Rules

- **Read lazily.** The endpoint writes each row to the response as it arrives and flushes in chunks. An exporter that
  collects the rows first (`ToList()`) loads the whole table into memory, which is what the streaming is there to avoid.
- **Honour the cancellation token.** It is cancelled when the editor closes the browser tab. Anything an exporter
  throws after that is treated as part of the cancellation and not logged as a failure.
- **Throwing ends the export.** The status and the first bytes of the file have been sent by then, so the connection
  is aborted, the error is logged, and the dashboard shows that the export failed. Nothing is saved as a short file.
- The export has no row limit. Excel stops at 1,048,576 rows.

## Example

A minimal exporter that yields one row. A real one reads from its own store and applies the query:

<!-- sample: src/samples/Umbraco.Community.PageNotFoundTracker.Samples/TrackedUrlExporterSample.cs#exporter-class -->
```csharp
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
```

Register it from a composer marked `[ComposeAfter(typeof(PageNotFoundTrackerComposer))]`, with `AddUnique` (see
[the registration rule](README.md#the-registration-rule)):

<!-- sample: src/samples/Umbraco.Community.PageNotFoundTracker.Samples/TrackedUrlExporterSampleComposer.cs#exporter-composer -->
```csharp
/// <summary>
/// Registers the sample exporter after the package, so it replaces the default.
/// </summary>
[ComposeAfter(typeof(PageNotFoundTrackerComposer))]
public sealed class TrackedUrlExporterSampleComposer : IComposer
{
    /// <inheritdoc />
    public void Compose(IUmbracoBuilder builder) =>
        builder.Services.AddUnique<ITrackedUrlExporter, TrackedUrlExporterSample>();
}
```

The dashboard list is not affected: it is read through `ITrackedUrlQueryService`, so a site that replaces only the
exporter still lists the package's own rows. Replace both when the data is elsewhere.
