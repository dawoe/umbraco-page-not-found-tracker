// Copyright (c) Dave Woestenborghs and contributors. Licensed under the MIT License. See LICENSE in the project root for license information.

using Umbraco.Cms.Core.Routing;

namespace Umbraco.Community.PageNotFoundTracker.Infrastructure.NotFound;

/// <summary>
/// A request that ended in a 404, handed to the <see cref="INotFoundRecorder"/>.
/// </summary>
/// <param name="PathAndQuery">The original request URL: path followed by the query string, if any.</param>
/// <param name="Domain">The Umbraco domain that matched the request, or <c>null</c> when none matched.</param>
/// <param name="Culture">The culture of the matched domain, or <c>null</c> when no domain matched or it has no culture.</param>
/// <param name="IsFile">Whether the request was for a file (the last path segment has an extension).</param>
/// <param name="PageResolved">Whether a 404 page was resolved for the request.</param>
internal sealed record NotFoundOccurrence(string PathAndQuery, Domain? Domain, string? Culture, bool IsFile, bool PageResolved);
