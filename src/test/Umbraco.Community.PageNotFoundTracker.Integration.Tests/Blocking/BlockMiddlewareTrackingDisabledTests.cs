// Copyright (c) Dave Woestenborghs and contributors. Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Umbraco.Cms.Core.DependencyInjection;
using Umbraco.Cms.Tests.Common.Testing;
using Umbraco.Community.PageNotFoundTracker.Core.Blocking;
using Umbraco.Community.PageNotFoundTracker.Core.Configuration;
using Umbraco.Community.PageNotFoundTracker.Infrastructure.Blocking;
using Umbraco.Community.PageNotFoundTracker.Infrastructure.Tracking;

namespace Umbraco.Community.PageNotFoundTracker.Integration.Tests.Blocking;

/// <summary>
/// Tests that blocking works with tracking switched off.
/// </summary>
[TestFixture]
[UmbracoTest(Database = UmbracoTestOptions.Database.NewSchemaPerTest, Boot = true)]
internal sealed class BlockMiddlewareTrackingDisabledTests : PageNotFoundTestBase
{
    /// <summary>
    /// A blocked request is still answered when <c>TrackingEnabled</c> is false.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task Blocking_works_with_tracking_disabled()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        var factory = this.GetRequiredService<IDbContextFactory<PageNotFoundTrackerDbContext>>();

        // Migrations still run with tracking off; wait for the first snapshot load, then add the rule.
        BlockSnapshot snapshot = BlockSnapshot.Empty;
        var provider = this.GetRequiredService<IBlockSnapshotProvider>();
        for (int i = 0; i < 10 && ReferenceEquals(snapshot, BlockSnapshot.Empty); i++)
        {
            snapshot = await provider.GetAsync(timeout.Token);
        }

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

        using HttpResponseMessage response = await this.GetAsync("/wp-admin/x");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Gone));
    }

    /// <inheritdoc />
    protected override void CustomTestSetup(IUmbracoBuilder builder)
    {
        base.CustomTestSetup(builder);
        builder.Services.Configure<PageNotFoundTrackerSettings>(settings => settings.TrackingEnabled = false);
    }
}
