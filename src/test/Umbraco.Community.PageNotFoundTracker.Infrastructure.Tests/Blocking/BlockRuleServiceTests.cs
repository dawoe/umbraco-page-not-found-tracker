// Copyright (c) Dave Woestenborghs and contributors. Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Data.Common;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Moq;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Services;
using Umbraco.Community.PageNotFoundTracker.Core.Blocking;
using Umbraco.Community.PageNotFoundTracker.Core.Configuration;
using Umbraco.Community.PageNotFoundTracker.Core.Tracking;
using Umbraco.Community.PageNotFoundTracker.Infrastructure.Blocking;
using Umbraco.Community.PageNotFoundTracker.Infrastructure.Tracking;

namespace Umbraco.Community.PageNotFoundTracker.Infrastructure.Tests.Blocking;

/// <summary>
/// Unit tests for <see cref="BlockRuleService"/>: the collaborators are mocks, the database is in-memory SQLite.
/// </summary>
[TestFixture]
internal sealed class BlockRuleServiceTests
{
    private static readonly DateTime T0 = new(2026, 11, 4, 10, 0, 0, DateTimeKind.Utc);

    private SqliteConnection connection = null!;
    private DbContextOptions<PageNotFoundTrackerDbContext> options = null!;
    private Mock<IBlockSnapshotProvider> snapshotProvider = null!;
    private Mock<IBlockRuleChangeNotifier> notifier = null!;
    private Mock<ITrackingFlusher> flusher = null!;
    private Mock<IDomainService> domainService = null!;
    private Mock<ILanguageService> languageService = null!;
    private TrackingQueue queue = null!;
    private List<string> calls = null!;
    private BlockRuleService service = null!;

    /// <summary>
    /// Opens the in-memory database, creates the schema and builds the service.
    /// </summary>
    [SetUp]
    public void SetUp()
    {
        this.connection = new SqliteConnection("Data Source=:memory:");
        this.connection.Open();
        this.options = new DbContextOptionsBuilder<PageNotFoundTrackerDbContext>().UseSqlite(this.connection).Options;
        using (var context = new PageNotFoundTrackerDbContext(this.options))
        {
            context.Database.EnsureCreated();
        }

        this.calls = [];
        this.snapshotProvider = new Mock<IBlockSnapshotProvider>();
        this.snapshotProvider.Setup(p => p.RefreshAsync(It.IsAny<CancellationToken>()))
            .Callback(() => this.calls.Add("refresh"))
            .ReturnsAsync(true);
        this.notifier = new Mock<IBlockRuleChangeNotifier>();
        this.notifier.Setup(n => n.NotifyAsync(It.IsAny<BlockRuleCachePayload>(), It.IsAny<CancellationToken>()))
            .Callback((BlockRuleCachePayload p, CancellationToken _) => this.calls.Add("notify:" + p.Change))
            .Returns(Task.CompletedTask);
        this.flusher = new Mock<ITrackingFlusher>();
        this.flusher.Setup(f => f.RunExclusiveAsync(It.IsAny<Func<Task>>(), It.IsAny<CancellationToken>()))
            .Returns((Func<Task> action, CancellationToken _) =>
            {
                this.calls.Add("exclusive");
                return action();
            });
        this.domainService = new Mock<IDomainService>();
        this.languageService = new Mock<ILanguageService>();
        this.languageService.Setup(l => l.GetAllAsync()).ReturnsAsync([Language("en-US"), Language("nl-NL")]);
        this.queue = new TrackingQueue(
            Options.Create(new PageNotFoundTrackerSettings()),
            new FakeTimeProvider(new DateTimeOffset(T0)),
            NullLogger<TrackingQueue>.Instance);

        IServiceProvider serviceProvider = new ServiceCollection()
            .AddSingleton<IDbContextFactory<PageNotFoundTrackerDbContext>>(new TestContextFactory(this.options))
            .BuildServiceProvider();
        this.service = new BlockRuleService(
            serviceProvider,
            this.queue,
            this.flusher.Object,
            this.snapshotProvider.Object,
            this.notifier.Object,
            this.domainService.Object,
            this.languageService.Object,
            new FakeTimeProvider(new DateTimeOffset(T0)),
            NullLogger<BlockRuleService>.Instance);
    }

    /// <summary>
    /// Closes the in-memory database.
    /// </summary>
    [TearDown]
    public void TearDown() => this.connection.Dispose();

    /// <summary>
    /// An invalid pattern or status code gives <see cref="CreateBlockRuleStatus.Invalid"/> with the validator's message
    /// and saves, refreshes, notifies and deletes nothing.
    /// </summary>
    /// <param name="pattern">The pattern.</param>
    /// <param name="statusCode">The status code.</param>
    /// <returns>A task.</returns>
    [TestCase("/*", 410)]
    [TestCase("", 410)]
    [TestCase("/umbraco/x*", 410)]
    [TestCase("/old", 302)]
    [TestCase("/old", 500)]
    public async Task Invalid_pattern_or_status_returns_Invalid_and_saves_nothing(string pattern, int statusCode)
    {
        await this.AddTrackedAsync("/old");

        CreateBlockRuleOutcome outcome = await this.service.CreateAsync(pattern, statusCode, string.Empty, string.Empty, CancellationToken.None);

        Assert.Multiple(async () =>
        {
            Assert.That(outcome.Status, Is.EqualTo(CreateBlockRuleStatus.Invalid));
            Assert.That(outcome.Message, Is.Not.Null.And.Not.Empty);
            Assert.That(outcome.RecordsRemoved, Is.False);
            Assert.That(await this.RulesAsync(), Is.Empty);
            Assert.That(await this.TrackedAsync(), Has.Count.EqualTo(1));
            Assert.That(this.calls, Is.Empty);
        });
    }

    /// <summary>
    /// A null pattern is invalid, not an exception.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task Null_pattern_is_invalid()
    {
        CreateBlockRuleOutcome outcome = await this.service.CreateAsync(null!, 410, string.Empty, string.Empty, CancellationToken.None);

        Assert.That(outcome.Status, Is.EqualTo(CreateBlockRuleStatus.Invalid));
    }

    /// <summary>
    /// A domain or culture longer than its column is invalid.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task Too_long_domain_or_culture_is_invalid()
    {
        CreateBlockRuleOutcome domain = await this.service.CreateAsync("/old", 410, new string('d', 256), string.Empty, CancellationToken.None);
        CreateBlockRuleOutcome culture = await this.service.CreateAsync("/old", 410, string.Empty, new string('c', 51), CancellationToken.None);

        Assert.Multiple(async () =>
        {
            Assert.That(domain.Status, Is.EqualTo(CreateBlockRuleStatus.Invalid));
            Assert.That(culture.Status, Is.EqualTo(CreateBlockRuleStatus.Invalid));
            Assert.That(await this.RulesAsync(), Is.Empty);
        });
    }

    /// <summary>
    /// A new rule is saved normalised, with the editor's spelling for display, the hash of the normalised pattern, the
    /// domain lower-cased and trimmed and the culture trimmed; then the snapshot is refreshed, the change is sent and the
    /// matching records are deleted inside the flush lock, in that order.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task Create_saves_the_rule_then_refreshes_notifies_and_deletes_in_order()
    {
        await this.AddTrackedAsync("/wp-admin/a", "www.a.local", "en-US");
        await this.AddTrackedAsync("/other", "www.a.local", "en-US");

        CreateBlockRuleOutcome outcome = await this.service.CreateAsync(" /WP-Admin/* ", 403, " WWW.A.local ", " en-US ", CancellationToken.None);

        BlockRule rule = (await this.RulesAsync()).Single();
        Assert.Multiple(async () =>
        {
            Assert.That(outcome, Is.EqualTo(new CreateBlockRuleOutcome(CreateBlockRuleStatus.Saved, null, true)));
            Assert.That(rule.Pattern, Is.EqualTo("/wp-admin/*"));
            Assert.That(rule.PatternHash, Is.EqualTo(TrackedUrlNormalizer.Hash("/wp-admin/*")));
            Assert.That(rule.DisplayPattern, Is.EqualTo("/WP-Admin/*"));
            Assert.That(rule.StatusCode, Is.EqualTo(403));
            Assert.That(rule.Domain, Is.EqualTo("www.a.local"));
            Assert.That(rule.Culture, Is.EqualTo("en-US"));
            Assert.That(rule.Created, Is.EqualTo(T0));
            Assert.That(this.calls, Is.EqualTo(new[] { "refresh", "notify:Saved", "exclusive" }));
            Assert.That((await this.TrackedAsync()).Select(r => r.RelativePath), Is.EqualTo(new[] { "/other" }));
        });

        this.notifier.Verify(
            n => n.NotifyAsync(new BlockRuleCachePayload("/wp-admin/*", "www.a.local", "en-US", BlockRuleChange.Saved), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    /// <summary>
    /// The same pattern, domain and culture again updates the status code of the one existing row and keeps the first
    /// spelling for display.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task Existing_identity_updates_the_status_in_one_row()
    {
        await this.service.CreateAsync("/Old-Page/", 410, string.Empty, string.Empty, CancellationToken.None);
        CreateBlockRuleOutcome second = await this.service.CreateAsync("/old-page", 403, string.Empty, string.Empty, CancellationToken.None);

        List<BlockRule> rules = await this.RulesAsync();
        Assert.Multiple(() =>
        {
            Assert.That(second.Status, Is.EqualTo(CreateBlockRuleStatus.Saved));
            Assert.That(rules, Has.Count.EqualTo(1));
            Assert.That(rules[0].StatusCode, Is.EqualTo(403));
            Assert.That(rules[0].DisplayPattern, Is.EqualTo("/Old-Page/"));
        });
    }

    /// <summary>
    /// The culture is stored in one spelling: the Umbraco language's ISO code ignoring case, else the .NET culture name,
    /// else lower-cased.
    /// </summary>
    /// <param name="given">The culture as passed.</param>
    /// <param name="stored">The culture as stored.</param>
    /// <returns>A task.</returns>
    [TestCase("en-us", "en-US")]
    [TestCase(" NL-nl ", "nl-NL")]
    [TestCase("fr-fr", "fr-FR")]
    [TestCase("Not-A-Culture-X", "not-a-culture-x")]
    [TestCase("", "")]
    public async Task Culture_is_canonicalised(string given, string stored)
    {
        await this.service.CreateAsync("/old", 410, string.Empty, given, CancellationToken.None);

        Assert.That((await this.RulesAsync()).Single().Culture, Is.EqualTo(stored));
    }

    /// <summary>
    /// The same culture in another case updates the one rule.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task Same_culture_other_case_updates_one_rule()
    {
        await this.service.CreateAsync("/old", 410, string.Empty, "en-US", CancellationToken.None);
        await this.service.CreateAsync("/old", 403, string.Empty, "EN-us", CancellationToken.None);

        List<BlockRule> rules = await this.RulesAsync();
        Assert.Multiple(() =>
        {
            Assert.That(rules, Has.Count.EqualTo(1));
            Assert.That(rules[0].StatusCode, Is.EqualTo(403));
            Assert.That(rules[0].Culture, Is.EqualTo("en-US"));
        });
    }

    /// <summary>
    /// A failing delete is logged, gives <c>RecordsRemoved=false</c> and keeps the rule; refresh and notify still ran.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task Delete_failure_keeps_the_rule_and_returns_RecordsRemoved_false()
    {
        await using (var context = new PageNotFoundTrackerDbContext(this.options))
        {
            await context.Database.ExecuteSqlRawAsync($"DROP TABLE {TrackingConstants.TableName}");
        }

        CreateBlockRuleOutcome outcome = await this.service.CreateAsync("/old", 410, string.Empty, string.Empty, CancellationToken.None);

        Assert.Multiple(async () =>
        {
            Assert.That(outcome.Status, Is.EqualTo(CreateBlockRuleStatus.Saved));
            Assert.That(outcome.RecordsRemoved, Is.False);
            Assert.That(outcome.Message, Is.EqualTo(BlockingConstants.RecordsNotRemovedMessage));
            Assert.That(await this.RulesAsync(), Has.Count.EqualTo(1));
            Assert.That(this.calls, Is.EqualTo(new[] { "refresh", "notify:Saved", "exclusive" }));
        });
    }

    /// <summary>
    /// A failing change notification is logged; the records are still deleted.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task Notify_failure_still_deletes_the_records()
    {
        await this.AddTrackedAsync("/old");
        this.notifier.Setup(n => n.NotifyAsync(It.IsAny<BlockRuleCachePayload>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("boom"));

        CreateBlockRuleOutcome outcome = await this.service.CreateAsync("/old", 410, string.Empty, string.Empty, CancellationToken.None);

        Assert.Multiple(async () =>
        {
            Assert.That(outcome.RecordsRemoved, Is.True);
            Assert.That(await this.TrackedAsync(), Is.Empty);
        });
    }

    /// <summary>
    /// Queued hits that match the rule within its scope are discarded; others stay.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task Create_discards_matching_queued_hits_in_scope_only()
    {
        this.queue.TryEnqueue(QueuedHit("/wp-admin/a", "www.a.local", "en-US"));
        this.queue.TryEnqueue(QueuedHit("/wp-admin/b", "www.b.local", "en-US"));
        this.queue.TryEnqueue(QueuedHit("/wp-admin/c", "www.a.local", "nl-NL"));
        this.queue.TryEnqueue(QueuedHit("/other", "www.a.local", "en-US"));

        await this.service.CreateAsync("/wp-admin/*", 410, "www.a.local", "en-us", CancellationToken.None);

        Assert.That(this.queue.TakeBatch().Select(h => h.RelativePath), Is.EquivalentTo(new[] { "/wp-admin/b", "/wp-admin/c", "/other" }));
    }

    /// <summary>
    /// Deleting an unknown id gives false and neither refreshes nor notifies.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task Delete_of_unknown_id_is_false()
    {
        bool deleted = await this.service.DeleteAsync(12345, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(deleted, Is.False);
            Assert.That(this.calls, Is.Empty);
        });
    }

    /// <summary>
    /// Deleting a rule removes it, refreshes the snapshot and sends a removal.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task Delete_removes_the_rule_refreshes_and_notifies_removed()
    {
        await this.service.CreateAsync("/old", 410, "www.a.local", "en-US", CancellationToken.None);
        int id = (await this.RulesAsync()).Single().Id;
        this.calls.Clear();

        bool deleted = await this.service.DeleteAsync(id, CancellationToken.None);

        Assert.Multiple(async () =>
        {
            Assert.That(deleted, Is.True);
            Assert.That(await this.RulesAsync(), Is.Empty);
            Assert.That(this.calls, Is.EqualTo(new[] { "refresh", "notify:Removed" }));
        });
        this.notifier.Verify(
            n => n.NotifyAsync(new BlockRuleCachePayload("/old", "www.a.local", "en-US", BlockRuleChange.Removed), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    /// <summary>
    /// A request that is cancelled after the rule is committed still refreshes, notifies and removes the covered records:
    /// the post-save steps must not depend on the request token.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task Create_cancelled_after_the_save_still_refreshes_notifies_and_removes_records()
    {
        await this.AddTrackedAsync("/old");
        using var cts = new CancellationTokenSource();
        BlockRuleService cancelling = this.BuildServiceCancellingAfterWrite(new CancelAfterSaveInterceptor(cts));

        CreateBlockRuleOutcome outcome = await cancelling.CreateAsync("/old", 410, string.Empty, string.Empty, cts.Token);

        Assert.Multiple(async () =>
        {
            Assert.That(cts.IsCancellationRequested, Is.True, "The test must have cancelled the request after the save.");
            Assert.That(outcome.Status, Is.EqualTo(CreateBlockRuleStatus.Saved));
            Assert.That(outcome.RecordsRemoved, Is.True);
            Assert.That(this.calls, Is.EqualTo(new[] { "refresh", "notify:Saved", "exclusive" }));
            Assert.That(await this.TrackedAsync(), Is.Empty);
        });
    }

    /// <summary>
    /// A request that is cancelled after the rule is deleted still refreshes and notifies the removal.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task Delete_cancelled_after_the_delete_still_refreshes_and_notifies()
    {
        await this.service.CreateAsync("/old", 410, string.Empty, string.Empty, CancellationToken.None);
        int id = (await this.RulesAsync()).Single().Id;
        this.calls.Clear();
        using var cts = new CancellationTokenSource();
        BlockRuleService cancelling = this.BuildServiceCancellingAfterWrite(new CancelAfterRuleDeleteInterceptor(cts));

        bool deleted = await cancelling.DeleteAsync(id, cts.Token);

        Assert.Multiple(async () =>
        {
            Assert.That(cts.IsCancellationRequested, Is.True, "The test must have cancelled the request after the delete.");
            Assert.That(deleted, Is.True);
            Assert.That(await this.RulesAsync(), Is.Empty);
            Assert.That(this.calls, Is.EqualTo(new[] { "refresh", "notify:Removed" }));
        });
    }

    /// <summary>
    /// An update changes every field in place, keeps the id and the creation date, and then refreshes, notifies and
    /// deletes the records the changed rule covers, in that order.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task Update_changes_every_field_keeps_id_and_created_then_refreshes_notifies_and_deletes_in_order()
    {
        await this.service.CreateAsync("/old", 410, string.Empty, string.Empty, CancellationToken.None);
        BlockRule before = (await this.RulesAsync()).Single();
        await this.AddTrackedAsync("/wp-admin/a", "www.a.local", "en-US");
        await this.AddTrackedAsync("/other", "www.a.local", "en-US");
        this.calls.Clear();

        CreateBlockRuleOutcome outcome = await this.service.UpdateAsync(before.Id, " /WP-Admin/* ", 403, " WWW.A.local ", " en-us ", CancellationToken.None);

        BlockRule rule = (await this.RulesAsync()).Single();
        Assert.Multiple(async () =>
        {
            Assert.That(outcome, Is.EqualTo(new CreateBlockRuleOutcome(CreateBlockRuleStatus.Saved, null, true)));
            Assert.That(rule.Id, Is.EqualTo(before.Id));
            Assert.That(rule.Created, Is.EqualTo(before.Created));
            Assert.That(rule.Pattern, Is.EqualTo("/wp-admin/*"));
            Assert.That(rule.PatternHash, Is.EqualTo(TrackedUrlNormalizer.Hash("/wp-admin/*")));
            Assert.That(rule.DisplayPattern, Is.EqualTo("/WP-Admin/*"));
            Assert.That(rule.StatusCode, Is.EqualTo(403));
            Assert.That(rule.Domain, Is.EqualTo("www.a.local"));
            Assert.That(rule.Culture, Is.EqualTo("en-US"));
            Assert.That(this.calls, Is.EqualTo(new[] { "refresh", "notify:Saved", "exclusive" }));
            Assert.That((await this.TrackedAsync()).Select(r => r.RelativePath), Is.EqualTo(new[] { "/other" }));
        });
        this.notifier.Verify(
            n => n.NotifyAsync(new BlockRuleCachePayload("/wp-admin/*", "www.a.local", "en-US", BlockRuleChange.Saved), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    /// <summary>
    /// Updating an unknown id gives <see cref="CreateBlockRuleStatus.NotFound"/> and does nothing.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task Update_of_unknown_id_is_NotFound_and_does_nothing()
    {
        CreateBlockRuleOutcome outcome = await this.service.UpdateAsync(999, "/x", 410, string.Empty, string.Empty, CancellationToken.None);

        Assert.Multiple(async () =>
        {
            Assert.That(outcome.Status, Is.EqualTo(CreateBlockRuleStatus.NotFound));
            Assert.That(outcome.RecordsRemoved, Is.False);
            Assert.That(await this.RulesAsync(), Is.Empty);
            Assert.That(this.calls, Is.Empty);
        });
    }

    /// <summary>
    /// An invalid pattern or status code gives <see cref="CreateBlockRuleStatus.Invalid"/> and changes nothing.
    /// </summary>
    /// <param name="pattern">The pattern.</param>
    /// <param name="statusCode">The status code.</param>
    /// <returns>A task.</returns>
    [TestCase("/*", 410)]
    [TestCase("/umbraco/x*", 410)]
    [TestCase("/old", 302)]
    public async Task Update_with_invalid_pattern_or_status_is_Invalid_and_changes_nothing(string pattern, int statusCode)
    {
        await this.service.CreateAsync("/old", 410, string.Empty, string.Empty, CancellationToken.None);
        int id = (await this.RulesAsync()).Single().Id;

        CreateBlockRuleOutcome outcome = await this.service.UpdateAsync(id, pattern, statusCode, string.Empty, string.Empty, CancellationToken.None);

        BlockRule rule = (await this.RulesAsync()).Single();
        Assert.Multiple(() =>
        {
            Assert.That(outcome.Status, Is.EqualTo(CreateBlockRuleStatus.Invalid));
            Assert.That(outcome.Message, Is.Not.Null.And.Not.Empty);
            Assert.That(rule.Pattern, Is.EqualTo("/old"));
            Assert.That(rule.StatusCode, Is.EqualTo(410));
            Assert.That(this.calls, Is.EqualTo(new[] { "refresh", "notify:Saved", "exclusive" }));
        });
    }

    /// <summary>
    /// An update into the identity of another rule gives <see cref="CreateBlockRuleStatus.Conflict"/> and changes nothing.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task Update_into_another_rules_identity_is_Conflict_and_changes_nothing()
    {
        await this.service.CreateAsync("/a", 410, string.Empty, string.Empty, CancellationToken.None);
        await this.service.CreateAsync("/b", 404, string.Empty, string.Empty, CancellationToken.None);
        int idB = (await this.RulesAsync()).Single(r => r.Pattern == "/b").Id;
        this.calls.Clear();

        CreateBlockRuleOutcome outcome = await this.service.UpdateAsync(idB, " /A/ ", 403, string.Empty, string.Empty, CancellationToken.None);

        List<BlockRule> rules = (await this.RulesAsync()).OrderBy(r => r.Pattern, StringComparer.Ordinal).ToList();
        Assert.Multiple(() =>
        {
            Assert.That(outcome.Status, Is.EqualTo(CreateBlockRuleStatus.Conflict));
            Assert.That(outcome.Message, Is.EqualTo(BlockingConstants.RuleExistsMessage));
            Assert.That(rules.Select(r => r.Pattern), Is.EqualTo(new[] { "/a", "/b" }));
            Assert.That(rules.Select(r => r.StatusCode), Is.EqualTo(new[] { 410, 404 }));
            Assert.That(this.calls, Is.Empty);
        });
    }

    /// <summary>
    /// Changing only the status or the spelling of a rule is not a conflict with itself.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task Update_of_only_the_status_or_spelling_of_the_same_rule_is_Saved()
    {
        await this.service.CreateAsync("/wp-admin/*", 410, string.Empty, string.Empty, CancellationToken.None);
        BlockRule before = (await this.RulesAsync()).Single();

        CreateBlockRuleOutcome outcome = await this.service.UpdateAsync(before.Id, "/WP-Admin/*/", 403, string.Empty, string.Empty, CancellationToken.None);

        List<BlockRule> rules = await this.RulesAsync();
        Assert.Multiple(() =>
        {
            Assert.That(outcome.Status, Is.EqualTo(CreateBlockRuleStatus.Saved));
            Assert.That(rules, Has.Count.EqualTo(1));
            Assert.That(rules[0].Pattern, Is.EqualTo("/wp-admin/*"));
            Assert.That(rules[0].DisplayPattern, Is.EqualTo("/WP-Admin/*/"));
            Assert.That(rules[0].StatusCode, Is.EqualTo(403));
            Assert.That(rules[0].PatternHash, Is.EqualTo(before.PatternHash));
        });
    }

    /// <summary>
    /// An update canonicalises the culture like a create.
    /// </summary>
    /// <param name="given">The culture as passed.</param>
    /// <param name="stored">The culture as stored.</param>
    /// <returns>A task.</returns>
    [TestCase("en-us", "en-US")]
    [TestCase("Not-A-Culture-X", "not-a-culture-x")]
    public async Task Update_canonicalises_the_culture(string given, string stored)
    {
        await this.service.CreateAsync("/old", 410, string.Empty, string.Empty, CancellationToken.None);
        int id = (await this.RulesAsync()).Single().Id;

        await this.service.UpdateAsync(id, "/old", 410, string.Empty, given, CancellationToken.None);

        Assert.That((await this.RulesAsync()).Single().Culture, Is.EqualTo(stored));
    }

    /// <summary>
    /// A failing record delete keeps the change and gives <c>RecordsRemoved=false</c>.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task Update_delete_failure_keeps_the_change_and_returns_RecordsRemoved_false()
    {
        await this.service.CreateAsync("/old", 410, string.Empty, string.Empty, CancellationToken.None);
        int id = (await this.RulesAsync()).Single().Id;
        await using (var context = new PageNotFoundTrackerDbContext(this.options))
        {
            await context.Database.ExecuteSqlRawAsync($"DROP TABLE {TrackingConstants.TableName}");
        }

        CreateBlockRuleOutcome outcome = await this.service.UpdateAsync(id, "/new", 410, string.Empty, string.Empty, CancellationToken.None);

        Assert.Multiple(async () =>
        {
            Assert.That(outcome.Status, Is.EqualTo(CreateBlockRuleStatus.Saved));
            Assert.That(outcome.RecordsRemoved, Is.False);
            Assert.That(outcome.Message, Is.EqualTo(BlockingConstants.RecordsNotRemovedMessage));
            Assert.That((await this.RulesAsync()).Single().Pattern, Is.EqualTo("/new"));
        });
    }

    /// <summary>
    /// Queued hits that match the changed rule within its scope are discarded; others stay.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task Update_discards_matching_queued_hits_in_scope_only()
    {
        await this.service.CreateAsync("/old", 410, string.Empty, string.Empty, CancellationToken.None);
        int id = (await this.RulesAsync()).Single().Id;
        this.queue.TryEnqueue(QueuedHit("/new/a", string.Empty, string.Empty));
        this.queue.TryEnqueue(QueuedHit("/new/a", "www.b.local", string.Empty));
        this.queue.TryEnqueue(QueuedHit("/other", string.Empty, string.Empty));

        await this.service.UpdateAsync(id, "/new/*", 410, "www.b.local", string.Empty, CancellationToken.None);

        List<TrackedUrlHit> left = [.. this.queue.TakeBatch()];
        Assert.That(left.Select(h => (h.RelativePath, h.Domain)), Is.EquivalentTo(new[] { ("/new/a", string.Empty), ("/other", string.Empty) }));
    }

    /// <summary>
    /// A request that is cancelled after the rule update is committed still refreshes, notifies and removes the covered
    /// records.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task Update_cancelled_after_the_write_still_refreshes_notifies_and_removes_records()
    {
        await this.service.CreateAsync("/old", 410, string.Empty, string.Empty, CancellationToken.None);
        int id = (await this.RulesAsync()).Single().Id;
        await this.AddTrackedAsync("/new");
        await this.AddTrackedAsync("/other");
        using var cts = new CancellationTokenSource();
        BlockRuleService cancelling = this.BuildServiceCancellingAfterWrite(new CancelAfterRuleUpdateInterceptor(cts));

        CreateBlockRuleOutcome outcome = await cancelling.UpdateAsync(id, "/new", 410, string.Empty, string.Empty, cts.Token);

        Assert.Multiple(async () =>
        {
            Assert.That(cts.IsCancellationRequested, Is.True, "The test must have cancelled the request after the update.");
            Assert.That(outcome.Status, Is.EqualTo(CreateBlockRuleStatus.Saved));
            Assert.That(this.calls.TakeLast(3), Is.EqualTo(new[] { "refresh", "notify:Saved", "exclusive" }));
            Assert.That((await this.TrackedAsync()).Select(r => r.RelativePath), Is.EqualTo(new[] { "/other" }));
        });
    }

    /// <summary>
    /// When another server takes the new identity between the collision check and the write, the unique index rejects
    /// the write and the update gives <see cref="CreateBlockRuleStatus.Conflict"/>.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task Update_unique_violation_on_the_write_is_Conflict()
    {
        await this.service.CreateAsync("/a", 410, string.Empty, string.Empty, CancellationToken.None);
        await this.service.CreateAsync("/b", 404, string.Empty, string.Empty, CancellationToken.None);
        int idB = (await this.RulesAsync()).Single(r => r.Pattern == "/b").Id;
        this.calls.Clear();
        BlockRuleService racing = this.BuildServiceWithInterceptor(new InsertBeforeRuleUpdateInterceptor("/c"));

        CreateBlockRuleOutcome outcome = await racing.UpdateAsync(idB, "/c", 403, string.Empty, string.Empty, CancellationToken.None);

        List<BlockRule> rules = await this.RulesAsync();
        BlockRule ruleB = rules.Single(r => r.Id == idB);
        Assert.Multiple(() =>
        {
            Assert.That(outcome.Status, Is.EqualTo(CreateBlockRuleStatus.Conflict));
            Assert.That(outcome.Message, Is.EqualTo(BlockingConstants.RuleExistsMessage));
            Assert.That(ruleB.Pattern, Is.EqualTo("/b"));
            Assert.That(ruleB.StatusCode, Is.EqualTo(404));
            Assert.That(rules, Has.Count.EqualTo(3));
            Assert.That(this.calls, Is.Empty);
        });
    }

    /// <summary>
    /// The scope options list the domain names lower-cased, distinct and sorted (wildcard domains excluded), and the
    /// language ISO codes.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task Scope_options_list_domains_and_languages()
    {
        this.domainService.Setup(d => d.GetAllAsync(false)).ReturnsAsync(
        [
            Domain("WWW.B.local"),
            Domain("www.a.local:44402/siteA"),
            Domain("www.b.local"),
        ]);
        this.languageService.Setup(l => l.GetAllAsync()).ReturnsAsync([Language("nl-NL"), Language("en-US")]);

        BlockScopeOptions options = await this.service.GetScopeOptionsAsync(CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(options.Domains, Is.EqualTo(new[] { "www.a.local:44402/sitea", "www.b.local" }));
            Assert.That(options.Cultures, Is.EqualTo(new[] { "en-US", "nl-NL" }));
        });
    }

    /// <summary>
    /// Builds a service whose collaborators throw when given a cancelled token (like the real ones) and whose database
    /// runs <paramref name="interceptor"/>, which cancels the request right after the rule write (insert, update or
    /// delete of a rule) committed.
    /// </summary>
    private BlockRuleService BuildServiceCancellingAfterWrite(IInterceptor interceptor)
    {
        this.snapshotProvider.Setup(p => p.RefreshAsync(It.IsAny<CancellationToken>()))
            .Returns((CancellationToken t) =>
            {
                t.ThrowIfCancellationRequested();
                this.calls.Add("refresh");
                return Task.FromResult(true);
            });
        this.notifier.Setup(n => n.NotifyAsync(It.IsAny<BlockRuleCachePayload>(), It.IsAny<CancellationToken>()))
            .Returns((BlockRuleCachePayload p, CancellationToken t) =>
            {
                t.ThrowIfCancellationRequested();
                this.calls.Add("notify:" + p.Change);
                return Task.CompletedTask;
            });
        this.flusher.Setup(f => f.RunExclusiveAsync(It.IsAny<Func<Task>>(), It.IsAny<CancellationToken>()))
            .Returns((Func<Task> action, CancellationToken t) =>
            {
                t.ThrowIfCancellationRequested();
                this.calls.Add("exclusive");
                return action();
            });

        return this.BuildServiceWithInterceptor(interceptor);
    }

    /// <summary>
    /// Builds a service on the fixture's collaborators whose database runs <paramref name="interceptor"/>.
    /// </summary>
    private BlockRuleService BuildServiceWithInterceptor(IInterceptor interceptor)
    {
        DbContextOptions<PageNotFoundTrackerDbContext> intercepted = new DbContextOptionsBuilder<PageNotFoundTrackerDbContext>()
            .UseSqlite(this.connection)
            .AddInterceptors(interceptor)
            .Options;
        IServiceProvider serviceProvider = new ServiceCollection()
            .AddSingleton<IDbContextFactory<PageNotFoundTrackerDbContext>>(new TestContextFactory(intercepted))
            .BuildServiceProvider();
        return new BlockRuleService(
            serviceProvider,
            this.queue,
            this.flusher.Object,
            this.snapshotProvider.Object,
            this.notifier.Object,
            this.domainService.Object,
            this.languageService.Object,
            new FakeTimeProvider(new DateTimeOffset(T0)),
            NullLogger<BlockRuleService>.Instance);
    }

    private static IDomain Domain(string name) => Mock.Of<IDomain>(d => d.DomainName == name);

    private static ILanguage Language(string isoCode) => Mock.Of<ILanguage>(l => l.IsoCode == isoCode);

    private static bool IsRuleUpdate(DbCommand command) =>
        command.CommandText.Contains("UPDATE", StringComparison.Ordinal) && command.CommandText.Contains(BlockingConstants.TableName, StringComparison.Ordinal);

    private static TrackedUrlHit QueuedHit(string relativePath, string domain, string culture) =>
        new(relativePath, "hash" + relativePath + domain + culture, relativePath, relativePath, domain, culture, false, 1, T0, T0);

    private async Task AddTrackedAsync(string relativePath, string domain = "", string culture = "")
    {
        await using var context = new PageNotFoundTrackerDbContext(this.options);
        context.TrackedUrls.Add(new TrackedUrl
        {
            Url = relativePath,
            UrlHash = TrackedUrlNormalizer.Hash(relativePath + domain + culture),
            RelativePath = relativePath,
            DisplayUrl = relativePath,
            Domain = domain,
            Culture = culture,
            HitCount = 1,
            FirstSeen = T0,
            LastSeen = T0,
        });
        await context.SaveChangesAsync();
    }

    private async Task<List<BlockRule>> RulesAsync()
    {
        await using var context = new PageNotFoundTrackerDbContext(this.options);
        return await context.BlockRules.AsNoTracking().ToListAsync();
    }

    private async Task<List<TrackedUrl>> TrackedAsync()
    {
        await using var context = new PageNotFoundTrackerDbContext(this.options);
        return await context.TrackedUrls.AsNoTracking().ToListAsync();
    }

    private sealed class TestContextFactory(DbContextOptions<PageNotFoundTrackerDbContext> options) : IDbContextFactory<PageNotFoundTrackerDbContext>
    {
        public PageNotFoundTrackerDbContext CreateDbContext() => new(options);
    }

    private sealed class CancelAfterSaveInterceptor(CancellationTokenSource cts) : SaveChangesInterceptor
    {
        public override ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result, CancellationToken cancellationToken = default)
        {
            cts.Cancel();
            return base.SavedChangesAsync(eventData, result, cancellationToken);
        }
    }

    private sealed class CancelAfterRuleDeleteInterceptor(CancellationTokenSource cts) : DbCommandInterceptor
    {
        public override ValueTask<int> NonQueryExecutedAsync(DbCommand command, CommandExecutedEventData eventData, int result, CancellationToken cancellationToken = default)
        {
            if (command.CommandText.StartsWith("DELETE", StringComparison.Ordinal) && command.CommandText.Contains(BlockingConstants.TableName, StringComparison.Ordinal))
            {
                cts.Cancel();
            }

            return base.NonQueryExecutedAsync(command, eventData, result, cancellationToken);
        }
    }

    private sealed class CancelAfterRuleUpdateInterceptor(CancellationTokenSource cts) : DbCommandInterceptor
    {
        public override ValueTask<int> NonQueryExecutedAsync(DbCommand command, CommandExecutedEventData eventData, int result, CancellationToken cancellationToken = default)
        {
            if (IsRuleUpdate(command))
            {
                cts.Cancel();
            }

            return base.NonQueryExecutedAsync(command, eventData, result, cancellationToken);
        }
    }

    /// <summary>
    /// Inserts a rule with <paramref name="pattern"/> (all domains, all cultures) right before the first rule update
    /// runs, like another server would between the collision check and the write.
    /// </summary>
    private sealed class InsertBeforeRuleUpdateInterceptor(string pattern) : DbCommandInterceptor
    {
        private bool fired;

        public override async ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand command, CommandEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (!this.fired && IsRuleUpdate(command))
            {
                this.fired = true;
                await using DbCommand insert = command.Connection!.CreateCommand();
                insert.Transaction = command.Transaction;
                insert.CommandText =
                    $"INSERT INTO {BlockingConstants.TableName} (Pattern, PatternHash, DisplayPattern, StatusCode, Domain, Culture, Created) " +
                    "VALUES ($pattern, $hash, $pattern, 410, '', '', $created)";
                AddParameter(insert, "$pattern", pattern);
                AddParameter(insert, "$hash", TrackedUrlNormalizer.Hash(pattern));
                AddParameter(insert, "$created", T0);
                await insert.ExecuteNonQueryAsync(cancellationToken);
            }

            return await base.NonQueryExecutingAsync(command, eventData, result, cancellationToken);
        }

        private static void AddParameter(DbCommand command, string name, object value)
        {
            DbParameter parameter = command.CreateParameter();
            parameter.ParameterName = name;
            parameter.Value = value;
            command.Parameters.Add(parameter);
        }
    }
}
