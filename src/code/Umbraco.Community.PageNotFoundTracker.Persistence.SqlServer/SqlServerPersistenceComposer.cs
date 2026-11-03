// Copyright (c) Dave Woestenborghs and contributors. Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.DependencyInjection;
using Umbraco.Cms.Core.Composing;
using Umbraco.Cms.Core.DependencyInjection;
using Umbraco.Community.PageNotFoundTracker.Core;
using Umbraco.Community.PageNotFoundTracker.Core.Tracking;

namespace Umbraco.Community.PageNotFoundTracker.Persistence.SqlServer;

/// <summary>
/// Registers the SQL Server tracking database provider. It is added next to other providers (not TryAdd), so a
/// provider registered later for the same name replaces it. Runs before <see cref="PageNotFoundTrackerComposer"/>, so a
/// provider registered from a composer marked <c>[ComposeAfter(typeof(PageNotFoundTrackerComposer))]</c> wins.
/// </summary>
[ComposeBefore(typeof(PageNotFoundTrackerComposer))]
internal sealed class SqlServerPersistenceComposer : IComposer
{
    /// <inheritdoc />
    public void Compose(IUmbracoBuilder builder) => builder.Services.AddSingleton<ITrackingDatabaseProvider, SqlServerTrackingDatabaseProvider>();
}
