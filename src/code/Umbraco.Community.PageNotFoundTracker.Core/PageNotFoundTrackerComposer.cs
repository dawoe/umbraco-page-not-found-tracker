// Copyright (c) Dave Woestenborghs and contributors. Licensed under the MIT License. See LICENSE in the project root for license information.

using Umbraco.Cms.Core.Composing;
using Umbraco.Cms.Core.DependencyInjection;

namespace Umbraco.Community.PageNotFoundTracker.Core;

/// <summary>
/// The composer that marks the end of the Page Not Found Tracker registrations. It registers nothing itself: every
/// composer of the package is marked <c>[ComposeBefore(typeof(PageNotFoundTrackerComposer))]</c>, so all package
/// registrations are made before it runs. To replace a package service (for example the 404 page resolver or a tracking
/// database provider), register your own in a composer marked <c>[ComposeAfter(typeof(PageNotFoundTrackerComposer))]</c>.
/// </summary>
public sealed class PageNotFoundTrackerComposer : IComposer
{
    /// <inheritdoc />
    public void Compose(IUmbracoBuilder builder)
    {
    }
}
