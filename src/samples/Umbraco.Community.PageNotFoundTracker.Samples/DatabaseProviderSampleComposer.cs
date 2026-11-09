// Copyright (c) Dave Woestenborghs and contributors. Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.DependencyInjection;
using Umbraco.Cms.Core.Composing;
using Umbraco.Cms.Core.DependencyInjection;
using Umbraco.Community.PageNotFoundTracker.Core;
using Umbraco.Community.PageNotFoundTracker.Core.Tracking;

namespace Umbraco.Community.PageNotFoundTracker.Samples;

// docs:begin db-provider-composer

/// <summary>
/// Registers the sample provider after the package. Providers are added next to the built-in ones; the last
/// registration for a provider name wins.
/// </summary>
[ComposeAfter(typeof(PageNotFoundTrackerComposer))]
public sealed class DatabaseProviderSampleComposer : IComposer
{
    /// <inheritdoc />
    public void Compose(IUmbracoBuilder builder) =>
        builder.Services.AddSingleton<ITrackingDatabaseProvider, DatabaseProviderSample>();
}

// docs:end db-provider-composer
