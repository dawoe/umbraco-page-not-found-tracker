// Copyright (c) Dave Woestenborghs and contributors. Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Globalization;
using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Services;
using Umbraco.Community.PageNotFoundTracker.Core.Blocking;
using Umbraco.Community.PageNotFoundTracker.Core.Tracking;
using Umbraco.Community.PageNotFoundTracker.Infrastructure.Tracking;

namespace Umbraco.Community.PageNotFoundTracker.Infrastructure.Blocking;

/// <summary>
/// The default <see cref="IBlockRuleService"/>. Creating a rule validates it, upserts it by its identity (pattern hash,
/// domain, culture), rebuilds this server's snapshot, tells the other servers, and then, holding the flush lock, discards
/// the matching queued hits and deletes the matching tracked records with one <c>LIKE</c> delete on
/// <see cref="TrackedUrl.RelativePath"/>.
/// </summary>
/// <remarks>
/// The context factory is resolved per call from the service provider, like in <see cref="TrackedUrlQueryService"/>:
/// the pooled factory runs its options action on first resolve, which must happen after the persistence gate opened.
/// </remarks>
internal sealed class BlockRuleService : IBlockRuleService
{
    /// <summary>
    /// The largest page size the list returns.
    /// </summary>
    internal const int MaxTake = 100;

    private const int MaxDomainLength = 255;
    private const int MaxCultureLength = 50;

    private readonly IServiceProvider serviceProvider;
    private readonly TrackingQueue queue;
    private readonly ITrackingFlusher flusher;
    private readonly IBlockSnapshotProvider snapshotProvider;
    private readonly IBlockRuleChangeNotifier notifier;
    private readonly IDomainService domainService;
    private readonly ILanguageService languageService;
    private readonly TimeProvider timeProvider;
    private readonly ILogger<BlockRuleService> logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="BlockRuleService"/> class.
    /// </summary>
    /// <param name="serviceProvider">The service provider; the context factory is resolved from it at call time only.</param>
    /// <param name="queue">The queue of hits.</param>
    /// <param name="flusher">The flusher that owns the flush lock.</param>
    /// <param name="snapshotProvider">The block snapshot provider of this server.</param>
    /// <param name="notifier">Tells the other servers about a rule change.</param>
    /// <param name="domainService">Umbraco's domain service.</param>
    /// <param name="languageService">Umbraco's language service.</param>
    /// <param name="timeProvider">The time provider.</param>
    /// <param name="logger">The logger.</param>
    public BlockRuleService(
        IServiceProvider serviceProvider,
        TrackingQueue queue,
        ITrackingFlusher flusher,
        IBlockSnapshotProvider snapshotProvider,
        IBlockRuleChangeNotifier notifier,
        IDomainService domainService,
        ILanguageService languageService,
        TimeProvider timeProvider,
        ILogger<BlockRuleService> logger)
    {
        this.serviceProvider = serviceProvider;
        this.queue = queue;
        this.flusher = flusher;
        this.snapshotProvider = snapshotProvider;
        this.notifier = notifier;
        this.domainService = domainService;
        this.languageService = languageService;
        this.timeProvider = timeProvider;
        this.logger = logger;
    }

    /// <inheritdoc />
    public async Task<BlockRulePage> QueryAsync(BlockRuleQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        int skip = Math.Max(query.Skip, 0);
        int take = Math.Clamp(query.Take, 1, MaxTake);

        await using PageNotFoundTrackerDbContext dbContext = await this.CreateContextAsync(cancellationToken);
        IQueryable<BlockRule> filtered = Filter(dbContext.BlockRules.AsNoTracking(), query);

        long total = await filtered.LongCountAsync(cancellationToken);
        List<BlockRule> items = await Order(filtered, query.SortColumn, query.Descending)
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken);

        return new BlockRulePage(items, total);
    }

    /// <inheritdoc />
    public async Task<CreateBlockRuleOutcome> CreateAsync(
        string pattern,
        int statusCode,
        string domain,
        string culture,
        CancellationToken cancellationToken)
    {
        string display = (pattern ?? string.Empty).Trim();
        string normalised = BlockPatternNormalizer.Normalize(display);
        string scopeDomain = (domain ?? string.Empty).Trim().ToLowerInvariant();
        string scopeCulture = await this.CanonicaliseCultureAsync((culture ?? string.Empty).Trim());

        string? message = BlockPatternValidator.Validate(normalised, statusCode)
            ?? ValidateScope(scopeDomain, scopeCulture);
        if (message is not null)
        {
            return new CreateBlockRuleOutcome(CreateBlockRuleStatus.Invalid, message, false);
        }

        var rule = new BlockRule
        {
            Pattern = normalised,
            PatternHash = TrackedUrlNormalizer.Hash(normalised),
            DisplayPattern = display.Length <= BlockingConstants.MaxPatternLength ? display : display[..BlockingConstants.MaxPatternLength],
            StatusCode = statusCode,
            Domain = scopeDomain,
            Culture = scopeCulture,
            Created = this.timeProvider.GetUtcNow().UtcDateTime,
        };

        await this.UpsertAsync(rule, cancellationToken);
        await this.snapshotProvider.RefreshAsync(cancellationToken);
        await this.NotifyAsync(new BlockRuleCachePayload(normalised, scopeDomain, scopeCulture, BlockRuleChange.Saved), cancellationToken);

        bool recordsRemoved = await this.RemoveMatchingRecordsAsync(normalised, scopeDomain, scopeCulture, cancellationToken);
        return new CreateBlockRuleOutcome(CreateBlockRuleStatus.Saved, null, recordsRemoved);
    }

    /// <inheritdoc />
    public async Task<bool> DeleteAsync(int id, CancellationToken cancellationToken)
    {
        BlockRule? rule;
        await using (PageNotFoundTrackerDbContext dbContext = await this.CreateContextAsync(cancellationToken))
        {
            rule = await dbContext.BlockRules.AsNoTracking().FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
            if (rule is null || await dbContext.BlockRules.Where(r => r.Id == id).ExecuteDeleteAsync(cancellationToken) == 0)
            {
                return false;
            }
        }

        await this.snapshotProvider.RefreshAsync(cancellationToken);
        await this.NotifyAsync(new BlockRuleCachePayload(rule.Pattern, rule.Domain, rule.Culture, BlockRuleChange.Removed), cancellationToken);
        return true;
    }

    /// <inheritdoc />
    public async Task<BlockScopeOptions> GetScopeOptionsAsync(CancellationToken cancellationToken)
    {
        IEnumerable<IDomain> domains = await this.domainService.GetAllAsync(false);
        cancellationToken.ThrowIfCancellationRequested();
        IEnumerable<ILanguage> languages = await this.languageService.GetAllAsync();

        string[] domainNames =
        [
            .. domains
                .Select(d => (d.DomainName ?? string.Empty).Trim().ToLowerInvariant())
                .Where(n => n.Length > 0)
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal),
        ];
        string[] cultures =
        [
            .. languages
                .Select(l => l.IsoCode)
                .Where(c => !string.IsNullOrWhiteSpace(c))
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal),
        ];
        return new BlockScopeOptions(domainNames, cultures);
    }

    /// <summary>
    /// Restricts tracked records to the ones a rule covers: <c>RelativePath LIKE pattern ESCAPE '\'</c> (or, for a
    /// pattern ending in <c>/*</c>, equal to the path before it), with the rule's domain (exact, lower-cased on both
    /// sides) and culture (ignoring case, like the in-memory matcher) when they are not empty. The delete runs this.
    /// </summary>
    /// <param name="rows">The tracked records.</param>
    /// <param name="normalisedPattern">The normalised pattern.</param>
    /// <param name="domain">The rule's domain, lower-cased; empty for all.</param>
    /// <param name="culture">The rule's culture; empty for all.</param>
    /// <returns>The matching records.</returns>
    internal static IQueryable<TrackedUrl> WhereMatches(IQueryable<TrackedUrl> rows, string normalisedPattern, string domain, string culture)
    {
        if (domain.Length > 0)
        {
            rows = rows.Where(r => r.Domain == domain);
        }

        if (culture.Length > 0)
        {
            string lowered = culture.ToLowerInvariant();
            rows = rows.Where(r => r.Culture.ToLower() == lowered);
        }

        (string like, string? exact) = BlockLikePattern.ToLike(normalisedPattern);
        return exact is null
            ? rows.Where(r => EF.Functions.Like(r.RelativePath, like, "\\"))
            : rows.Where(r => EF.Functions.Like(r.RelativePath, like, "\\") || r.RelativePath == exact);
    }

    private static string? ValidateScope(string domain, string culture)
    {
        if (domain.Length > MaxDomainLength)
        {
            return $"The domain can be at most {MaxDomainLength} characters.";
        }

        return culture.Length > MaxCultureLength ? $"The culture can be at most {MaxCultureLength} characters." : null;
    }

    private static IQueryable<BlockRule> Filter(IQueryable<BlockRule> rows, BlockRuleQuery query)
    {
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            string like = LikePattern.Contains(query.Search);
            rows = rows.Where(r => EF.Functions.Like(r.Pattern, like, "\\"));
        }

        switch (query.Domain.Kind)
        {
            case TrackedUrlFilterKind.Empty:
                rows = rows.Where(r => r.Domain == string.Empty);
                break;
            case TrackedUrlFilterKind.Exactly:
                string domain = query.Domain.Value ?? string.Empty;
                rows = rows.Where(r => r.Domain == domain);
                break;
        }

        switch (query.Culture.Kind)
        {
            case TrackedUrlFilterKind.Empty:
                rows = rows.Where(r => r.Culture == string.Empty);
                break;
            case TrackedUrlFilterKind.Exactly:
                string culture = query.Culture.Value ?? string.Empty;
                rows = rows.Where(r => r.Culture == culture);
                break;
        }

        return rows;
    }

    private static IOrderedQueryable<BlockRule> Order(IQueryable<BlockRule> rows, BlockRuleSortColumn column, bool descending)
    {
        // The final ascending Id keeps the order total, so paging neither repeats nor skips rows.
        return column switch
        {
            BlockRuleSortColumn.Pattern => OrderBy(rows, r => r.Pattern, descending),
            BlockRuleSortColumn.StatusCode => OrderBy(rows, r => r.StatusCode, descending),
            BlockRuleSortColumn.Domain => OrderBy(rows, r => r.Domain, descending),
            BlockRuleSortColumn.Culture => OrderBy(rows, r => r.Culture, descending),
            _ => OrderBy(rows, r => r.Created, descending),
        };
    }

    private static IOrderedQueryable<BlockRule> OrderBy<TKey>(IQueryable<BlockRule> rows, Expression<Func<BlockRule, TKey>> key, bool descending) =>
        (descending ? rows.OrderByDescending(key) : rows.OrderBy(key)).ThenBy(r => r.Id);

    private static Task<int> UpdateStatusAsync(DbSet<BlockRule> set, BlockRule rule, CancellationToken cancellationToken)
    {
        string hash = rule.PatternHash;
        string domain = rule.Domain;
        string culture = rule.Culture;
        int statusCode = rule.StatusCode;
        return set
            .Where(r => r.PatternHash == hash && r.Domain == domain && r.Culture == culture)
            .ExecuteUpdateAsync(setters => setters.SetProperty(r => r.StatusCode, statusCode), cancellationToken);
    }

    private async Task UpsertAsync(BlockRule rule, CancellationToken cancellationToken)
    {
        await using PageNotFoundTrackerDbContext dbContext = await this.CreateContextAsync(cancellationToken);
        if (await UpdateStatusAsync(dbContext.BlockRules, rule, cancellationToken) > 0)
        {
            return;
        }

        dbContext.BlockRules.Add(rule);
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // Another server inserted the same rule between the update and the insert: update that one instead.
            dbContext.Entry(rule).State = EntityState.Detached;
            if (await UpdateStatusAsync(dbContext.BlockRules, rule, cancellationToken) == 0)
            {
                throw;
            }
        }
    }

    /// <summary>
    /// Gives a culture one spelling, so the rule identity does not depend on case or on the database collation: the ISO
    /// code of the Umbraco language it matches (ignoring case), else the .NET culture name, else lower-cased.
    /// </summary>
    /// <param name="culture">The trimmed culture; empty means all cultures.</param>
    /// <returns>The canonical culture.</returns>
    private async Task<string> CanonicaliseCultureAsync(string culture)
    {
        if (culture.Length == 0)
        {
            return culture;
        }

        IEnumerable<ILanguage>? languages = await this.languageService.GetAllAsync();
        string? isoCode = languages?
            .Select(l => l.IsoCode)
            .FirstOrDefault(code => string.Equals(code, culture, StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrEmpty(isoCode))
        {
            return isoCode;
        }

        try
        {
            string name = CultureInfo.GetCultureInfo(culture).Name;
            return string.Equals(name, culture, StringComparison.OrdinalIgnoreCase) ? name : culture.ToLowerInvariant();
        }
        catch (CultureNotFoundException)
        {
            return culture.ToLowerInvariant();
        }
    }

    private async Task NotifyAsync(BlockRuleCachePayload payload, CancellationToken cancellationToken)
    {
        try
        {
            await this.notifier.NotifyAsync(payload, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            this.logger.LogWarning(
                ex,
                "Page Not Found Tracker: telling the other servers about the block rule {Pattern} failed. They pick it up on their next rule set rebuild.",
                payload.Pattern);
        }
    }

    private async Task<bool> RemoveMatchingRecordsAsync(string pattern, string domain, string culture, CancellationToken cancellationToken)
    {
        Func<TrackedUrlHit, bool> matches = BlockHitMatcher.Create(pattern, domain, culture);
        try
        {
            await this.flusher.RunExclusiveAsync(
                async () =>
                {
                    this.queue.DiscardMatching(matches);

                    await using PageNotFoundTrackerDbContext dbContext = await this.CreateContextAsync(cancellationToken);
                    await WhereMatches(dbContext.TrackedUrls, pattern, domain, culture).ExecuteDeleteAsync(cancellationToken);
                },
                cancellationToken);
            return true;
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            this.logger.LogError(
                ex,
                "Page Not Found Tracker: the block rule {Pattern} was saved, but deleting the tracked URLs it covers failed. The rule stays; the remaining records no longer grow.",
                pattern);
            return false;
        }
    }

    private async Task<PageNotFoundTrackerDbContext> CreateContextAsync(CancellationToken cancellationToken) =>
        await this.serviceProvider.GetRequiredService<IDbContextFactory<PageNotFoundTrackerDbContext>>().CreateDbContextAsync(cancellationToken);
}
