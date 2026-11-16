// Copyright (c) Dave Woestenborghs and contributors. Licensed under the MIT License. See LICENSE in the project root for license information.

using Umbraco.Cms.Core.Composing;
using Umbraco.Cms.Core.DependencyInjection;
using Umbraco.Community.PageNotFoundTracker.Core;
using Umbraco.Community.PageNotFoundTracker.Core.Tracking;
using Umbraco.Extensions;

namespace Umbraco.Community.PageNotFoundTracker.Samples;

// docs:begin exporter-composer

/// <summary>
/// Registers the sample exporter after the package, so it replaces the default.
/// </summary>
[ComposeAfter(typeof(PageNotFoundTrackerComposer))]
public sealed class TrackedUrlExporterSampleComposer : IComposer
{
    /// <inheritdoc />
    public void Compose(IUmbracoBuilder builder) =>
        builder.Services.AddUnique<ITrackedUrlExporter, TrackedUrlExporterSample>();
}

// docs:end exporter-composer
