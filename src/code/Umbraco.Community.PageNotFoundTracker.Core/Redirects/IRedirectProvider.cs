// Copyright (c) Dave Woestenborghs and contributors. Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Text.Json;
using Umbraco.Community.PageNotFoundTracker.Core.Tracking;

namespace Umbraco.Community.PageNotFoundTracker.Core.Redirects;

/// <summary>
/// Creates a redirect for a tracked URL. Replaceable: register another implementation after the package composer.
/// </summary>
public interface IRedirectProvider
{
    /// <summary>
    /// Determines whether this provider can create a redirect for the tracked URL.
    /// </summary>
    /// <param name="url">The tracked URL.</param>
    /// <returns><c>true</c> when a redirect can be created.</returns>
    bool CanHandle(TrackedUrl url);

    /// <summary>
    /// Creates the redirect.
    /// </summary>
    /// <param name="url">The tracked URL to redirect.</param>
    /// <param name="payload">Provider specific data sent by the dashboard, or <c>null</c>.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result of the creation.</returns>
    Task<RedirectResult> CreateAsync(TrackedUrl url, JsonElement? payload, CancellationToken cancellationToken);
}
