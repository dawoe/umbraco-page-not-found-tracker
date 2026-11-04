// Copyright (c) Dave Woestenborghs and contributors. Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;
using Umbraco.Cms.Api.Management.OpenApi;

namespace Umbraco.Community.PageNotFoundTracker.BackOffice.Configuration;

/// <summary>
/// Adds the package's OpenAPI document (<see cref="Constants.ApiName"/>) and marks its operations as protected by
/// Umbraco's backoffice security scheme (<c>Backoffice-User</c>, defined by the Management API itself).
/// </summary>
internal sealed class ConfigureSwaggerOptions : IConfigureOptions<SwaggerGenOptions>
{
    /// <inheritdoc />
    public void Configure(SwaggerGenOptions options)
    {
        options.SwaggerDoc(
            Constants.ApiName,
            new OpenApiInfo
            {
                Title = "Page Not Found Tracker Management API",
                Version = "1.0",
                Description = "The tracked 404 URLs for the Page Not Found Tracker dashboard.",
            });

        options.OperationFilter<SecurityRequirementsOperationFilter>();
    }

    /// <summary>
    /// Adds the backoffice security requirement (and the 401/403 responses) to the operations of this API only.
    /// </summary>
    private sealed class SecurityRequirementsOperationFilter : BackOfficeSecurityRequirementsOperationFilterBase
    {
        /// <inheritdoc />
        protected override string ApiName => Constants.ApiName;
    }
}
