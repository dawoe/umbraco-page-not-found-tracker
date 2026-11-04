// Copyright (c) Dave Woestenborghs and contributors. Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.AspNetCore.Builder;
using Umbraco.Community.PageNotFoundTracker.Infrastructure.Extensions;

namespace Umbraco.Community.PageNotFoundTracker.Extensions;

/// <summary>
/// Extension methods for <see cref="WebApplication"/>.
/// </summary>
public static class WebApplicationExtensions
{
    /// <summary>
    /// Adds the Page Not Found Tracker middleware to the request pipeline: blocking of unwanted URLs and the 404 page for
    /// missing files.
    /// </summary>
    /// <param name="app">A <see cref="WebApplication"/> instance.</param>
    /// <remarks>
    /// Call it after <c>BootUmbracoAsync()</c> and before <c>UseUmbraco()</c> in <c>Program.cs</c>, so the middleware wraps
    /// Umbraco's pipeline (static files, media and routing) and sees the 404 it produces. Calling it more than once
    /// registers the middleware only once. It also enables blocking: blocked requests are answered before static files,
    /// redirects and routing.
    /// </remarks>
    public static void UsePageNotFoundTracker(this WebApplication app)
    {
        app.UseBlocking();
        app.UseFileNotFoundHandling();
    }
}
