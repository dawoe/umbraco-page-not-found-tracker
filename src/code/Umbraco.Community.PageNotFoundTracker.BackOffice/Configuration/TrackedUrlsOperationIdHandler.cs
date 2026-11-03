// Copyright (c) Dave Woestenborghs and contributors. Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.AspNetCore.Mvc.Controllers;
using Umbraco.Cms.Api.Common.OpenApi;
using Umbraco.Community.PageNotFoundTracker.BackOffice.Controllers;

namespace Umbraco.Community.PageNotFoundTracker.BackOffice.Configuration;

/// <summary>
/// Gives the package's operations stable OpenAPI operation IDs: the action method name (for example
/// <c>GetTrackedUrls</c>), so the generated client does not change when a route does. Umbraco's own handler only
/// handles controllers in <c>Umbraco.Cms.Api</c> namespaces.
/// </summary>
internal sealed class TrackedUrlsOperationIdHandler : IOperationIdHandler
{
    /// <inheritdoc />
    public bool CanHandle(ApiDescription apiDescription) =>
        apiDescription.ActionDescriptor is ControllerActionDescriptor descriptor
        && descriptor.ControllerTypeInfo.IsSubclassOf(typeof(TrackedUrlsControllerBase));

    /// <inheritdoc />
    public string Handle(ApiDescription apiDescription) =>
        ((ControllerActionDescriptor)apiDescription.ActionDescriptor).ActionName;
}
