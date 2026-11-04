// Copyright (c) Dave Woestenborghs and contributors. Licensed under the MIT License. See LICENSE in the project root for license information.

using Umbraco.Cms.Core.Composing;
using Umbraco.Cms.Core.DependencyInjection;
using Umbraco.Community.PageNotFoundTracker.BackOffice.Extensions;
using Umbraco.Community.PageNotFoundTracker.Core;

namespace Umbraco.Community.PageNotFoundTracker.BackOffice;

/// <summary>
/// Composer for the Page Not Found Tracker back office: registers the Management API (OpenAPI document, operation IDs,
/// dashboard access policy). Runs before <see cref="PageNotFoundTrackerComposer"/>, like every package composer.
/// </summary>
[ComposeBefore(typeof(PageNotFoundTrackerComposer))]
internal sealed class BackOfficeComposer : IComposer
{
    /// <inheritdoc />
    public void Compose(IUmbracoBuilder builder) => builder.AddPageNotFoundTrackerManagementApi();
}
