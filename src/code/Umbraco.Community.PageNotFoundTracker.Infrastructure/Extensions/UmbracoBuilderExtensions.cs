// Copyright (c) Dave Woestenborghs and contributors. Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Umbraco.Cms.Core.DependencyInjection;
using Umbraco.Community.PageNotFoundTracker.Core;
using Umbraco.Community.PageNotFoundTracker.Core.Configuration;
using Umbraco.Community.PageNotFoundTracker.Core.Dashboard;
using Umbraco.Community.PageNotFoundTracker.Core.NotFound;
using Umbraco.Community.PageNotFoundTracker.Core.Redirects;
using Umbraco.Community.PageNotFoundTracker.Core.Tracking;
using Umbraco.Community.PageNotFoundTracker.Infrastructure.Dashboard;
using Umbraco.Community.PageNotFoundTracker.Infrastructure.NotFound;
using Umbraco.Community.PageNotFoundTracker.Infrastructure.Redirects;
using Umbraco.Community.PageNotFoundTracker.Infrastructure.Tracking;
using Umbraco.Extensions;

namespace Umbraco.Community.PageNotFoundTracker.Infrastructure.Extensions;

/// <summary>
/// Extension methods for <see cref="IUmbracoBuilder"/>.
/// </summary>
internal static class UmbracoBuilderExtensions
{
    /// <summary>
    /// Registers the Page Not Found Tracker services with the Umbraco builder.
    /// </summary>
    /// <param name="builder">A <see cref="IUmbracoBuilder"/>.</param>
    /// <returns>Updated <see cref="IUmbracoBuilder"/>.</returns>
    /// <remarks>
    /// Replaceable services are registered with <c>AddUnique</c>, so the last registration wins. The package composers
    /// run before <see cref="PageNotFoundTrackerComposer"/>; a consumer replaces a service from a composer marked
    /// <c>[ComposeAfter(typeof(PageNotFoundTrackerComposer))]</c>.
    /// </remarks>
    public static IUmbracoBuilder AddPageNotFoundTracker(this IUmbracoBuilder builder)
    {
        builder.Services.AddOptions<PageNotFoundTrackerSettings>()
            .Bind(builder.Config.GetSection(Constants.SectionName));

        builder.Services.AddUnique<INotFoundPageResolver, DefaultNotFoundPageResolver>();
        builder.Services.AddUnique<IRequestDomainMatcher, RequestDomainMatcher>();

        builder.Services.AddUnique<IDashboardAccess, DashboardAccess>();

        builder.Services.AddUnique<ITrackedUrlQueryService, TrackedUrlQueryService>();
        builder.Services.AddUnique<ITrackedUrlRemover, TrackedUrlRemover>();
        builder.Services.AddUnique<ITrackedUrlRedirectService, TrackedUrlRedirectService>();
        builder.Services.AddUnique<IRedirectProvider, UmbracoRedirectProvider>();

        // TimeProvider is a shared framework service, not ours: keep a registration the site already made.
        builder.Services.TryAddSingleton(TimeProvider.System);
        builder.Services.AddSingleton<TrackingQueue>();
        builder.Services.AddSingleton<TrackingPersistenceState>();
        builder.Services.AddSingleton<ITrackingPersistenceState>(sp => sp.GetRequiredService<TrackingPersistenceState>());
        builder.Services.AddUnique<INotFoundRecorder, NotFoundTracker>();
        AddTrackingPersistence(builder.Services);

        // Replaces Umbraco's ContentFinderByConfigured404 as the last-chance finder. Ours asks the resolver first and
        // delegates to Umbraco's finder when nothing resolves, so the built-in behaviour is kept, not removed.
        builder.SetContentLastChanceFinder<PageNotFoundLastChanceFinder>();

        return builder;
    }

    /// <summary>
    /// Registers the tracking database context, the provider selection and the flush service.
    /// </summary>
    /// <param name="services">The service collection.</param>
    private static void AddTrackingPersistence(IServiceCollection services)
    {
        // Database providers are registered with Add (not TryAdd), so several can exist; the selector picks the last
        // registered one for Umbraco's provider name.
        services.AddSingleton<TrackingDatabaseProviderSelector>();
        services.AddSingleton<DefaultTrackedUrlUpsert>();

        // The options action runs when the pooled factory is first resolved, which only the flush does, after the
        // gate opened. With no matching provider the options stay unconfigured.
        services.AddUmbracoDbContext<PageNotFoundTrackerDbContext>(
            (options, connectionString, providerName, serviceProvider) =>
            {
                if (serviceProvider is null || string.IsNullOrEmpty(connectionString))
                {
                    return;
                }

                serviceProvider.GetRequiredService<TrackingDatabaseProviderSelector>()
                    .Select(providerName)?
                    .ConfigureDbContext(options, connectionString);
            },
            shareUmbracoConnection: true);

        services.AddHostedService<TrackingMigrationService>();
        services.AddSingleton<TrackingFlushService>();
        services.AddHostedService(sp => sp.GetRequiredService<TrackingFlushService>());
        services.AddSingleton<ITrackingFlusher>(sp => sp.GetRequiredService<TrackingFlushService>());
    }
}
