// Copyright (c) Dave Woestenborghs and contributors. Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.PublishedCache;
using Umbraco.Cms.Core.Services;
using Umbraco.Community.PageNotFoundTracker.Core.Blocking;
using Umbraco.Community.PageNotFoundTracker.Core.Tracking;
using Umbraco.Community.PageNotFoundTracker.Infrastructure.NotFound;
using Umbraco.Community.PageNotFoundTracker.Infrastructure.Tracking;

namespace Umbraco.Community.PageNotFoundTracker.Infrastructure.Blocking;

/// <summary>
/// Holds the current <see cref="BlockSnapshot"/> (singleton). The snapshot reference is replaced atomically, so a reader
/// never sees a half-built rule set. Rebuilds are serialised; the initial load is started by
/// <see cref="BlockSnapshotService"/>, later ones by the invalidation handlers and the cache refresher.
/// </summary>
internal sealed class BlockSnapshotProvider : IBlockSnapshotProvider
{
    private readonly IServiceProvider serviceProvider;
    private readonly IDomainCache domainCache;
    private readonly IRuntimeState runtimeState;
    private readonly ITrackingPersistenceState persistenceState;
    private readonly TimeProvider timeProvider;
    private readonly ILogger<BlockSnapshotProvider> logger;
    private readonly BlockSnapshotTimings timings;
    private readonly SemaphoreSlim refreshLock = new(1, 1);

    // Completed after the first load attempt, successful or not; requests before that wait for it.
    private readonly TaskCompletionSource initialAttempt = new(TaskCreationOptions.RunContinuationsAsynchronously);

    private BlockSnapshot? current;
    private int passThroughWarned;

    /// <summary>
    /// Initializes a new instance of the <see cref="BlockSnapshotProvider"/> class.
    /// </summary>
    /// <param name="serviceProvider">The service provider; the context factory is resolved from it per load only.</param>
    /// <param name="domainCache">Umbraco's domain cache.</param>
    /// <param name="runtimeState">Umbraco's runtime state.</param>
    /// <param name="persistenceState">The tracking persistence state.</param>
    /// <param name="timeProvider">The time provider.</param>
    /// <param name="logger">The logger.</param>
    /// <param name="timings">The waits; <c>null</c> uses <see cref="BlockSnapshotTimings.Default"/>.</param>
    public BlockSnapshotProvider(
        IServiceProvider serviceProvider,
        IDomainCache domainCache,
        IRuntimeState runtimeState,
        ITrackingPersistenceState persistenceState,
        TimeProvider timeProvider,
        ILogger<BlockSnapshotProvider> logger,
        BlockSnapshotTimings? timings = null)
    {
        this.serviceProvider = serviceProvider;
        this.domainCache = domainCache;
        this.runtimeState = runtimeState;
        this.persistenceState = persistenceState;
        this.timeProvider = timeProvider;
        this.logger = logger;
        this.timings = timings ?? BlockSnapshotTimings.Default;
        this.LoadAsync = this.LoadFromDatabaseAsync;
    }

    /// <summary>
    /// Gets or sets the load call that builds a snapshot from the database and the domain cache; unit tests replace it.
    /// </summary>
    internal Func<CancellationToken, Task<BlockSnapshot>> LoadAsync { get; set; }

    /// <inheritdoc />
    public ValueTask<BlockSnapshot> GetAsync(CancellationToken cancellationToken)
    {
        BlockSnapshot? snapshot = Volatile.Read(ref this.current);
        if (snapshot is not null)
        {
            return new ValueTask<BlockSnapshot>(snapshot);
        }

        // Unavailable is final for the process, and below Run (install, upgrade) nothing is loaded yet: pass through at
        // once. A failed initial attempt also passes through at once; the service retries in the background.
        if (this.persistenceState.Status == TrackingPersistenceStatus.Unavailable
            || this.runtimeState.Level != RuntimeLevel.Run
            || this.initialAttempt.Task.IsCompleted)
        {
            // Re-read: a load may have published its snapshot between the first read and the completed-check above.
            return new ValueTask<BlockSnapshot>(Volatile.Read(ref this.current) ?? BlockSnapshot.Empty);
        }

        return this.WaitForInitialLoadAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<bool> RefreshAsync(CancellationToken cancellationToken)
    {
        if (this.persistenceState.Status != TrackingPersistenceStatus.Available)
        {
            // Pending: the startup service loads once the gate opens. Unavailable: there is nothing to load.
            return false;
        }

        await this.refreshLock.WaitAsync(cancellationToken);
        try
        {
            BlockSnapshot snapshot = await this.LoadAsync(cancellationToken);
            Volatile.Write(ref this.current, snapshot);
            this.initialAttempt.TrySetResult();
            return true;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            if (Volatile.Read(ref this.current) is null)
            {
                this.logger.LogError(
                    ex,
                    "Page Not Found Tracker: loading the block rules failed. Requests pass through unblocked until a load succeeds.");
            }
            else
            {
                this.logger.LogError(ex, "Page Not Found Tracker: rebuilding the block rules failed. The previous rules stay in use.");
            }

            this.initialAttempt.TrySetResult();
            return false;
        }
        finally
        {
            this.refreshLock.Release();
        }
    }

    private async ValueTask<BlockSnapshot> WaitForInitialLoadAsync(CancellationToken cancellationToken)
    {
        try
        {
            await this.initialAttempt.Task.WaitAsync(this.timings.InitialLoadWait, this.timeProvider, cancellationToken);
        }
        catch (TimeoutException)
        {
            if (Interlocked.Exchange(ref this.passThroughWarned, 1) == 0)
            {
                this.logger.LogWarning(
                    "Page Not Found Tracker: the block rules were not loaded within {Wait}. Requests pass through unblocked until they are.",
                    this.timings.InitialLoadWait);
            }

            return Volatile.Read(ref this.current) ?? BlockSnapshot.Empty;
        }

        return Volatile.Read(ref this.current) ?? BlockSnapshot.Empty;
    }

    private async Task<BlockSnapshot> LoadFromDatabaseAsync(CancellationToken cancellationToken)
    {
        // Resolved per load: the pooled factory runs its options action on first resolve, which must happen after the
        // persistence gate opened.
        IDbContextFactory<PageNotFoundTrackerDbContext> factory =
            this.serviceProvider.GetRequiredService<IDbContextFactory<PageNotFoundTrackerDbContext>>();
        await using PageNotFoundTrackerDbContext dbContext = await factory.CreateDbContextAsync(cancellationToken);
        List<BlockRule> rules = await dbContext.BlockRules.AsNoTracking().ToListAsync(cancellationToken);
        IReadOnlyList<DomainEntry> domains = DomainSelector.FromCache(this.domainCache.GetAll(false));
        return BlockSnapshot.Build(rules, domains);
    }
}
