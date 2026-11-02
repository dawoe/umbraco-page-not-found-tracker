// Copyright (c) Dave Woestenborghs and contributors. Licensed under the MIT License. See LICENSE in the project root for license information.

using Umbraco.Cms.Core.Routing;

namespace Umbraco.Community.PageNotFoundTracker.Core.NotFound;

/// <summary>
/// Chooses the page that answers a 404. Replace the registration to change how the page is chosen.
/// </summary>
public interface INotFoundPageResolver
{
    /// <summary>
    /// Resolves the 404 page for a request.
    /// </summary>
    /// <param name="context">The request that ended in a 404.</param>
    /// <param name="domain">The Umbraco domain already matched by the caller, or <c>null</c> when no domain matched. The resolver does not match domains itself.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The 404 page, or <c>null</c> when none is found, in which case Umbraco's default 404 behaviour applies.</returns>
    Task<NotFoundPage?> ResolveAsync(NotFoundRequestContext context, Domain? domain, CancellationToken cancellationToken);
}
