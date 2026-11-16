// Copyright (c) Dave Woestenborghs and contributors. Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Runtime.CompilerServices;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Umbraco.Community.PageNotFoundTracker.Core.Tracking;

namespace Umbraco.Community.PageNotFoundTracker.Infrastructure.Tracking;

/// <summary>
/// The default <see cref="ITrackedUrlExporter"/>: one database command over the same filter and order as the paged
/// list, read row by row. Nothing is buffered, so the memory use does not grow with the table.
/// </summary>
/// <remarks>
/// The context factory is resolved lazily, not injected, for the reason given on <see cref="TrackedUrlQueryService"/>.
/// </remarks>
/// <param name="serviceProvider">The service provider; the context factory is resolved from it at call time only.</param>
internal sealed class TrackedUrlExporter(IServiceProvider serviceProvider) : ITrackedUrlExporter
{
    /// <inheritdoc />
    public async IAsyncEnumerable<TrackedUrl> StreamAsync(TrackedUrlExportQuery query, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        // The context stays open for the whole enumeration: the rows are read from one open data reader.
        await using PageNotFoundTrackerDbContext dbContext = await serviceProvider
            .GetRequiredService<IDbContextFactory<PageNotFoundTrackerDbContext>>()
            .CreateDbContextAsync(cancellationToken);

        IQueryable<TrackedUrl> rows = TrackedUrlQueryComposition.Order(
            TrackedUrlQueryComposition.Filter(dbContext.TrackedUrls.AsNoTracking(), query.Search, query.Domain, query.Culture),
            query.SortColumn,
            query.Descending);

        await foreach (TrackedUrl row in rows.AsAsyncEnumerable().WithCancellation(cancellationToken))
        {
            yield return row;
        }
    }
}
