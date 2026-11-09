// Copyright (c) Dave Woestenborghs and contributors. Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Net;
using Microsoft.EntityFrameworkCore;
using Umbraco.Cms.Core.Cache;
using Umbraco.Cms.Core.Serialization;
using Umbraco.Cms.Tests.Common.Testing;
using Umbraco.Community.PageNotFoundTracker.Core.Blocking;
using Umbraco.Community.PageNotFoundTracker.Core.Tracking;
using Umbraco.Community.PageNotFoundTracker.Infrastructure.Blocking;
using Umbraco.Community.PageNotFoundTracker.Infrastructure.Tracking;

namespace Umbraco.Community.PageNotFoundTracker.Integration.Tests.Blocking;

/// <summary>
/// Tests the block rule cache refresher in the real composition. A second server cannot be started, so the refresher is
/// invoked directly and through the notifier, which reaches it through Umbraco's distributed cache (the test host has a
/// local server messenger that delivers refreshes to this same process).
/// </summary>
[TestFixture]
[UmbracoTest(Database = UmbracoTestOptions.Database.NewSchemaPerTest, Boot = true)]
internal sealed class BlockRuleCacheRefresherIntegrationTests : PageNotFoundTestBase
{
    private static readonly TimeSpan ReadyTimeout = TimeSpan.FromSeconds(60);

    /// <summary>
    /// The refresher is part of Umbraco's cache refresher collection under its fixed id.
    /// </summary>
    [Test]
    public void Refresher_is_registered_in_the_collection()
    {
        var collection = this.GetRequiredService<CacheRefresherCollection>();

        ICacheRefresher? refresher = collection[BlockRuleCacheRefresher.UniqueId];

        Assert.Multiple(() =>
        {
            Assert.That(refresher, Is.TypeOf<BlockRuleCacheRefresher>());
            Assert.That(refresher!.Name, Is.EqualTo("Page Not Found Tracker Block Rules"));
        });
    }

    /// <summary>
    /// A rule inserted directly in the database (as another server would) applies once the registered refresher got a
    /// saved payload, and the refresher discards the matching queued hits.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task Payload_refresh_on_the_registered_refresher_makes_a_rule_inserted_directly_in_the_database_apply()
    {
        await this.InsertRuleAsync("/wp-admin/*", 410);
        using (HttpResponseMessage before = await this.GetAsync("/wp-admin/x"))
        {
            Assert.That(before.StatusCode, Is.Not.EqualTo(HttpStatusCode.Gone), "The rule must not apply before the refresh.");
        }

        var queue = this.GetRequiredService<TrackingQueue>();
        queue.TryEnqueue(Hit("/wp-admin/queued"));
        queue.TryEnqueue(Hit("/keep-me"));
        var refresher = (BlockRuleCacheRefresher)this.GetRequiredService<CacheRefresherCollection>()[BlockRuleCacheRefresher.UniqueId]!;

        refresher.Refresh([new BlockRuleCachePayload("/wp-admin/*", string.Empty, string.Empty, BlockRuleChange.Saved)]);

        using HttpResponseMessage after = await this.GetAsync("/wp-admin/x");
        Assert.Multiple(() =>
        {
            Assert.That(after.StatusCode, Is.EqualTo(HttpStatusCode.Gone));
            Assert.That(queue.TakeBatch().Select(h => h.RelativePath), Is.EqualTo(new[] { "/keep-me" }));
        });
    }

    /// <summary>
    /// The payload survives Umbraco's own JSON serializer (the one that carries it to the other servers), constructor
    /// binding and the <see cref="BlockRuleChange"/> enum included.
    /// </summary>
    /// <param name="change">The change.</param>
    [TestCase(BlockRuleChange.Saved)]
    [TestCase(BlockRuleChange.Removed)]
    public void Payload_round_trips_through_the_umbraco_json_serializer(BlockRuleChange change)
    {
        var serializer = this.GetRequiredService<IJsonSerializer>();
        var payload = new BlockRuleCachePayload("/a/*", "example.com", "nl-NL", change);

        string json = serializer.Serialize(new[] { payload });
        BlockRuleCachePayload[]? back = serializer.Deserialize<BlockRuleCachePayload[]>(json);

        Assert.That(back, Is.EqualTo(new[] { payload }), json);
    }

    /// <summary>
    /// The path that reaches another server: a payload serialized by Umbraco's serializer goes through the refresher's
    /// <c>Refresh(string json)</c>. A saved rule inserted directly in the database applies and the matching queued hit is
    /// discarded while the non-matching one stays.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task Saved_json_refresh_applies_the_rule_and_discards_the_matching_queued_hit()
    {
        await this.InsertRuleAsync("/json-saved/*", 410);
        using (HttpResponseMessage before = await this.GetAsync("/json-saved/x"))
        {
            Assert.That(before.StatusCode, Is.Not.EqualTo(HttpStatusCode.Gone), "The rule must not apply before the refresh.");
        }

        var queue = this.GetRequiredService<TrackingQueue>();
        queue.TryEnqueue(Hit("/json-saved/queued"));
        queue.TryEnqueue(Hit("/keep-me"));
        string json = this.GetRequiredService<IJsonSerializer>()
            .Serialize(new[] { new BlockRuleCachePayload("/json-saved/*", string.Empty, string.Empty, BlockRuleChange.Saved) });
        var refresher = (BlockRuleCacheRefresher)this.GetRequiredService<CacheRefresherCollection>()[BlockRuleCacheRefresher.UniqueId]!;

        refresher.Refresh(json);

        using HttpResponseMessage after = await this.GetAsync("/json-saved/x");
        Assert.Multiple(() =>
        {
            Assert.That(after.StatusCode, Is.EqualTo(HttpStatusCode.Gone));
            Assert.That(queue.TakeBatch().Select(h => h.RelativePath), Is.EqualTo(new[] { "/keep-me" }));
        });
    }

    /// <summary>
    /// A removed payload serialized by Umbraco's serializer rebuilds the snapshot (a rule inserted directly applies) and
    /// discards no queued hit.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task Removed_json_refresh_rebuilds_the_snapshot_and_keeps_queued_hits()
    {
        await this.InsertRuleAsync("/json-removed/*", 403);
        var queue = this.GetRequiredService<TrackingQueue>();
        queue.TryEnqueue(Hit("/json-removed/queued"));
        string json = this.GetRequiredService<IJsonSerializer>()
            .Serialize(new[] { new BlockRuleCachePayload("/json-removed/*", string.Empty, string.Empty, BlockRuleChange.Removed) });
        var refresher = (BlockRuleCacheRefresher)this.GetRequiredService<CacheRefresherCollection>()[BlockRuleCacheRefresher.UniqueId]!;

        refresher.Refresh(json);

        using HttpResponseMessage after = await this.GetAsync("/json-removed/x");
        Assert.Multiple(() =>
        {
            Assert.That(after.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
            Assert.That(queue.TakeBatch().Select(h => h.RelativePath), Is.EqualTo(new[] { "/json-removed/queued" }));
        });
    }

    /// <summary>
    /// The notifier reaches the refresher through the distributed cache: a rule inserted directly applies after only the
    /// notifier was called, with no local refresh.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task Notifier_reaches_the_refresher_through_the_distributed_cache()
    {
        await this.InsertRuleAsync("/xmlrpc.php", 403);

        await this.GetRequiredService<IBlockRuleChangeNotifier>().NotifyAsync(
            new BlockRuleCachePayload("/xmlrpc.php", string.Empty, string.Empty, BlockRuleChange.Saved),
            CancellationToken.None);

        HttpStatusCode status = HttpStatusCode.OK;
        for (int i = 0; i < 50 && status != HttpStatusCode.Forbidden; i++)
        {
            using HttpResponseMessage response = await this.GetAsync("/xmlrpc.php");
            status = response.StatusCode;
            if (status != HttpStatusCode.Forbidden)
            {
                await Task.Delay(100);
            }
        }

        Assert.That(status, Is.EqualTo(HttpStatusCode.Forbidden));
    }

    /// <summary>
    /// Creating a rule through the service blocks the path (the service's local refresh and the distributed one agree).
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task Creating_a_rule_through_the_service_blocks_the_path()
    {
        await this.WaitForPersistenceAsync();

        CreateBlockRuleOutcome outcome = await this.GetRequiredService<IBlockRuleService>()
            .CreateAsync("/blocked-by-service/*", 410, string.Empty, string.Empty, CancellationToken.None);
        using HttpResponseMessage response = await this.GetAsync("/blocked-by-service/x");

        Assert.Multiple(() =>
        {
            Assert.That(outcome.Status, Is.EqualTo(CreateBlockRuleStatus.Saved));
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Gone));
        });
    }

    private static TrackedUrlHit Hit(string path)
    {
        DateTime now = DateTime.UtcNow;
        return new TrackedUrlHit(path, TrackedUrlNormalizer.Hash(path), path, path, string.Empty, string.Empty, false, 1, now, now);
    }

    private async Task WaitForPersistenceAsync()
    {
        using var timeout = new CancellationTokenSource(ReadyTimeout);
        bool ready = await this.GetRequiredService<ITrackingPersistenceState>().WaitUntilReadyAsync(timeout.Token);
        Assert.That(ready, Is.True, "The tracking persistence must become available.");
    }

    private async Task InsertRuleAsync(string pattern, int statusCode)
    {
        await this.WaitForPersistenceAsync();

        var factory = this.GetRequiredService<IDbContextFactory<PageNotFoundTrackerDbContext>>();
        await using PageNotFoundTrackerDbContext dbContext = await factory.CreateDbContextAsync();
        dbContext.BlockRules.Add(new BlockRule
        {
            Pattern = BlockPatternNormalizer.Normalize(pattern),
            PatternHash = TrackedUrlNormalizer.Hash(pattern),
            DisplayPattern = pattern,
            StatusCode = statusCode,
            Created = DateTime.UtcNow,
        });
        await dbContext.SaveChangesAsync();
    }
}
