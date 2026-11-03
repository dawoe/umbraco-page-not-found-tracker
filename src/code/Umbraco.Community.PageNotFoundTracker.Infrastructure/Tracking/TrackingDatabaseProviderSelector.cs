// Copyright (c) Dave Woestenborghs and contributors. Licensed under the MIT License. See LICENSE in the project root for license information.

using Umbraco.Community.PageNotFoundTracker.Core.Tracking;

namespace Umbraco.Community.PageNotFoundTracker.Infrastructure.Tracking;

/// <summary>
/// Picks the <see cref="ITrackingDatabaseProvider"/> for Umbraco's database provider name.
/// </summary>
/// <param name="providers">The registered providers, in registration order.</param>
internal sealed class TrackingDatabaseProviderSelector(IEnumerable<ITrackingDatabaseProvider> providers)
{
    private readonly ITrackingDatabaseProvider[] providers = [.. providers];

    /// <summary>
    /// Selects the provider for a provider name. The match ignores case; with several matches the last registered
    /// wins, so a custom provider registered after the built-in one replaces it.
    /// </summary>
    /// <param name="providerName">The name of Umbraco's database provider.</param>
    /// <returns>The provider, or <c>null</c> when none serves the name.</returns>
    public ITrackingDatabaseProvider? Select(string? providerName)
    {
        if (string.IsNullOrEmpty(providerName))
        {
            return null;
        }

        return this.providers.LastOrDefault(p => string.Equals(p.ProviderName, providerName, StringComparison.OrdinalIgnoreCase));
    }
}
