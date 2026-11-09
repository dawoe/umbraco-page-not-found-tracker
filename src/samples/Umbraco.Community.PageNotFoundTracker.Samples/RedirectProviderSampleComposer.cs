// Copyright (c) Dave Woestenborghs and contributors. Licensed under the MIT License. See LICENSE in the project root for license information.

using Umbraco.Cms.Core.Composing;
using Umbraco.Cms.Core.DependencyInjection;
using Umbraco.Community.PageNotFoundTracker.Core;
using Umbraco.Community.PageNotFoundTracker.Core.Redirects;
using Umbraco.Extensions;

namespace Umbraco.Community.PageNotFoundTracker.Samples;

// docs:begin redirect-provider-composer

/// <summary>
/// Registers the sample redirect provider after the package, so it replaces the default.
/// </summary>
[ComposeAfter(typeof(PageNotFoundTrackerComposer))]
public sealed class RedirectProviderSampleComposer : IComposer
{
    /// <inheritdoc />
    public void Compose(IUmbracoBuilder builder) =>
        builder.Services.AddUnique<IRedirectProvider, RedirectProviderSample>();
}

// docs:end redirect-provider-composer
