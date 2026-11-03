// Copyright (c) Dave Woestenborghs and contributors. Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OpenIddict.Validation.AspNetCore;
using Umbraco.Cms.Api.Common.OpenApi;
using Umbraco.Cms.Core.DependencyInjection;
using Umbraco.Community.PageNotFoundTracker.BackOffice.Configuration;
using Umbraco.Community.PageNotFoundTracker.BackOffice.Security;

namespace Umbraco.Community.PageNotFoundTracker.BackOffice.Extensions;

/// <summary>
/// Registers the Page Not Found Tracker Management API.
/// </summary>
internal static class UmbracoBuilderExtensions
{
    /// <summary>
    /// Adds the OpenAPI document, the stable operation IDs and the dashboard access policy. Safe to call more than once.
    /// </summary>
    /// <param name="builder">The Umbraco builder.</param>
    /// <returns>The builder.</returns>
    public static IUmbracoBuilder AddPageNotFoundTrackerManagementApi(this IUmbracoBuilder builder)
    {
        builder.Services.ConfigureOptions<ConfigureSwaggerOptions>();
        builder.Services.TryAddEnumerable(ServiceDescriptor.Singleton<IOperationIdHandler, TrackedUrlsOperationIdHandler>());

        builder.Services.TryAddEnumerable(ServiceDescriptor.Singleton<IAuthorizationHandler, DashboardAccessHandler>());
        builder.Services.AddAuthorization(options => options.AddPolicy(Constants.DashboardAccessPolicy, policy =>
        {
            // The backoffice token scheme, as Umbraco's own Management API policies use.
            policy.AuthenticationSchemes.Add(OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme);
            policy.Requirements.Add(new DashboardAccessRequirement());
        }));

        return builder;
    }
}
