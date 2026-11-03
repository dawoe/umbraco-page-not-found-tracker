// Copyright (c) Dave Woestenborghs and contributors. Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.DependencyInjection;
using Umbraco.Cms.Core.Composing;
using Umbraco.Cms.Core.DependencyInjection;
using Umbraco.Community.PageNotFoundTracker.Core;
using Umbraco.Community.PageNotFoundTracker.Core.NotFound;
using Umbraco.Community.PageNotFoundTracker.Core.Tracking;

namespace Umbraco.Community.PageNotFoundTracker.Integration.Tests;

/// <summary>
/// A consumer-style composer that runs after <see cref="PageNotFoundTrackerComposer"/> and records what the package had
/// registered by then. It only adds <see cref="ComposerOrderProbe"/>, so it does not affect other tests.
/// </summary>
[ComposeAfter(typeof(PageNotFoundTrackerComposer))]
internal sealed class ComposerOrderProbeComposer : IComposer
{
    /// <inheritdoc />
    public void Compose(IUmbracoBuilder builder)
    {
        var resolverRegistered = builder.Services.Any(descriptor => descriptor.ServiceType == typeof(INotFoundPageResolver));
        var providerCount = builder.Services.Count(descriptor => descriptor.ServiceType == typeof(ITrackingDatabaseProvider));
        builder.Services.AddSingleton(new ComposerOrderProbe(resolverRegistered, providerCount));
    }
}
