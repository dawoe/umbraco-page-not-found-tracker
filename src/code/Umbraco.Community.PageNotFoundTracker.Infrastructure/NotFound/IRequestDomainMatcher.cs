// Copyright (c) Dave Woestenborghs and contributors. Licensed under the MIT License. See LICENSE in the project root for license information.

using Umbraco.Cms.Core.Routing;
using Umbraco.Community.PageNotFoundTracker.Core.NotFound;

namespace Umbraco.Community.PageNotFoundTracker.Infrastructure.NotFound;

/// <summary>
/// Finds the Umbraco domain that serves a request, for requests without a published request (files).
/// </summary>
internal interface IRequestDomainMatcher
{
    /// <summary>
    /// Finds the domain matching the request.
    /// </summary>
    /// <param name="context">The request that ended in a 404.</param>
    /// <returns>The matching domain, or <c>null</c> when no domain matches.</returns>
    Domain? Match(NotFoundRequestContext context);
}
