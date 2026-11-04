// Copyright (c) Dave Woestenborghs and contributors. Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.AspNetCore.Builder;
using Umbraco.Community.PageNotFoundTracker.Infrastructure.Blocking;
using Umbraco.Community.PageNotFoundTracker.Infrastructure.NotFound;

namespace Umbraco.Community.PageNotFoundTracker.Infrastructure.Extensions;

/// <summary>
/// Extension methods for <see cref="IApplicationBuilder"/>.
/// </summary>
internal static class ApplicationBuilderExtensions
{
    /// <summary>
    /// The <see cref="IApplicationBuilder.Properties"/> key that marks the file 404 middleware as registered.
    /// </summary>
    internal const string FileNotFoundMiddlewareRegisteredKey = "Umbraco.Community.PageNotFoundTracker.FileNotFoundMiddlewareRegistered";

    /// <summary>
    /// The <see cref="IApplicationBuilder.Properties"/> key that marks the block middleware as registered.
    /// </summary>
    internal const string BlockMiddlewareRegisteredKey = "Umbraco.Community.PageNotFoundTracker.BlockMiddlewareRegistered";

    /// <summary>
    /// Adds <see cref="BlockMiddleware"/> to the pipeline, once: later calls on the same application do nothing.
    /// </summary>
    /// <param name="app">The application builder.</param>
    /// <returns>The same application builder.</returns>
    public static IApplicationBuilder UseBlocking(this IApplicationBuilder app)
    {
        if (app.Properties.ContainsKey(BlockMiddlewareRegisteredKey))
        {
            return app;
        }

        app.Properties[BlockMiddlewareRegisteredKey] = true;
        return app.UseMiddleware<BlockMiddleware>();
    }

    /// <summary>
    /// Adds <see cref="FileNotFoundMiddleware"/> to the pipeline, once: later calls on the same application do nothing.
    /// </summary>
    /// <param name="app">The application builder.</param>
    /// <returns>The same application builder.</returns>
    public static IApplicationBuilder UseFileNotFoundHandling(this IApplicationBuilder app)
    {
        if (app.Properties.ContainsKey(FileNotFoundMiddlewareRegisteredKey))
        {
            return app;
        }

        app.Properties[FileNotFoundMiddlewareRegisteredKey] = true;
        return app.UseMiddleware<FileNotFoundMiddleware>();
    }
}
