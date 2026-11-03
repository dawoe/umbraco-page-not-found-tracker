// Copyright (c) Dave Woestenborghs and contributors. Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Configuration.Models;
using Umbraco.Cms.Core.Services;

namespace Umbraco.Community.PageNotFoundTracker.Infrastructure.Tracking;

/// <summary>
/// Migrates the tracking tables once Umbraco runs and opens the persistence gate. Migrations run regardless of the
/// <c>TrackingEnabled</c> setting.
/// </summary>
internal sealed class TrackingMigrationService : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(1);

    private readonly IRuntimeState runtimeState;
    private readonly TrackingPersistenceState persistenceState;
    private readonly TrackingDatabaseProviderSelector providerSelector;
    private readonly IOptionsMonitor<ConnectionStrings> connectionStrings;
    private readonly IServiceProvider serviceProvider;
    private readonly TimeProvider timeProvider;
    private readonly ILogger<TrackingMigrationService> logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="TrackingMigrationService"/> class.
    /// </summary>
    /// <param name="runtimeState">Umbraco's runtime state.</param>
    /// <param name="persistenceState">The persistence state (the gate).</param>
    /// <param name="providerSelector">The database provider selector.</param>
    /// <param name="connectionStrings">Umbraco's connection string options.</param>
    /// <param name="serviceProvider">The service provider; the context factory is resolved from it after a provider matched.</param>
    /// <param name="timeProvider">The time provider.</param>
    /// <param name="logger">The logger.</param>
    public TrackingMigrationService(
        IRuntimeState runtimeState,
        TrackingPersistenceState persistenceState,
        TrackingDatabaseProviderSelector providerSelector,
        IOptionsMonitor<ConnectionStrings> connectionStrings,
        IServiceProvider serviceProvider,
        TimeProvider timeProvider,
        ILogger<TrackingMigrationService> logger)
    {
        this.runtimeState = runtimeState;
        this.persistenceState = persistenceState;
        this.providerSelector = providerSelector;
        this.connectionStrings = connectionStrings;
        this.serviceProvider = serviceProvider;
        this.timeProvider = timeProvider;
        this.logger = logger;
    }

    /// <summary>
    /// Gets or sets the migration call. Defaults to <c>Database.MigrateAsync</c>; unit tests replace it.
    /// </summary>
    internal Func<PageNotFoundTrackerDbContext, CancellationToken, Task> MigrateAsync { get; set; } =
        (dbContext, cancellationToken) => dbContext.Database.MigrateAsync(cancellationToken);

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            if (this.runtimeState.Level != RuntimeLevel.Run)
            {
                using var timer = new PeriodicTimer(PollInterval, this.timeProvider);
                while (this.runtimeState.Level != RuntimeLevel.Run)
                {
                    if (!await timer.WaitForNextTickAsync(stoppingToken))
                    {
                        return;
                    }
                }
            }

            await this.MigrateOnceAsync(stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Stopping before the migration finished: the state stays pending.
        }
    }

    private async Task MigrateOnceAsync(CancellationToken cancellationToken)
    {
        try
        {
            string? providerName = this.connectionStrings.CurrentValue.ProviderName;
            if (this.providerSelector.Select(providerName) is null)
            {
                this.logger.LogError(
                    "Page Not Found Tracker: no database provider is registered for '{ProviderName}'. Tracking is disabled.",
                    providerName);
                this.persistenceState.SetUnavailable();
                return;
            }

            IDbContextFactory<PageNotFoundTrackerDbContext> factory =
                this.serviceProvider.GetRequiredService<IDbContextFactory<PageNotFoundTrackerDbContext>>();
            await using PageNotFoundTrackerDbContext dbContext = await factory.CreateDbContextAsync(cancellationToken);
            await this.MigrateAsync(dbContext, cancellationToken);
            this.persistenceState.SetAvailable();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "Page Not Found Tracker: migrating the tracking tables failed. Tracking is disabled.");
            this.persistenceState.SetUnavailable();
        }
    }
}
