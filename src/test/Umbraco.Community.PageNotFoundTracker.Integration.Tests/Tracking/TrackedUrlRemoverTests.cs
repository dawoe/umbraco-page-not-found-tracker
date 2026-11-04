// Copyright (c) Dave Woestenborghs and contributors. Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Umbraco.Cms.Core.Configuration.Models;
using Umbraco.Community.PageNotFoundTracker.Core.Configuration;
using Umbraco.Community.PageNotFoundTracker.Core.Tracking;
using Umbraco.Community.PageNotFoundTracker.Infrastructure.Tracking;

namespace Umbraco.Community.PageNotFoundTracker.Integration.Tests.Tracking;

/// <summary>
/// Tests deleting a tracked URL against a real database (SQLite, and SQL Server when configured).
/// </summary>
internal sealed class TrackedUrlRemoverTests : TrackingDatabaseFixture
{
    private static readonly DateTime Seen = new(2026, 11, 4, 10, 0, 0, DateTimeKind.Utc);

    private TrackingQueue queue = null!;
    private TrackingFlushService flushService = null!;
    private TrackedUrlRemover remover = null!;
    private TrackedUrlQueryService queryService = null!;

    /// <summary>
    /// Initializes a new instance of the <see cref="TrackedUrlRemoverTests"/> class.
    /// </summary>
    /// <param name="database">The database to run on.</param>
    public TrackedUrlRemoverTests(TrackingDatabase database)
        : base(database)
    {
    }

    /// <summary>
    /// Builds the queue, flush service, remover and query service on the fixture's database.
    /// </summary>
    [SetUp]
    public void SetUp()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IDbContextFactory<PageNotFoundTrackerDbContext>>(new TestContextFactory(this));
        IServiceProvider sp = services.BuildServiceProvider();

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
        this.remover = new TrackedUrlRemover(this.queue, this.flushService, sp);
        this.queryService = new TrackedUrlQueryService(sp);
    }

    /// <summary>
    /// Disposes the flush service.
    /// </summary>
    [TearDown]
    public void TearDown() => this.flushService.Dispose();

    /// <summary>
    /// A queued hit does not bring a deleted row back.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task Delete_removes_row_and_a_queued_hit_does_not_bring_it_back()
    {
        TrackedUrlHit hit = Hit("/remover-a");
        await this.UpsertAsync(hit);
        TrackedUrl row = (await this.GetRowsAsync(hit.Url)).Single();
        this.queue.TryEnqueue(hit);

        bool deleted = await this.remover.DeleteAsync(row, CancellationToken.None);
        await this.flushService.FlushAsync(CancellationToken.None);

        List<TrackedUrl> after = await this.GetRowsAsync(hit.Url);
        Assert.Multiple(() =>
        {
            Assert.That(deleted, Is.True);
            Assert.That(after, Is.Empty);
        });
    }

    /// <summary>
    /// Delete waits for a flush batch in progress.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task Delete_waits_for_a_flush_in_progress()
    {
        TrackedUrlHit hit = Hit("/remover-b");
        await this.UpsertAsync(hit);
        TrackedUrl row = (await this.GetRowsAsync(hit.Url)).Single();

        var entered = new TaskCompletionSource();
        var release = new TaskCompletionSource();
        Task holder = this.flushService.RunExclusiveAsync(
            async () =>
            {
                entered.SetResult();
                await release.Task;
            },
            CancellationToken.None);
        await entered.Task;

        Task<bool> delete = this.remover.DeleteAsync(row, CancellationToken.None);
        await Task.Delay(300);
        Assert.That(delete.IsCompleted, Is.False);

        release.SetResult();
        await holder;
        Assert.That(await delete.WaitAsync(TimeSpan.FromSeconds(10)), Is.True);
    }

    /// <summary>
    /// An unknown id gives null.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task GetByIdAsync_returns_null_for_unknown_id() =>
        Assert.That(await this.queryService.GetByIdAsync(int.MaxValue, CancellationToken.None), Is.Null);

    private static TrackedUrlHit Hit(string url) =>
        new(url, "hash" + url, url, url, string.Empty, string.Empty, false, 1, Seen, Seen);

    private async Task UpsertAsync(TrackedUrlHit hit)
    {
        await using PageNotFoundTrackerDbContext dbContext = this.CreateContext();
        await new DefaultTrackedUrlUpsert(NullLogger<DefaultTrackedUrlUpsert>.Instance).UpsertAsync(dbContext, [hit], CancellationToken.None);
    }

    private async Task<List<TrackedUrl>> GetRowsAsync(string url)
    {
        await using PageNotFoundTrackerDbContext dbContext = this.CreateContext();
        return await dbContext.TrackedUrls.AsNoTracking().Where(r => r.Url == url).ToListAsync();
    }

    private sealed class TestContextFactory(TrackedUrlRemoverTests owner) : IDbContextFactory<PageNotFoundTrackerDbContext>
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
