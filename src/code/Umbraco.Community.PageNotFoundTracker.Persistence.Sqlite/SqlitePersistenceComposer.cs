// Copyright (c) Dave Woestenborghs and contributors. Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.DependencyInjection;
using Umbraco.Cms.Core.Composing;
using Umbraco.Cms.Core.DependencyInjection;
using Umbraco.Community.PageNotFoundTracker.Core.Tracking;

namespace Umbraco.Community.PageNotFoundTracker.Persistence.Sqlite;

/// <summary>
/// Registers the SQLite tracking database provider. It is added next to other providers (not TryAdd), so a provider
/// registered later for the same name replaces it. To replace it with your own provider for the same name, register yours
/// in a composer marked <c>[ComposeAfter(typeof(SqlitePersistenceComposer))]</c>, so it is registered later and wins.
/// </summary>
public sealed class SqlitePersistenceComposer : IComposer
{
    /// <inheritdoc />
    public void Compose(IUmbracoBuilder builder) => builder.Services.AddSingleton<ITrackingDatabaseProvider, SqliteTrackingDatabaseProvider>();
}
