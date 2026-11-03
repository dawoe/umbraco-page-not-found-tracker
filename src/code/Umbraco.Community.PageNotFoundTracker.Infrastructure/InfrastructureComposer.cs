// Copyright (c) Dave Woestenborghs and contributors. Licensed under the MIT License. See LICENSE in the project root for license information.

using Umbraco.Cms.Core.Composing;
using Umbraco.Cms.Core.DependencyInjection;
using Umbraco.Community.PageNotFoundTracker.Core;
using Umbraco.Community.PageNotFoundTracker.Infrastructure.Extensions;

namespace Umbraco.Community.PageNotFoundTracker.Infrastructure;

/// <summary>
/// Composer for the Page Not Found Tracker infrastructure. Runs before <see cref="PageNotFoundTrackerComposer"/>, so a
/// consumer composer that runs after that one replaces these registrations.
/// </summary>
[ComposeBefore(typeof(PageNotFoundTrackerComposer))]
internal sealed class InfrastructureComposer : IComposer
{
    /// <inheritdoc />
    public void Compose(IUmbracoBuilder builder) => builder.AddPageNotFoundTracker();
}
