// Copyright (c) Dave Woestenborghs and contributors. Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Umbraco.Community.PageNotFoundTracker.Infrastructure.Extensions;

namespace Umbraco.Community.PageNotFoundTracker.Integration.Tests;

/// <summary>
/// Puts the package middleware in front of the test server's Umbraco pipeline, where a site's <c>Program.cs</c> puts it
/// with <c>UsePageNotFoundTracker()</c>.
/// </summary>
/// <remarks>
/// <c>UmbracoTestServerTestBase</c> builds its pipeline in a non-virtual <c>Configure(IApplicationBuilder)</c> and has
/// no <c>WebApplication</c>, so <c>UsePageNotFoundTracker()</c> cannot be called. The startup filter runs before that
/// <c>Configure</c> and calls <c>UseFileNotFoundHandling()</c>, the method that
/// <c>UsePageNotFoundTracker()</c> delegates to.
/// </remarks>
internal sealed class PageNotFoundTrackerStartupFilter : IStartupFilter
{
    /// <inheritdoc />
    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) =>
        app =>
        {
            app.UseBlocking();
            app.UseFileNotFoundHandling();
            next(app);
        };
}
