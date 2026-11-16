// Copyright (c) Dave Woestenborghs and contributors. Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Umbraco.Community.PageNotFoundTracker.Core.Tracking;
using Umbraco.Community.PageNotFoundTracker.Infrastructure.Tracking;

namespace Umbraco.Community.PageNotFoundTracker.Integration.Tests.Tracking;

/// <summary>
/// Tests <see cref="TrackedUrlExporter"/> against a real database, once on SQLite and once on SQL Server. The streamed
/// rows must be exactly the rows <see cref="TrackedUrlQueryService.GetPageAsync"/> returns across all pages.
/// </summary>
internal sealed class TrackedUrlExporterTests : TrackingDatabaseFixture
{
    private static readonly DateTime Start = new(2026, 11, 1, 8, 0, 0, DateTimeKind.Utc);

    private TrackedUrlExporter exporter = null!;
    private TrackedUrlQueryService queryService = null!;

    /// <summary>
    /// Initializes a new instance of the <see cref="TrackedUrlExporterTests"/> class.
    /// </summary>
    /// <param name="database">The database to run on.</param>
    public TrackedUrlExporterTests(TrackingDatabase database)
        : base(database)
    {
    }

    /// <summary>
    /// Seeds the table with 25 rows and creates the services.
    /// </summary>
    /// <returns>A task.</returns>
    [SetUp]
    public async Task SeedAsync()
    {
        await this.ReplaceRowsAsync(CreateSeedRows(25));
        ServiceProvider provider = new ServiceCollection()
            .AddSingleton<IDbContextFactory<PageNotFoundTrackerDbContext>>(new TestContextFactory(this))
            .BuildServiceProvider();
        this.exporter = new TrackedUrlExporter(provider);
        this.queryService = new TrackedUrlQueryService(provider);
    }

    /// <summary>
    /// Tests every sort column in both directions: the streamed ids equal the ids of all pages of the paged query.
    /// </summary>
    /// <param name="column">The sort column.</param>
    /// <param name="descending">Whether to sort descending.</param>
    /// <returns>A task.</returns>
    [Test]
    public async Task StreamAsync_EverySortColumn_BothDirections_MatchesAllPagesOfGetPage(
        [Values] TrackedUrlSortColumn column,
        [Values] bool descending)
    {
        int[] expected = await this.PagedIdsAsync(new TrackedUrlExportQuery(column, descending, null, default, default));

        int[] actual = await this.StreamedIdsAsync(new TrackedUrlExportQuery(column, descending, null, default, default));

        Assert.That(actual, Is.EqualTo(expected));
        Assert.That(actual, Has.Length.EqualTo(25));
    }

    /// <summary>
    /// Tests that the search, the domain filter and the culture filter select the same rows as the paged query.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task StreamAsync_SearchAndFilters_MatchGetPage()
    {
        var query = new TrackedUrlExportQuery(
            TrackedUrlSortColumn.Url,
            false,
            "about",
            TrackedUrlFilter.Exactly("www.sitea.local"),
            TrackedUrlFilter.Empty);

        int[] expected = await this.PagedIdsAsync(query);
        int[] actual = await this.StreamedIdsAsync(query);

        Assert.That(actual, Is.EqualTo(expected));

        var emptyDomain = new TrackedUrlExportQuery(TrackedUrlSortColumn.Hits, true, null, TrackedUrlFilter.Empty, TrackedUrlFilter.Exactly("en-US"));
        Assert.That(await this.StreamedIdsAsync(emptyDomain), Is.EqualTo(await this.PagedIdsAsync(emptyDomain)));
    }

    /// <summary>
    /// Tests that more rows than any page size or internal batch are all returned, in the requested order.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task StreamAsync_MoreRowsThanOneBatch_ReturnsEveryRow()
    {
        await this.ReplaceRowsAsync(CreateSeedRows(3000));
        var query = new TrackedUrlExportQuery(TrackedUrlSortColumn.FirstSeen, false, null, default, default);

        int[] actual = await this.StreamedIdsAsync(query);

        List<TrackedUrl> all = await this.GetAllAsync();
        Assert.That(actual, Is.EqualTo(all.OrderBy(r => r.FirstSeen).ThenBy(r => r.Id).Select(r => r.Id)));
        Assert.That(actual, Has.Length.EqualTo(3000));
    }

    /// <summary>
    /// Tests that cancelling while streaming stops the read with an <see cref="OperationCanceledException"/>.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task StreamAsync_Cancelled_StopsWithOperationCanceled()
    {
        await this.ReplaceRowsAsync(CreateSeedRows(3000));
        using var cts = new CancellationTokenSource();
        var query = new TrackedUrlExportQuery(TrackedUrlSortColumn.Hits, true, null, default, default);

        // CatchAsync: the database provider may throw the derived TaskCanceledException.
        Assert.CatchAsync<OperationCanceledException>(async () =>
        {
            int count = 0;
            await foreach (TrackedUrl row in this.exporter.StreamAsync(query, cts.Token))
            {
                if (++count == 1)
                {
                    await cts.CancelAsync();
                }
            }
        });
    }

    /// <summary>
    /// Tests that an empty table streams nothing.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task StreamAsync_NoRows_ReturnsEmpty()
    {
        await this.ReplaceRowsAsync([]);

        int[] actual = await this.StreamedIdsAsync(new TrackedUrlExportQuery(TrackedUrlSortColumn.Hits, true, null, default, default));

        Assert.That(actual, Is.Empty);
    }

    private static TrackedUrl Row(string url, string domain, string culture, long hits, bool isFile, int minute) =>
        new()
        {
            Url = url,
            UrlHash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(url + "|" + domain + "|" + culture))),
            RelativePath = url,
            DisplayUrl = url,
            Domain = domain,
            Culture = culture,
            IsFile = isFile,
            HitCount = hits,
            FirstSeen = Start.AddMinutes(minute),
            LastSeen = Start.AddMinutes(minute + 5),
        };

    private static List<TrackedUrl> CreateSeedRows(int count)
    {
        string[] domains = [string.Empty, "www.sitea.local", "www.siteb.local"];
        string[] cultures = [string.Empty, "en-US", "nl-NL"];
        var rows = new List<TrackedUrl>();
        for (int i = 0; i < count; i++)
        {
            // Hit counts, first seen and last seen repeat on purpose, so every sort column has ties.
            string name = i % 5 == 2 ? $"/about-{i}" : $"/page-{i}";
            rows.Add(Row(name, domains[i % 3], cultures[(i / 3) % 3], (i % 4) + 1, i % 2 == 0, i % 6));
        }

        return rows;
    }

    private async Task<int[]> PagedIdsAsync(TrackedUrlExportQuery query)
    {
        var ids = new List<int>();
        for (int skip = 0; ; skip += 10)
        {
            TrackedUrlPage page = await this.queryService.GetPageAsync(
                new TrackedUrlQuery(skip, 10, query.SortColumn, query.Descending, query.Search, query.Domain, query.Culture),
                CancellationToken.None);
            ids.AddRange(page.Items.Select(r => r.Id));
            if (skip + 10 >= page.Total)
            {
                return [.. ids];
            }
        }
    }

    private async Task<int[]> StreamedIdsAsync(TrackedUrlExportQuery query)
    {
        var ids = new List<int>();
        await foreach (TrackedUrl row in this.exporter.StreamAsync(query, CancellationToken.None))
        {
            ids.Add(row.Id);
        }

        return [.. ids];
    }

    private async Task ReplaceRowsAsync(IEnumerable<TrackedUrl> rows)
    {
        await using PageNotFoundTrackerDbContext dbContext = this.CreateContext();
        await dbContext.TrackedUrls.ExecuteDeleteAsync();
        dbContext.TrackedUrls.AddRange(rows);
        await dbContext.SaveChangesAsync();
    }

    private async Task<List<TrackedUrl>> GetAllAsync()
    {
        await using PageNotFoundTrackerDbContext dbContext = this.CreateContext();
        return await dbContext.TrackedUrls.AsNoTracking().OrderBy(r => r.Id).ToListAsync();
    }

    private sealed class TestContextFactory(TrackedUrlExporterTests owner) : IDbContextFactory<PageNotFoundTrackerDbContext>
    {
        public PageNotFoundTrackerDbContext CreateDbContext() => owner.CreateContext();
    }
}
