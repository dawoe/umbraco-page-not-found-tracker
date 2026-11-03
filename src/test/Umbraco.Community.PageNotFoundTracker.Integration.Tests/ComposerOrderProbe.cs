// Copyright (c) Dave Woestenborghs and contributors. Licensed under the MIT License. See LICENSE in the project root for license information.

using Umbraco.Community.PageNotFoundTracker.Core;
using Umbraco.Community.PageNotFoundTracker.Core.NotFound;
using Umbraco.Community.PageNotFoundTracker.Core.Tracking;

namespace Umbraco.Community.PageNotFoundTracker.Integration.Tests;

/// <summary>
/// What the package had registered when a composer that runs after <see cref="PageNotFoundTrackerComposer"/> ran.
/// </summary>
/// <param name="ResolverRegistered">Whether <see cref="INotFoundPageResolver"/> was registered.</param>
/// <param name="ProviderCount">The number of registered <see cref="ITrackingDatabaseProvider"/>s.</param>
internal sealed record ComposerOrderProbe(bool ResolverRegistered, int ProviderCount);
