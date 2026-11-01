// Copyright (c) Dave Woestenborghs and contributors. Licensed under the MIT License. See LICENSE in the project root for license information.

using Umbraco.Cms.Core.DependencyInjection;

namespace Umbraco.Community.PageNotFoundTracker.Infrastructure.Extensions;

/// <summary>
/// Extension methods for <see cref="IUmbracoBuilder"/>.
/// </summary>
internal static class UmbracoBuilderExtensions
{
    /// <summary>
    /// Registers the Page Not Found Tracker services with the Umbraco builder.
    /// </summary>
    /// <param name="builder">A <see cref="IUmbracoBuilder"/>.</param>
    /// <returns>Updated <see cref="IUmbracoBuilder"/>.</returns>
    public static IUmbracoBuilder AddPageNotFoundTracker(this IUmbracoBuilder builder) => builder;
}
