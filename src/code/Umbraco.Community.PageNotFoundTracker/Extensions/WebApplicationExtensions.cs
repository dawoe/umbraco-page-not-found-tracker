// Copyright (c) Dave Woestenborghs and contributors. Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.AspNetCore.Builder;

namespace Umbraco.Community.PageNotFoundTracker.Extensions;

/// <summary>
/// Extension methods for <see cref="WebApplication"/>.
/// </summary>
public static class WebApplicationExtensions
{
    /// <summary>
    /// Adds the Page Not Found Tracker to the request pipeline. Safe to call at any position in <c>Program.cs</c>; currently does nothing and is the hook later specs fill.
    /// </summary>
    /// <param name="app">A <see cref="WebApplication"/> instance.</param>
    public static void UsePageNotFoundTracker(this WebApplication app)
    {
    }
}
