// Copyright (c) Dave Woestenborghs and contributors. Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Umbraco.Cms.Core.DependencyInjection;
using Umbraco.Community.PageNotFoundTracker.Core;
using Umbraco.Community.PageNotFoundTracker.Core.Configuration;
using Umbraco.Community.PageNotFoundTracker.Core.NotFound;
using Umbraco.Community.PageNotFoundTracker.Core.Tracking;
using Umbraco.Community.PageNotFoundTracker.Infrastructure.NotFound;
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
    /// Services are registered with <c>TryAdd</c>, so a registration made by the consumer wins regardless of order.
    /// </remarks>
    public static IUmbracoBuilder AddPageNotFoundTracker(this IUmbracoBuilder builder)
    {
        builder.Services.AddOptions<PageNotFoundTrackerSettings>()
            .Bind(builder.Config.GetSection(Constants.SectionName));

        builder.Services.TryAddSingleton<INotFoundPageResolver, DefaultNotFoundPageResolver>();
        builder.Services.TryAddSingleton<IRequestDomainMatcher, RequestDomainMatcher>();
        builder.Services.TryAddSingleton(TimeProvider.System);
        builder.Services.AddSingleton<TrackingQueue>();
        builder.Services.AddSingleton<TrackingPersistenceState>();
        builder.Services.AddSingleton<ITrackingPersistenceState>(sp => sp.GetRequiredService<TrackingPersistenceState>());
        builder.Services.TryAddSingleton<INotFoundRecorder, NotFoundTracker>();

        // Replaces Umbraco's ContentFinderByConfigured404 as the last-chance finder. Ours asks the resolver first and
        // delegates to Umbraco's finder when nothing resolves, so the built-in behaviour is kept, not removed.
        builder.SetContentLastChanceFinder<PageNotFoundLastChanceFinder>();

        return builder;
    }
}
