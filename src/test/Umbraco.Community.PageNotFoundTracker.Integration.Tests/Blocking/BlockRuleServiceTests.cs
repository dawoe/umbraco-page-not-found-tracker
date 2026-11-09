// Copyright (c) Dave Woestenborghs and contributors. Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Umbraco.Cms.Core.Configuration.Models;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Services;
using Umbraco.Community.PageNotFoundTracker.Core.Blocking;
using Umbraco.Community.PageNotFoundTracker.Core.Configuration;
using Umbraco.Community.PageNotFoundTracker.Core.Tracking;
using Umbraco.Community.PageNotFoundTracker.Infrastructure.Blocking;
using Umbraco.Community.PageNotFoundTracker.Infrastructure.Tracking;
using Umbraco.Community.PageNotFoundTracker.Integration.Tests.Tracking;

namespace Umbraco.Community.PageNotFoundTracker.Integration.Tests.Blocking;

/// <summary>
/// Tests <see cref="BlockRuleService"/> against a real database (SQLite, and SQL Server when configured): the LIKE delete,
/// its parity with the in-memory matcher, the queue discard, the upsert and the rule list.
/// </summary>
internal sealed class BlockRuleServiceTests : TrackingDatabaseFixture
{
    private static readonly DateTime Start = new(2026, 11, 4, 8, 0, 0, DateTimeKind.Utc);

    private TrackingQueue queue = null!;
    private TrackingFlushService flushService = null!;
    private BlockRuleService service = null!;

    /// <summary>
    /// Initializes a new instance of the <see cref="BlockRuleServiceTests"/> class.
    /// </summary>
    /// <param name="database">The database to run on.</param>
    public BlockRuleServiceTests(TrackingDatabase database)
        : base(database)
    {
    }

    /// <summary>
    /// Empties both tables and builds the queue, the flush service and the rule service on the fixture's database.
    /// </summary>
    /// <returns>A task.</returns>
    [SetUp]
    public async Task SetUpAsync()
    {
        await using (PageNotFoundTrackerDbContext dbContext = this.CreateContext())
        {
            await dbContext.TrackedUrls.ExecuteDeleteAsync();
            await dbContext.BlockRules.ExecuteDeleteAsync();
        }

        IServiceProvider sp = new ServiceCollection()
            .AddSingleton<IDbContextFactory<PageNotFoundTrackerDbContext>>(new TestContextFactory(this))
            .BuildServiceProvider();
        var settings = Options.Create(new PageNotFoundTrackerSettings());
        this.queue = new TrackingQueue(settings, TimeProvider.System, NullLogger<TrackingQueue>.Instance);
        var state = new TrackingPersistenceState();
        state.SetAvailable();
        this.flushService = new TrackingFlushService(
            this.queue,
            state,
            new TrackingDatabaseProviderSelector([]),
            new StaticOptionsMonitor<ConnectionStrings>(new ConnectionStrings()),
            sp,
            new DefaultTrackedUrlUpsert(NullLogger<DefaultTrackedUrlUpsert>.Instance),
            settings,
            TimeProvider.System,
            NullLogger<TrackingFlushService>.Instance);

        var snapshotProvider = new Mock<IBlockSnapshotProvider>();
        snapshotProvider.Setup(p => p.RefreshAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);
        var notifier = new Mock<IBlockRuleChangeNotifier>();
        notifier.Setup(n => n.NotifyAsync(It.IsAny<BlockRuleCachePayload>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        this.service = new BlockRuleService(
            sp,
            this.queue,
            this.flushService,
            snapshotProvider.Object,
            notifier.Object,
            Mock.Of<IDomainService>(),
            Mock.Of<ILanguageService>(l => l.GetAllAsync() == Task.FromResult<IEnumerable<ILanguage>>(
                new[] { Mock.Of<ILanguage>(x => x.IsoCode == "en-US"), Mock.Of<ILanguage>(x => x.IsoCode == "nl-NL") })),
            TimeProvider.System,
            NullLogger<BlockRuleService>.Instance);
    }

    /// <summary>
    /// Disposes the flush service.
    /// </summary>
    [TearDown]
    public void TearDown() => this.flushService.Dispose();

    /// <summary>
    /// Creating a rule deletes the matching rows within its domain and culture, whatever their query string, and keeps
    /// rows outside the scope or the pattern.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task Create_deletes_matching_rows_in_scope_and_keeps_others()
    {
        await this.AddRowsAsync(
            Row("/wp-admin/a", "www.a.local", "en-US"),
            Row("/wp-admin/b", "www.a.local", "en-US", url: "/wp-admin/b?x=1"),
            Row("/wp-admin/b", "www.a.local", "en-US", url: "/wp-admin/b?x=2"),
            Row("/wp-admin", "www.a.local", "en-US"),
            Row("/wp-admin/a_b%c", "www.a.local", "en-US"),
            Row("/wp-adminx", "www.a.local", "en-US"),
            Row("/other", "www.a.local", "en-US"),
            Row("/wp-admin/a", "www.b.local", "en-US"),
            Row("/wp-admin/a", "www.a.local", "nl-NL"),
            Row("/wp-admin/a", string.Empty, string.Empty));

        CreateBlockRuleOutcome outcome = await this.service.CreateAsync("/wp-admin/*", 410, "WWW.A.local", "en-US", CancellationToken.None);

        List<TrackedUrl> remaining = await this.GetRowsAsync();
        Assert.Multiple(() =>
        {
            Assert.That(outcome, Is.EqualTo(new CreateBlockRuleOutcome(CreateBlockRuleStatus.Saved, null, true)));
            Assert.That(
                remaining.Select(r => (r.RelativePath, r.Domain, r.Culture)),
                Is.EquivalentTo(new[]
                {
                    ("/wp-adminx", "www.a.local", "en-US"),
                    ("/other", "www.a.local", "en-US"),
                    ("/wp-admin/a", "www.b.local", "en-US"),
                    ("/wp-admin/a", "www.a.local", "nl-NL"),
                    ("/wp-admin/a", string.Empty, string.Empty),
                }));
        });
    }

    /// <summary>
    /// A rule for all domains and cultures deletes every matching row; a culture given in another case still matches the
    /// row's culture (as the in-memory matcher does).
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task Create_for_all_scopes_deletes_everywhere_and_culture_ignores_case()
    {
        await this.AddRowsAsync(
            Row("/x", "www.a.local", "en-US"),
            Row("/x", "www.b.local", "nl-NL"),
            Row("/x", string.Empty, string.Empty),
            Row("/y", "www.a.local", "en-US"),
            Row("/y", "www.a.local", "nl-NL"));

        await this.service.CreateAsync("/x", 410, string.Empty, string.Empty, CancellationToken.None);
        await this.service.CreateAsync("/y", 410, string.Empty, "en-us", CancellationToken.None);

        List<TrackedUrl> remaining = await this.GetRowsAsync();
        Assert.That(remaining.Select(r => (r.RelativePath, r.Culture)), Is.EqualTo(new[] { ("/y", "nl-NL") }));
    }

    /// <summary>
    /// <c>_</c> and <c>%</c> in a pattern are literal characters in the delete, not SQL wildcards.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task Create_wildcard_with_underscore_does_not_over_delete()
    {
        await this.AddRowsAsync(Row("/a_b"), Row("/a_bc"), Row("/axb"), Row("/axbc"), Row("/50%off"), Row("/50xoff"), Row("/50off"));

        await this.service.CreateAsync("/a_b*", 410, string.Empty, string.Empty, CancellationToken.None);
        await this.service.CreateAsync("/50%25*", 410, string.Empty, string.Empty, CancellationToken.None);

        List<TrackedUrl> remaining = await this.GetRowsAsync();
        Assert.That(remaining.Select(r => r.RelativePath), Is.EquivalentTo(new[] { "/axb", "/axbc", "/50xoff", "/50off" }));
    }

    /// <summary>
    /// The SQL the delete runs and the in-memory matcher select the same rows for every pattern.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task Like_delete_and_matcher_select_the_same_rows()
    {
        string[] paths =
        [
            "/x", "/x/y", "/x/y/z", "/xy", "/xy/y", "/x/q/y", "/a_b", "/axb", "/a%b", "/a%%b", "/axxb", "/50%off", "/50off",
            "/index.php", "/x.php/y", "/abc", "/a/b/c", "/acb", "/[x]y", "/xy]", "/back\\slash", "/backslash", "/wp-admin",
            "/wp-admin/a", "/wp-adminx", "/",
        ];
        string[] patterns =
        [
            "/x/*", "/x", "/a_b*", "/a%b", "/50%*", "*.php", "/a*b*c", "/[x]*", "/back\\slash*", "/x*/y", "*_*", "/wp-admin/*",
            "/a*a", "*/y", "/x/*/z",
        ];
        await this.AddRowsAsync([.. paths.Select(p => Row(p))]);
        await using PageNotFoundTrackerDbContext dbContext = this.CreateContext();
        List<TrackedUrl> all = await dbContext.TrackedUrls.AsNoTracking().ToListAsync();

        foreach (string pattern in patterns)
        {
            var compiled = CompiledBlockPattern.Compile(pattern);
            string[] expected = [.. all.Where(r => compiled.IsMatch(r.RelativePath)).Select(r => r.RelativePath).Order(StringComparer.Ordinal)];
            List<string> actual = await BlockRuleService.WhereMatches(dbContext.TrackedUrls.AsNoTracking(), pattern, string.Empty, string.Empty)
                .Select(r => r.RelativePath)
                .ToListAsync();

            Assert.That(actual.Order(StringComparer.Ordinal), Is.EqualTo(expected), pattern);
        }
    }

    /// <summary>
    /// A hit still queued for a URL the new rule covers is discarded, so the next flush does not bring the row back.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task Create_discards_a_queued_hit_so_the_row_does_not_return()
    {
        TrackedUrl row = Row("/old");
        await this.AddRowsAsync(row);
        this.queue.TryEnqueue(Hit(row));

        await this.service.CreateAsync("/old", 410, string.Empty, string.Empty, CancellationToken.None);
        await this.flushService.FlushAsync(CancellationToken.None);

        Assert.That(await this.GetRowsAsync(), Is.Empty);
    }

    /// <summary>
    /// The same pattern in another spelling updates the status code of the one rule and keeps the first spelling.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task Same_pattern_other_case_updates_status_not_duplicate()
    {
        await this.service.CreateAsync("/Old-Page", 410, string.Empty, string.Empty, CancellationToken.None);
        CreateBlockRuleOutcome second = await this.service.CreateAsync("/old-page/", 403, string.Empty, string.Empty, CancellationToken.None);

        List<BlockRule> rules = await this.GetRulesAsync();
        Assert.Multiple(() =>
        {
            Assert.That(second.Status, Is.EqualTo(CreateBlockRuleStatus.Saved));
            Assert.That(rules, Has.Count.EqualTo(1));
            Assert.That(rules[0].StatusCode, Is.EqualTo(403));
            Assert.That(rules[0].DisplayPattern, Is.EqualTo("/Old-Page"));
            Assert.That(rules[0].Pattern, Is.EqualTo("/old-page"));
        });
    }

    /// <summary>
    /// The same culture in another case updates the status of the one rule, on every database.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task Same_culture_other_case_updates_one_rule()
    {
        await this.service.CreateAsync("/old", 410, string.Empty, "en-US", CancellationToken.None);
        CreateBlockRuleOutcome second = await this.service.CreateAsync("/old", 403, string.Empty, "en-us", CancellationToken.None);

        List<BlockRule> rules = await this.GetRulesAsync();
        Assert.Multiple(() =>
        {
            Assert.That(second.Status, Is.EqualTo(CreateBlockRuleStatus.Saved));
            Assert.That(rules, Has.Count.EqualTo(1));
            Assert.That(rules[0].StatusCode, Is.EqualTo(403));
            Assert.That(rules[0].Culture, Is.EqualTo("en-US"));
        });
    }

    /// <summary>
    /// After a rule is removed, the URL it covered is tracked again from a count of one.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task Delete_then_hit_is_tracked_again_from_one()
    {
        TrackedUrl row = Row("/old");
        row.HitCount = 5;
        await this.AddRowsAsync(row);
        await this.service.CreateAsync("/old", 410, string.Empty, string.Empty, CancellationToken.None);
        int id = (await this.GetRulesAsync()).Single().Id;

        bool deleted = await this.service.DeleteAsync(id, CancellationToken.None);
        bool deletedAgain = await this.service.DeleteAsync(id, CancellationToken.None);
        this.queue.TryEnqueue(Hit(Row("/old")));
        await this.flushService.FlushAsync(CancellationToken.None);

        List<TrackedUrl> rows = await this.GetRowsAsync();
        Assert.Multiple(async () =>
        {
            Assert.That(deleted, Is.True);
            Assert.That(deletedAgain, Is.False);
            Assert.That(await this.GetRulesAsync(), Is.Empty);
            Assert.That(rows.Select(r => (r.RelativePath, r.HitCount)), Is.EqualTo(new[] { ("/old", 1L) }));
        });
    }

    /// <summary>
    /// The rule list: default order newest first, every column in both directions with ties broken by ascending Id,
    /// search on the pattern with literal LIKE characters, the domain and culture filters, and paging with clamping.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task Query_filters_sorts_and_pages()
    {
        await this.AddRulesAsync(CreateSeedRules());
        List<BlockRule> all = await this.GetRulesAsync();

        BlockRulePage byDefault = await this.service.QueryAsync(Query(take: 100), CancellationToken.None);
        Assert.That(
            byDefault.Items.Select(r => r.Id),
            Is.EqualTo(all.OrderByDescending(r => r.Created).ThenBy(r => r.Id).Select(r => r.Id)),
            "default order");

        foreach (BlockRuleSortColumn column in Enum.GetValues<BlockRuleSortColumn>())
        {
            foreach (bool descending in new[] { false, true })
            {
                int[] expected = [.. SortExpected(all, column, descending).Select(r => r.Id)];
                BlockRulePage first = await this.service.QueryAsync(Query(skip: 0, take: 7, column: column, descending: descending), CancellationToken.None);
                BlockRulePage second = await this.service.QueryAsync(Query(skip: 7, take: 7, column: column, descending: descending), CancellationToken.None);
                int[] actual = [.. first.Items.Concat(second.Items).Select(r => r.Id)];

                Assert.That(actual, Is.EqualTo(expected), $"{column} {(descending ? "desc" : "asc")}");
                Assert.That(first.Total, Is.EqualTo(all.Count));
            }
        }

        BlockRulePage search = await this.service.QueryAsync(Query(take: 100, search: "WP-ADMIN"), CancellationToken.None);
        BlockRulePage literalUnderscore = await this.service.QueryAsync(Query(take: 100, search: "_"), CancellationToken.None);
        BlockRulePage domain = await this.service.QueryAsync(Query(take: 100, domain: TrackedUrlFilter.Exactly("www.a.local")), CancellationToken.None);
        BlockRulePage noDomain = await this.service.QueryAsync(Query(take: 100, domain: TrackedUrlFilter.Empty), CancellationToken.None);
        BlockRulePage culture = await this.service.QueryAsync(Query(take: 100, culture: TrackedUrlFilter.Exactly("nl-NL")), CancellationToken.None);
        BlockRulePage noCulture = await this.service.QueryAsync(Query(take: 100, culture: TrackedUrlFilter.Empty), CancellationToken.None);
        BlockRulePage zeroTake = await this.service.QueryAsync(Query(take: 0), CancellationToken.None);
        BlockRulePage hugeTake = await this.service.QueryAsync(Query(skip: -3, take: 10000), CancellationToken.None);
        BlockRulePage pastEnd = await this.service.QueryAsync(Query(skip: 500), CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(search.Items.Select(r => r.Id), Is.EquivalentTo(all.Where(r => r.Pattern.Contains("wp-admin")).Select(r => r.Id)));
            Assert.That(search.Total, Is.GreaterThan(0));
            Assert.That(literalUnderscore.Items.Select(r => r.Pattern), Is.EqualTo(new[] { "/a_b" }));
            Assert.That(domain.Items.Select(r => r.Id), Is.EquivalentTo(all.Where(r => r.Domain == "www.a.local").Select(r => r.Id)));
            Assert.That(noDomain.Items.Select(r => r.Id), Is.EquivalentTo(all.Where(r => r.Domain.Length == 0).Select(r => r.Id)));
            Assert.That(culture.Items.Select(r => r.Id), Is.EquivalentTo(all.Where(r => r.Culture == "nl-NL").Select(r => r.Id)));
            Assert.That(noCulture.Items.Select(r => r.Id), Is.EquivalentTo(all.Where(r => r.Culture.Length == 0).Select(r => r.Id)));
            Assert.That(domain.Total, Is.GreaterThan(0));
            Assert.That(culture.Total, Is.GreaterThan(0));
            Assert.That(zeroTake.Items, Has.Count.EqualTo(1));
            Assert.That(hugeTake.Items, Has.Count.EqualTo(all.Count));
            Assert.That(pastEnd.Items, Is.Empty);
            Assert.That(pastEnd.Total, Is.EqualTo(all.Count));
        });
    }

    /// <summary>
    /// Editing a rule changes it in place (same id and created time, new spelling and status) and deletes the tracked rows
    /// the new pattern covers.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task Update_changes_the_rule_and_deletes_the_rows_the_new_pattern_covers()
    {
        await this.AddRowsAsync(Row("/old-a"), Row("/new-a"), Row("/new-b/x"), Row("/other"));
        await this.service.CreateAsync("/old-a", 410, string.Empty, string.Empty, CancellationToken.None);
        BlockRule before = (await this.GetRulesAsync()).Single();

        CreateBlockRuleOutcome outcome = await this.service.UpdateAsync(before.Id, "/New-*", 403, string.Empty, string.Empty, CancellationToken.None);

        BlockRule after = (await this.GetRulesAsync()).Single();
        Assert.Multiple(async () =>
        {
            Assert.That(outcome, Is.EqualTo(new CreateBlockRuleOutcome(CreateBlockRuleStatus.Saved, null, true)));
            Assert.That(after.Id, Is.EqualTo(before.Id));
            Assert.That(after.Created, Is.EqualTo(before.Created));
            Assert.That((after.Pattern, after.DisplayPattern, after.StatusCode), Is.EqualTo(("/new-*", "/New-*", 403)));
            Assert.That((await this.GetRowsAsync()).Select(r => r.RelativePath), Is.EqualTo(new[] { "/other" }));
        });
    }

    /// <summary>
    /// Editing a rule into the identity of another rule is refused and changes neither rule.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task Update_into_another_rules_identity_is_refused_and_changes_nothing()
    {
        await this.service.CreateAsync("/a", 410, string.Empty, string.Empty, CancellationToken.None);
        await this.service.CreateAsync("/b", 404, string.Empty, string.Empty, CancellationToken.None);
        int bId = (await this.GetRulesAsync()).Single(r => r.Pattern == "/b").Id;

        CreateBlockRuleOutcome outcome = await this.service.UpdateAsync(bId, "/A/", 403, string.Empty, string.Empty, CancellationToken.None);

        List<BlockRule> rules = await this.GetRulesAsync();
        Assert.Multiple(() =>
        {
            Assert.That(outcome.Status, Is.EqualTo(CreateBlockRuleStatus.Conflict));
            Assert.That(outcome.Message, Is.EqualTo(BlockingConstants.RuleExistsMessage));
            Assert.That(rules.Select(r => (r.Pattern, r.StatusCode)), Is.EqualTo(new[] { ("/a", 410), ("/b", 404) }));
        });
    }

    /// <summary>
    /// After a rule is edited to another pattern, the old path is tracked again from a count of one.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task Update_then_old_path_is_tracked_again_from_one()
    {
        TrackedUrl row = Row("/old");
        row.HitCount = 5;
        await this.AddRowsAsync(row);
        await this.service.CreateAsync("/old", 410, string.Empty, string.Empty, CancellationToken.None);
        int id = (await this.GetRulesAsync()).Single().Id;

        await this.service.UpdateAsync(id, "/new", 410, string.Empty, string.Empty, CancellationToken.None);
        this.queue.TryEnqueue(Hit(Row("/old")));
        await this.flushService.FlushAsync(CancellationToken.None);

        List<TrackedUrl> rows = await this.GetRowsAsync();
        Assert.That(rows.Select(r => (r.RelativePath, r.HitCount)), Is.EqualTo(new[] { ("/old", 1L) }));
    }

    private static BlockRuleQuery Query(
        int skip = 0,
        int take = 20,
        BlockRuleSortColumn column = BlockRuleSortColumn.Created,
        bool descending = true,
        string? search = null,
        TrackedUrlFilter domain = default,
        TrackedUrlFilter culture = default) =>
        new(skip, take, column, descending, search, domain, culture);

    private static IEnumerable<BlockRule> SortExpected(List<BlockRule> all, BlockRuleSortColumn column, bool descending)
    {
        // Seeded patterns, domains and cultures compare the same ordinal and under both databases' collations.
        Func<BlockRule, object> key = column switch
        {
            BlockRuleSortColumn.Pattern => r => r.Pattern,
            BlockRuleSortColumn.StatusCode => r => r.StatusCode,
            BlockRuleSortColumn.Domain => r => r.Domain,
            BlockRuleSortColumn.Culture => r => r.Culture.ToLowerInvariant(),
            BlockRuleSortColumn.Created => r => r.Created,
            _ => throw new ArgumentOutOfRangeException(nameof(column)),
        };

        IComparer<object> comparer = Comparer<object>.Create((a, b) => a is string x && b is string y
            ? string.CompareOrdinal(x, y)
            : Comparer<object>.Default.Compare(a, b));
        return descending
            ? all.OrderByDescending(key, comparer).ThenBy(r => r.Id)
            : all.OrderBy(key, comparer).ThenBy(r => r.Id);
    }

    private static List<BlockRule> CreateSeedRules()
    {
        string[] domains = [string.Empty, "www.a.local", "www.b.local"];
        string[] cultures = [string.Empty, "en-US", "nl-NL"];
        int[] statuses = [403, 404, 410];
        var rules = new List<BlockRule>();
        for (int i = 0; i < 13; i++)
        {
            // Status codes, domains, cultures and created times repeat on purpose, so every sort column has ties.
            string pattern = i == 12 ? "/a_b" : (i % 3 == 0 ? $"/wp-admin/p{i:00}*" : $"/page-{i:00}");
            rules.Add(new BlockRule
            {
                Pattern = pattern,
                PatternHash = TrackedUrlNormalizer.Hash(pattern),
                DisplayPattern = pattern,
                StatusCode = statuses[i % 3],
                Domain = domains[(i / 2) % 3],
                Culture = cultures[(i / 3) % 3],
                Created = Start.AddMinutes(i % 5),
            });
        }

        return rules;
    }

    private static TrackedUrl Row(string relativePath, string domain = "", string culture = "", string? url = null)
    {
        string fullUrl = url ?? relativePath;
        return new TrackedUrl
        {
            Url = fullUrl,
            UrlHash = TrackedUrlNormalizer.Hash(fullUrl + "|" + domain + "|" + culture),
            RelativePath = relativePath,
            DisplayUrl = fullUrl,
            Domain = domain,
            Culture = culture,
            HitCount = 1,
            FirstSeen = Start,
            LastSeen = Start,
        };
    }

    private static TrackedUrlHit Hit(TrackedUrl row) =>
        new(row.Url, row.UrlHash, row.RelativePath, row.DisplayUrl, row.Domain, row.Culture, false, 1, Start, Start);

    private async Task AddRowsAsync(params TrackedUrl[] rows)
    {
        await using PageNotFoundTrackerDbContext dbContext = this.CreateContext();
        dbContext.TrackedUrls.AddRange(rows);
        await dbContext.SaveChangesAsync();
    }

    private async Task AddRulesAsync(IEnumerable<BlockRule> rules)
    {
        await using PageNotFoundTrackerDbContext dbContext = this.CreateContext();
        dbContext.BlockRules.AddRange(rules);
        await dbContext.SaveChangesAsync();
    }

    private async Task<List<TrackedUrl>> GetRowsAsync()
    {
        await using PageNotFoundTrackerDbContext dbContext = this.CreateContext();
        return await dbContext.TrackedUrls.AsNoTracking().OrderBy(r => r.Id).ToListAsync();
    }

    private async Task<List<BlockRule>> GetRulesAsync()
    {
        await using PageNotFoundTrackerDbContext dbContext = this.CreateContext();
        return await dbContext.BlockRules.AsNoTracking().OrderBy(r => r.Id).ToListAsync();
    }

    private sealed class TestContextFactory(BlockRuleServiceTests owner) : IDbContextFactory<PageNotFoundTrackerDbContext>
    {
        public PageNotFoundTrackerDbContext CreateDbContext() => owner.CreateContext();
    }

    private sealed class StaticOptionsMonitor<T>(T value) : IOptionsMonitor<T>
    {
        public T CurrentValue => value;

        public T Get(string? name) => value;

        public IDisposable? OnChange(Action<T, string?> listener) => null;
    }
}
