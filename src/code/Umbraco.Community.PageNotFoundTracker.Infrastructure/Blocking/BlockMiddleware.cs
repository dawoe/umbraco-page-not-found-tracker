// Copyright (c) Dave Woestenborghs and contributors. Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.AspNetCore.Http;
using Umbraco.Community.PageNotFoundTracker.Core.NotFound;
using Umbraco.Community.PageNotFoundTracker.Infrastructure.Tracking;

namespace Umbraco.Community.PageNotFoundTracker.Infrastructure.Blocking;

/// <summary>
/// Answers requests that match a block rule with the rule's status code and an empty body, ahead of static files,
/// redirects and routing. It never records the request. Umbraco-owned paths always pass through.
/// </summary>
internal sealed class BlockMiddleware
{
    private readonly RequestDelegate next;

    /// <summary>
    /// Initializes a new instance of the <see cref="BlockMiddleware"/> class.
    /// </summary>
    /// <param name="next">The next delegate.</param>
    public BlockMiddleware(RequestDelegate next) => this.next = next;

    /// <summary>
    /// Handles a request.
    /// </summary>
    /// <param name="context">The HTTP context.</param>
    /// <param name="provider">The snapshot provider.</param>
    /// <returns>A task.</returns>
    public async Task InvokeAsync(HttpContext context, IBlockSnapshotProvider provider)
    {
        string rawPath = context.Request.Path.Value ?? string.Empty;
        if (UmbracoOwnedPaths.IsUmbracoOwned(rawPath))
        {
            await this.next(context);
            return;
        }

        BlockSnapshot snapshot;
        try
        {
            snapshot = await provider.GetAsync(context.RequestAborted);
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            // The client went away while the request waited for the initial load: nobody reads an answer.
            return;
        }

        if (snapshot.IsEmpty)
        {
            // The common case on a site without rules: no path normalisation on the hot path.
            await this.next(context);
            return;
        }

        // Kestrel already percent-decoded the path once: it is not decoded again.
        string path = TrackedUrlNormalizer.NormalizePath(rawPath);
        int? status = snapshot.FindStatus(path, context.Request.Host.Host, context.Request.Host.Port);
        if (status is null)
        {
            await this.next(context);
            return;
        }

        context.Response.StatusCode = status.Value;
        context.Response.ContentLength = 0;
    }
}
