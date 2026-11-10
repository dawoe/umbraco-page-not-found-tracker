// Copyright (c) Dave Woestenborghs and contributors. Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Tests.Common.Testing;
using Umbraco.Community.PageNotFoundTracker.Core.Blocking;
using Umbraco.Community.PageNotFoundTracker.Core.Tracking;
using Umbraco.Community.PageNotFoundTracker.Infrastructure.Blocking;
using Umbraco.Community.PageNotFoundTracker.Infrastructure.Tracking;

namespace Umbraco.Community.PageNotFoundTracker.Integration.Tests.Blocking;

/// <summary>
/// Tests the block middleware in the real Umbraco pipeline: blocked requests are answered ahead of routing, static files
/// and redirects, and never recorded.
/// </summary>
[TestFixture]
[UmbracoTest(Database = UmbracoTestOptions.Database.NewSchemaPerTest, Boot = true)]
internal sealed class BlockMiddlewareIntegrationTests : PageNotFoundTestBase
{
    private const string StaticFileName = "pnft-block-test.txt";

    private static readonly TimeSpan ReadyTimeout = TimeSpan.FromSeconds(60);

    private string? staticFilePath;

    /// <summary>
    /// Removes the throwaway static file.
    /// </summary>
    [TearDown]
    public void RemoveStaticFile()
    {
        if (this.staticFilePath is not null && File.Exists(this.staticFilePath))
        {
            File.Delete(this.staticFilePath);
        }
    }

    /// <summary>
    /// A blocked missing path gets 410 with an empty body and is not recorded.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task Blocked_missing_path_gets_410_without_body_and_is_not_recorded()
    {
        await this.AddRuleAsync("/wp-admin/*", 410);

        using HttpResponseMessage response = await this.GetAsync("/wp-admin/setup.php?x=1");
        using HttpResponseMessage head = await this.SendAsync(HttpMethod.Head, "/wp-admin/x");
        using HttpResponseMessage post = await this.SendAsync(HttpMethod.Post, "/wp-admin/x");
        string body = await response.Content.ReadAsStringAsync();

        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Gone));
            Assert.That(body, Is.Empty);
            Assert.That(head.StatusCode, Is.EqualTo(HttpStatusCode.Gone));
            Assert.That(post.StatusCode, Is.EqualTo(HttpStatusCode.Gone));
            Assert.That(this.Recorder.Occurrences, Is.Empty);
        });
    }

    /// <summary>
    /// A path that is an existing published page is blocked too.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task Existing_page_is_blocked()
    {
        using (HttpResponseMessage before = await this.GetAsync("/about"))
        {
            Assert.That(before.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        }

        await this.AddRuleAsync("/about", 403);

        using HttpResponseMessage response = await this.GetAsync("/about");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
    }

    /// <summary>
    /// A physical file in the web root is blocked: the middleware runs ahead of the static file middleware.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task Static_file_is_blocked_ahead_of_the_static_file_middleware()
    {
        var environment = this.GetRequiredService<IWebHostEnvironment>();
        string? webRoot = environment.WebRootPath;
        Assert.That(webRoot, Is.Not.Null.And.Not.Empty, "The test host has no web root, so static files cannot be served.");
        Directory.CreateDirectory(webRoot!);
        this.staticFilePath = Path.Combine(webRoot!, StaticFileName);
        await File.WriteAllTextAsync(this.staticFilePath, "static content");

        using (HttpResponseMessage before = await this.GetAsync("/" + StaticFileName))
        {
            Assert.That(before.StatusCode, Is.EqualTo(HttpStatusCode.OK), "The static file must be served before it is blocked.");
            Assert.That(await before.Content.ReadAsStringAsync(), Is.EqualTo("static content"));
        }

        await this.AddRuleAsync("/" + StaticFileName, 410);

        using HttpResponseMessage response = await this.GetAsync("/" + StaticFileName);

        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Gone));
            Assert.That(response.Content.ReadAsStringAsync().GetAwaiter().GetResult(), Is.Empty);
        });
    }

    /// <summary>
    /// A block beats a redirect registered for the same URL.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task Block_beats_a_redirect_for_the_same_url()
    {
        this.GetRequiredService<IRedirectUrlService>().Register("/old-page", OtherPageKey, null);
        using (HttpResponseMessage before = await this.GetAsync("/old-page"))
        {
            Assert.That(before.StatusCode, Is.EqualTo(HttpStatusCode.MovedPermanently), "The redirect must work before blocking.");
        }

        await this.AddRuleAsync("/old-page", 410);

        using HttpResponseMessage response = await this.GetAsync("/old-page");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Gone));
    }

    /// <summary>
    /// Umbraco-owned paths are never blocked, also by patterns that would match them.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task Umbraco_owned_paths_are_never_blocked()
    {
        await this.AddRuleAsync("/*/management/*", 410);
        await this.AddRuleAsync("/*", 403);

        using HttpResponseMessage status = await this.GetAsync("/umbraco/management/api/v1/server/status");
        using HttpResponseMessage blockable = await this.GetAsync("/umbracoextra");

        Assert.Multiple(() =>
        {
            Assert.That(status.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(blockable.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
        });
    }

    /// <summary>
    /// Editing a rule frees the old path (404 page again, recorded) and blocks the new one (empty body, not recorded).
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task Edited_rule_frees_the_old_path_and_blocks_the_new_one()
    {
        IBlockRuleService rules = this.GetRequiredService<IBlockRuleService>();
        await this.WaitForPersistenceAsync();
        await rules.CreateAsync("/pnft-old", 410, string.Empty, string.Empty, CancellationToken.None);
        using (HttpResponseMessage blocked = await this.GetAsync("/pnft-old"))
        {
            Assert.That(blocked.StatusCode, Is.EqualTo(HttpStatusCode.Gone));
        }

        BlockRulePage page = await rules.QueryAsync(
            new BlockRuleQuery(0, 10, BlockRuleSortColumn.Created, true, "pnft-old", TrackedUrlFilter.Any, TrackedUrlFilter.Any),
            CancellationToken.None);
        int id = page.Items.Single().Id;

        CreateBlockRuleOutcome outcome = await rules.UpdateAsync(id, "/pnft-new/*", 403, string.Empty, string.Empty, CancellationToken.None);
        using HttpResponseMessage old = await this.GetAsync("/pnft-old");
        using HttpResponseMessage fresh = await this.GetAsync("/pnft-new/x");

        Assert.Multiple(async () =>
        {
            Assert.That(outcome.Status, Is.EqualTo(CreateBlockRuleStatus.Saved));
            Assert.That(old.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
            Assert.That(this.Recorder.Occurrences.Select(o => o.PathAndQuery), Does.Contain("/pnft-old"));
            Assert.That(fresh.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
            Assert.That(await fresh.Content.ReadAsStringAsync(), Is.Empty);
            Assert.That(this.Recorder.Occurrences.Select(o => o.PathAndQuery), Does.Not.Contain("/pnft-new/x"));
        });
    }

    private async Task WaitForPersistenceAsync()
    {
        using var timeout = new CancellationTokenSource(ReadyTimeout);
        bool ready = await this.GetRequiredService<ITrackingPersistenceState>().WaitUntilReadyAsync(timeout.Token);
        Assert.That(ready, Is.True, "The tracking persistence must become available.");
    }

    private async Task AddRuleAsync(string pattern, int statusCode)
    {
        using var timeout = new CancellationTokenSource(ReadyTimeout);
        await this.WaitForPersistenceAsync();

        var factory = this.GetRequiredService<IDbContextFactory<PageNotFoundTrackerDbContext>>();
        await using (PageNotFoundTrackerDbContext dbContext = await factory.CreateDbContextAsync(timeout.Token))
        {
            dbContext.BlockRules.Add(new BlockRule
            {
                Pattern = BlockPatternNormalizer.Normalize(pattern),
                PatternHash = TrackedUrlNormalizer.Hash(pattern + statusCode),
                DisplayPattern = pattern,
                StatusCode = statusCode,
                Created = DateTime.UtcNow,
            });
            await dbContext.SaveChangesAsync(timeout.Token);
        }

        Assert.That(await this.GetRequiredService<IBlockSnapshotProvider>().RefreshAsync(timeout.Token), Is.True);
    }
}
