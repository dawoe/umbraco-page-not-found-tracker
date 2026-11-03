// Copyright (c) Dave Woestenborghs and contributors. Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Net;
using System.Reflection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using NPoco;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Cache;
using Umbraco.Cms.Core.DependencyInjection;
using Umbraco.Cms.Core.Notifications;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core.Sync;
using Umbraco.Cms.Infrastructure.Scoping;
using Umbraco.Cms.Tests.Common.Builders;
using Umbraco.Cms.Tests.Common.Builders.Extensions;
using Umbraco.Cms.Tests.Common.Testing;
using Umbraco.Cms.Tests.Integration.TestServerTest;
using Umbraco.Community.PageNotFoundTracker.Core.Configuration;
using Umbraco.Community.PageNotFoundTracker.Core.Tracking;
using Umbraco.Community.PageNotFoundTracker.Infrastructure.Tracking;
using Umbraco.Extensions;

namespace Umbraco.Community.PageNotFoundTracker.Integration.Tests.Tracking;

/// <summary>
/// Tests tracking end to end on the test server: the real recorder queues 404 requests for a page and a file, a
/// forced flush writes them through the package's EF Core context, and Umbraco's own database (read with NPoco) holds
/// the rows. The site has a home page but no 404 page, so the 404s are tracked without a resolved page, and reading the
/// rows through Umbraco proves that EF Core and Umbraco use the same SQLite file.
/// </summary>
[TestFixture]
[UmbracoTest(Database = UmbracoTestOptions.Database.NewSchemaPerTest, Boot = true)]
internal sealed class TrackingEndToEndTests : UmbracoTestServerTestBase
{
    private static readonly TimeSpan ReadyTimeout = TimeSpan.FromSeconds(60);

    /// <inheritdoc />
    public override void Setup()
    {
        base.Setup();
        this.CreateHomePage();
    }

    /// <summary>
    /// Tests that two spellings of a missing page and a missing file end up as two rows in Umbraco's database.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task Missing_Page_And_File_Are_Written_To_Umbraco_Database()
    {
        using (var timeout = new CancellationTokenSource(ReadyTimeout))
        {
            bool ready = await this.GetRequiredService<ITrackingPersistenceState>().WaitUntilReadyAsync(timeout.Token);
            Assert.That(ready, Is.True, "The tracking persistence must become available.");
        }

        foreach (string path in new[] { "/does-not-exist", "/Does-Not-Exist/", "/missing.pdf" })
        {
            using HttpResponseMessage response = await this.Client.GetAsync(new Uri(path, UriKind.Relative));
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound), path);
        }

        await this.GetRequiredService<ITrackingFlusher>().FlushAsync(CancellationToken.None);

        List<TrackedUrlRow> rows;
        using (IScope scope = this.GetRequiredService<IScopeProvider>().CreateScope(autoComplete: true))
        {
            rows = scope.Database.Fetch<TrackedUrlRow>(
                $"SELECT Url, HitCount, IsFile FROM {TrackingConstants.TableName} ORDER BY Url");
        }

        Assert.That(rows.Select(r => r.Url), Is.EqualTo(new[] { "/does-not-exist", "/missing.pdf" }));
        Assert.Multiple(() =>
        {
            Assert.That(rows[0].HitCount, Is.EqualTo(2));
            Assert.That(rows[0].IsFile, Is.False);
            Assert.That(rows[1].HitCount, Is.EqualTo(1));
            Assert.That(rows[1].IsFile, Is.True);
        });
    }

    /// <inheritdoc />
    protected override void CustomTestSetup(IUmbracoBuilder builder)
    {
        // Only the forced flush writes during the test.
        builder.Services.Configure<PageNotFoundTrackerSettings>(settings => settings.FlushInterval = TimeSpan.FromHours(1));

        // The file 404 middleware, placed where UsePageNotFoundTracker() puts it on a real site.
        builder.Services.AddTransient<IStartupFilter, PageNotFoundTrackerStartupFilter>();

        // Publishing must refresh the caches that front-end routing reads (see LocalServerMessenger).
        builder.Services.AddUnique<IServerMessenger, LocalServerMessenger>();
        builder.AddNotificationHandler<ContentTreeChangeNotification, ContentTreeChangeDistributedCacheNotificationHandler>();
        builder.AddNotificationHandler<ContentTypeChangedNotification, ContentTypeChangedDistributedCacheNotificationHandler>();
    }

    /// <inheritdoc />
    protected override void CustomMvcSetup(IMvcBuilder mvcBuilder)
    {
        // Umbraco's default 404 view is a compiled view in this assembly; the test server does not register it itself.
        mvcBuilder.AddApplicationPart(Assembly.Load("Umbraco.Cms.StaticAssets"));
    }

    /// <summary>
    /// Publishes a home page, so the site has content and a missing path reaches the last-chance finder. There is no
    /// 404 page.
    /// </summary>
    private void CreateHomePage()
    {
        var contentType = new ContentTypeBuilder().WithAlias("trackingHome").WithName("Tracking home").Build();
        Assert.That(
            this.GetRequiredService<IContentTypeService>().CreateAsync(contentType, Constants.Security.SuperUserKey).GetAwaiter().GetResult().Success,
            Is.True);

        var contentService = this.GetRequiredService<IContentService>();
        var home = new ContentBuilder().WithContentType(contentType).WithName("Home").WithParentId(Constants.System.Root).Build();
        Assert.Multiple(() =>
        {
            Assert.That(contentService.Save(home).Success, Is.True);
            Assert.That(contentService.Publish(home, ["*"]).Success, Is.True);
        });
    }

    /// <summary>
    /// A tracked URL row as read through Umbraco's database.
    /// </summary>
    [TableName(TrackingConstants.TableName)]
    private sealed class TrackedUrlRow
    {
        /// <summary>
        /// Gets or sets the normalised URL.
        /// </summary>
        [Column("Url")]
        public string Url { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the hit count.
        /// </summary>
        [Column("HitCount")]
        public int HitCount { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the URL is a file.
        /// </summary>
        [Column("IsFile")]
        public bool IsFile { get; set; }
    }
}
