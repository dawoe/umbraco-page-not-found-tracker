// Copyright (c) Dave Woestenborghs and contributors. Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.EntityFrameworkCore;
using Umbraco.Cms.Tests.Common.Testing;
using Umbraco.Cms.Tests.Integration.TestServerTest;
using Umbraco.Community.PageNotFoundTracker.Core.Blocking;
using Umbraco.Community.PageNotFoundTracker.Core.Tracking;
using Umbraco.Community.PageNotFoundTracker.Infrastructure.Blocking;
using Umbraco.Community.PageNotFoundTracker.Infrastructure.Tracking;

namespace Umbraco.Community.PageNotFoundTracker.Integration.Tests.Blocking;

/// <summary>
/// Tests that the block snapshot provider is registered, loaded at startup and loads rules from the real tracking
/// database (SQLite).
/// </summary>
[TestFixture]
[UmbracoTest(Database = UmbracoTestOptions.Database.NewSchemaPerTest, Boot = true)]
internal sealed class BlockSnapshotLoadTests : UmbracoTestServerTestBase
{
    private static readonly TimeSpan ReadyTimeout = TimeSpan.FromSeconds(60);

    /// <summary>
    /// The startup load fills the snapshot, and a refresh after adding a rule serves that rule.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task Startup_load_and_refresh_read_rules_from_the_database()
    {
        using var timeout = new CancellationTokenSource(ReadyTimeout);
        bool ready = await this.GetRequiredService<ITrackingPersistenceState>().WaitUntilReadyAsync(timeout.Token);
        Assert.That(ready, Is.True, "The tracking persistence must become available.");

        var provider = this.GetRequiredService<IBlockSnapshotProvider>();

        // Wait for the startup service's initial load (a request may wait for it at most 5 s, so retry a few times).
        BlockSnapshot initial = BlockSnapshot.Empty;
        for (int i = 0; i < 10 && ReferenceEquals(initial, BlockSnapshot.Empty); i++)
        {
            initial = await provider.GetAsync(timeout.Token);
        }

        Assert.That(initial, Is.Not.SameAs(BlockSnapshot.Empty), "The startup service must load the first snapshot.");
        Assert.That(initial.FindStatus("/wp-admin/x", "localhost", null), Is.Null);

        var factory = this.GetRequiredService<IDbContextFactory<PageNotFoundTrackerDbContext>>();
        await using (PageNotFoundTrackerDbContext dbContext = await factory.CreateDbContextAsync(timeout.Token))
        {
            dbContext.BlockRules.Add(new BlockRule
            {
                Pattern = BlockPatternNormalizer.Normalize("/wp-admin/*"),
                PatternHash = TrackedUrlNormalizer.Hash("/wp-admin/*"),
                DisplayPattern = "/wp-admin/*",
                StatusCode = 410,
                Created = DateTime.UtcNow,
            });
            await dbContext.SaveChangesAsync(timeout.Token);
        }

        Assert.That(await provider.RefreshAsync(timeout.Token), Is.True);

        BlockSnapshot refreshed = await provider.GetAsync(timeout.Token);
        Assert.Multiple(() =>
        {
            Assert.That(refreshed.FindStatus("/wp-admin/x", "localhost", null), Is.EqualTo(410));
            Assert.That(initial.FindStatus("/wp-admin/x", "localhost", null), Is.Null, "The old snapshot is never edited.");
        });
    }
}
