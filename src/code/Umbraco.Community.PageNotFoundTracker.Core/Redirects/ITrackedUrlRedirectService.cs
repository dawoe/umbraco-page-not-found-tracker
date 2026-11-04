// Copyright (c) Dave Woestenborghs and contributors. Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Text.Json;

namespace Umbraco.Community.PageNotFoundTracker.Core.Redirects;

/// <summary>
/// Creates a redirect for a tracked URL and removes the tracked URL afterwards. Replaceable.
/// </summary>
public interface ITrackedUrlRedirectService
{
    /// <summary>
    /// Creates a redirect for the tracked URL with the given id.
    /// </summary>
    /// <param name="trackedUrlId">The id of the tracked URL.</param>
    /// <param name="payload">Provider specific data, or <c>null</c>.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The outcome.</returns>
    Task<CreateRedirectOutcome> CreateAsync(int trackedUrlId, JsonElement? payload, CancellationToken cancellationToken);
}
