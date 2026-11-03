// Copyright (c) Dave Woestenborghs and contributors. Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Umbraco.Community.PageNotFoundTracker.Core.Tracking;
using Umbraco.Community.PageNotFoundTracker.Infrastructure.Tracking;

namespace Umbraco.Community.PageNotFoundTracker.Integration.Tests.Tracking;

/// <summary>
/// Tests <see cref="TrackedUrlQueryService"/> against a real database, once on SQLite and once on SQL Server. Every
/// test starts from the same 25 seeded rows (the table is emptied and refilled), so the tests do not share state.
/// </summary>
internal sealed class TrackedUrlQueryServiceTests : TrackingDatabaseFixture
{
    private static readonly DateTime Start = new(2026, 11, 1, 8, 0, 0, DateTimeKind.Utc);

    private TrackedUrlQueryService service = null!;

    /// <summary>
    /// Initializes a new instance of the <see cref="TrackedUrlQueryServiceTests"/> class.
    /// </summary>
    /// <param name="database">The database to run on.</param>
    public TrackedUrlQueryServiceTests(TrackingDatabase database)
        : base(database)
    {
    }

    /// <summary>
    /// Seeds the table with 25 rows and creates the service.
    /// </summary>
    /// <returns>A task.</returns>
    [SetUp]
    public async Task SeedAsync()
    {
        await this.ReplaceRowsAsync(CreateSeedRows());
        this.service = new TrackedUrlQueryService(new TestContextFactory(this));
    }

    /// <summary>
    /// Tests that the default order is hits descending and ties go by ascending Id.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task GetPageAsync_Default_OrdersByHitsDescendingThenId()
    {
        TrackedUrlPage page = await this.service.GetPageAsync(Query(), CancellationToken.None);

        var expected = (await this.GetAllAsync()).OrderByDescending(r => r.HitCount).ThenBy(r => r.Id).Take(10).Select(r => r.Id);
        Assert.Multiple(() =>
        {
            Assert.That(page.Total, Is.EqualTo(25));
            Assert.That(page.Items.Select(r => r.Id), Is.EqualTo(expected));
        });
    }

    /// <summary>
    /// Tests every sort column in both directions: the order matches, ties are broken by ascending Id, and a second
    /// page neither repeats nor skips rows.
    /// </summary>
    /// <param name="column">The sort column.</param>
    /// <param name="descending">Whether to sort descending.</param>
    /// <returns>A task.</returns>
    [Test]
    public async Task GetPageAsync_EverySortColumn_BothDirections_OrderedWithStableTies(
        [Values] TrackedUrlSortColumn column,
        [Values] bool descending)
    {
        List<TrackedUrl> all = await this.GetAllAsync();
        IEnumerable<TrackedUrl> expected = SortExpected(all, column, descending);
        int[] expectedIds = [.. expected.Select(r => r.Id)];

        TrackedUrlPage first = await this.service.GetPageAsync(Query(skip: 0, take: 10, column: column, descending: descending), CancellationToken.None);
        TrackedUrlPage second = await this.service.GetPageAsync(Query(skip: 10, take: 10, column: column, descending: descending), CancellationToken.None);
        TrackedUrlPage third = await this.service.GetPageAsync(Query(skip: 20, take: 10, column: column, descending: descending), CancellationToken.None);

        int[] actual = [.. first.Items.Concat(second.Items).Concat(third.Items).Select(r => r.Id)];
        Assert.Multiple(() =>
        {
            Assert.That(actual, Is.EqualTo(expectedIds));
            Assert.That(actual, Is.Unique);
            Assert.That(first.Total, Is.EqualTo(25));
        });
    }

    /// <summary>
    /// Tests that the search is a case-insensitive "contains" on the URL.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task GetPageAsync_SearchIsCaseInsensitiveContains()
    {
        TrackedUrlPage page = await this.service.GetPageAsync(Query(search: "ABOUT-"), CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(page.Total, Is.EqualTo(5));
            Assert.That(page.Items.Select(r => r.Url), Is.All.Contains("about-"));
        });
    }

    /// <summary>
    /// Tests that percent, underscore and bracket in the search are literal characters, not wildcards.
    /// </summary>
    /// <param name="term">The search term.</param>
    /// <param name="expectedUrl">The only URL that matches.</param>
    /// <returns>A task.</returns>
    [TestCase("%", "/a%b")]
    [TestCase("_", "/a_b")]
    [TestCase("[", "/a[b")]
    public async Task GetPageAsync_SearchTreatsPercentUnderscoreBracketLiterally(string term, string expectedUrl)
    {
        await this.ReplaceRowsAsync([Row("/a%b"), Row("/a_b"), Row("/a[b"), Row("/axb")]);

        TrackedUrlPage page = await this.service.GetPageAsync(Query(search: term), CancellationToken.None);

        Assert.That(page.Items.Select(r => r.Url), Is.EqualTo(new[] { expectedUrl }));
        Assert.That(page.Total, Is.EqualTo(1));
    }

    /// <summary>
    /// Tests the domain filter: a value, the empty marker, and any.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task GetPageAsync_DomainFilter_Value_Empty_Any()
    {
        List<TrackedUrl> all = await this.GetAllAsync();

        TrackedUrlPage value = await this.service.GetPageAsync(Query(take: 100, domain: TrackedUrlFilter.Exactly("www.sitea.local")), CancellationToken.None);
        TrackedUrlPage empty = await this.service.GetPageAsync(Query(take: 100, domain: TrackedUrlFilter.Empty), CancellationToken.None);
        TrackedUrlPage any = await this.service.GetPageAsync(Query(take: 100, domain: TrackedUrlFilter.Any), CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(value.Items.Select(r => r.Id), Is.EquivalentTo(all.Where(r => r.Domain == "www.sitea.local").Select(r => r.Id)));
            Assert.That(value.Total, Is.GreaterThan(0));
            Assert.That(empty.Items.Select(r => r.Id), Is.EquivalentTo(all.Where(r => r.Domain.Length == 0).Select(r => r.Id)));
            Assert.That(empty.Total, Is.GreaterThan(0));
            Assert.That(any.Total, Is.EqualTo(25));
        });
    }

    /// <summary>
    /// Tests the culture filter: a value, the empty marker, and any.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task GetPageAsync_CultureFilter_Value_Empty_Any()
    {
        List<TrackedUrl> all = await this.GetAllAsync();

        TrackedUrlPage value = await this.service.GetPageAsync(Query(take: 100, culture: TrackedUrlFilter.Exactly("nl-NL")), CancellationToken.None);
        TrackedUrlPage empty = await this.service.GetPageAsync(Query(take: 100, culture: TrackedUrlFilter.Empty), CancellationToken.None);
        TrackedUrlPage any = await this.service.GetPageAsync(Query(take: 100, culture: TrackedUrlFilter.Any), CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(value.Items.Select(r => r.Id), Is.EquivalentTo(all.Where(r => r.Culture == "nl-NL").Select(r => r.Id)));
            Assert.That(value.Total, Is.GreaterThan(0));
            Assert.That(empty.Items.Select(r => r.Id), Is.EquivalentTo(all.Where(r => r.Culture.Length == 0).Select(r => r.Id)));
            Assert.That(empty.Total, Is.GreaterThan(0));
            Assert.That(any.Total, Is.EqualTo(25));
        });
    }

    /// <summary>
    /// Tests that a domain whose name looks like an empty marker is filtered as a value, not as empty.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task GetPageAsync_DomainNamedLikeTheEmptyMarker_FiltersByValueNotEmpty()
    {
        TrackedUrl odd = Row("/odd", domain: "(empty)");
        TrackedUrl plain = Row("/plain");
        await this.ReplaceRowsAsync([odd, plain]);

        TrackedUrlPage byValue = await this.service.GetPageAsync(Query(domain: TrackedUrlFilter.Exactly("(empty)")), CancellationToken.None);
        TrackedUrlPage empty = await this.service.GetPageAsync(Query(domain: TrackedUrlFilter.Empty), CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(byValue.Items.Select(r => r.Url), Is.EqualTo(new[] { "/odd" }));
            Assert.That(empty.Items.Select(r => r.Url), Is.EqualTo(new[] { "/plain" }));
        });
    }

    /// <summary>
    /// Tests that search, domain and culture filters combine with AND.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task GetPageAsync_FiltersAndSearchCombine()
    {
        List<TrackedUrl> all = await this.GetAllAsync();
        int[] expected = [.. all.Where(r => r.Url.Contains("about-") && r.Domain == "www.sitea.local" && r.Culture == "nl-NL").Select(r => r.Id)];

        TrackedUrlPage page = await this.service.GetPageAsync(
            Query(take: 100, search: "About-", domain: TrackedUrlFilter.Exactly("www.sitea.local"), culture: TrackedUrlFilter.Exactly("nl-NL")),
            CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(expected, Is.Not.Empty, "The seed must contain a combined match.");
            Assert.That(page.Items.Select(r => r.Id), Is.EquivalentTo(expected));
            Assert.That(page.Total, Is.EqualTo(expected.Length));
        });
    }

    /// <summary>
    /// Tests that a page past the end has no items and still reports the real total.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task GetPageAsync_PagePastTheEnd_ReturnsNoItemsAndTrueTotal()
    {
        TrackedUrlPage page = await this.service.GetPageAsync(Query(skip: 500, take: 20), CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(page.Items, Is.Empty);
            Assert.That(page.Total, Is.EqualTo(25));
        });
    }

    /// <summary>
    /// Tests that a negative skip counts as 0 and the take is clamped to 1 to 100.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task GetPageAsync_TakeAndSkipOutOfRange_AreClamped()
    {
        TrackedUrlPage negativeSkip = await this.service.GetPageAsync(Query(skip: -5, take: 3), CancellationToken.None);
        TrackedUrlPage zeroTake = await this.service.GetPageAsync(Query(take: 0), CancellationToken.None);
        TrackedUrlPage hugeTake = await this.service.GetPageAsync(Query(take: 100000), CancellationToken.None);

        TrackedUrlPage firstThree = await this.service.GetPageAsync(Query(skip: 0, take: 3), CancellationToken.None);
        Assert.Multiple(() =>
        {
            Assert.That(negativeSkip.Items.Select(r => r.Id), Is.EqualTo(firstThree.Items.Select(r => r.Id)));
            Assert.That(zeroTake.Items, Has.Count.EqualTo(1));
            Assert.That(hugeTake.Items, Has.Count.EqualTo(25));
        });
    }

    /// <summary>
    /// Tests that the filter options list distinct non-empty values, sorted ordinal, and flag empty values.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task GetFilterOptionsAsync_ReturnsDistinctValuesAndEmptyFlags()
    {
        TrackedUrlFilterOptions options = await this.service.GetFilterOptionsAsync(CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(options.Domains, Is.EqualTo(new[] { "www.sitea.local", "www.siteb.local" }));
            Assert.That(options.Cultures, Is.EqualTo(new[] { "en-US", "nl-NL" }));
            Assert.That(options.HasEmptyDomain, Is.True);
            Assert.That(options.HasEmptyCulture, Is.True);
        });

        await this.ReplaceRowsAsync([Row("/x", domain: "d", culture: "en-US")]);
        TrackedUrlFilterOptions none = await this.service.GetFilterOptionsAsync(CancellationToken.None);
        Assert.Multiple(() =>
        {
            Assert.That(none.HasEmptyDomain, Is.False);
            Assert.That(none.HasEmptyCulture, Is.False);
        });
    }

    /// <summary>
    /// Tests that an empty table gives empty lists and false flags.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task GetFilterOptionsAsync_NoRows_ReturnsEmptyLists()
    {
        await this.ReplaceRowsAsync([]);

        TrackedUrlFilterOptions options = await this.service.GetFilterOptionsAsync(CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(options.Domains, Is.Empty);
            Assert.That(options.Cultures, Is.Empty);
            Assert.That(options.HasEmptyDomain, Is.False);
            Assert.That(options.HasEmptyCulture, Is.False);
        });
    }

    private static TrackedUrlQuery Query(
        int skip = 0,
        int take = 10,
        TrackedUrlSortColumn column = TrackedUrlSortColumn.Hits,
        bool descending = true,
        string? search = null,
        TrackedUrlFilter domain = default,
        TrackedUrlFilter culture = default) =>
        new(skip, take, column, descending, search, domain, culture);

    private static IEnumerable<TrackedUrl> SortExpected(List<TrackedUrl> all, TrackedUrlSortColumn column, bool descending)
    {
        // Seeded values are lower-case ASCII (domains and cultures excepted: cultures are compared ordinal-ignore-case,
        // which equals the database order for the seeded values), so SQLite and SQL Server collations agree.
        Func<TrackedUrl, object> key = column switch
        {
            TrackedUrlSortColumn.Hits => r => r.HitCount,
            TrackedUrlSortColumn.Url => r => r.Url,
            TrackedUrlSortColumn.Domain => r => r.Domain,
            TrackedUrlSortColumn.Culture => r => r.Culture.ToLowerInvariant(),
            TrackedUrlSortColumn.IsFile => r => r.IsFile,
            TrackedUrlSortColumn.FirstSeen => r => r.FirstSeen,
            TrackedUrlSortColumn.LastSeen => r => r.LastSeen,
            _ => throw new ArgumentOutOfRangeException(nameof(column)),
        };

        IComparer<object> comparer = Comparer<object>.Create((a, b) => a is string x && b is string y
            ? string.CompareOrdinal(x, y)
            : Comparer<object>.Default.Compare(a, b));
        return descending
            ? all.OrderByDescending(key, comparer).ThenBy(r => r.Id)
            : all.OrderBy(key, comparer).ThenBy(r => r.Id);
    }

    private static TrackedUrl Row(string url, string domain = "", string culture = "", long hits = 1, bool isFile = false, int minute = 0) =>
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

    private static List<TrackedUrl> CreateSeedRows()
    {
        string[] domains = [string.Empty, "www.sitea.local", "www.siteb.local"];
        string[] cultures = [string.Empty, "en-US", "nl-NL"];
        var rows = new List<TrackedUrl>();
        for (int i = 0; i < 25; i++)
        {
            // Hit counts, first seen and last seen repeat on purpose, so every sort column has ties.
            string name = i % 5 == 2 ? $"/about-{i}" : $"/page-{i}";
            rows.Add(Row(
                name,
                domains[i % 3],
                cultures[(i / 3) % 3],
                hits: (i % 4) + 1,
                isFile: i % 2 == 0,
                minute: i % 6));
        }

        return rows;
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

    private sealed class TestContextFactory(TrackedUrlQueryServiceTests owner) : IDbContextFactory<PageNotFoundTrackerDbContext>
    {
        public PageNotFoundTrackerDbContext CreateDbContext() => owner.CreateContext();
    }
}
