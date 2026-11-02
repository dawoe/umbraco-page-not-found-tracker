// Copyright (c) Dave Woestenborghs and contributors. Licensed under the MIT License. See LICENSE in the project root for license information.

namespace Umbraco.Community.PageNotFoundTracker.Core.NotFound;

/// <summary>
/// Describes a request that ended in a 404.
/// </summary>
/// <param name="PathAndQuery">The original request URL: path followed by the query string, if any.</param>
/// <param name="Host">The host name of the request.</param>
/// <param name="Port">The port of the request, or <c>null</c> when the host header carried none.</param>
/// <param name="IsFile">Whether the last segment of the request path has a file extension.</param>
public sealed record NotFoundRequestContext(string PathAndQuery, string Host, int? Port, bool IsFile)
{
    /// <summary>
    /// Gets the request path without the query string.
    /// </summary>
    public string Path
    {
        get
        {
            var index = this.PathAndQuery.IndexOf('?', StringComparison.Ordinal);
            return index < 0 ? this.PathAndQuery : this.PathAndQuery[..index];
        }
    }
}
