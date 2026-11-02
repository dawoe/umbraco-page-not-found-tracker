// Copyright (c) Dave Woestenborghs and contributors. Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewEngines;
using Microsoft.Extensions.Logging;
using Umbraco.Cms.Core.Web;
using Umbraco.Cms.Web.Common.Controllers;

namespace Umbraco.Community.PageNotFoundTracker.Integration.Tests;

/// <summary>
/// Hijacks the route of the test secure page document type with an <see cref="AuthorizeAttribute"/>, so only an
/// authenticated visitor may see pages of that type.
/// </summary>
[Authorize]
public sealed class SecurePageController : RenderController
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SecurePageController"/> class.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="compositeViewEngine">The view engine.</param>
    /// <param name="umbracoContextAccessor">The Umbraco context accessor.</param>
    public SecurePageController(ILogger<RenderController> logger, ICompositeViewEngine compositeViewEngine, IUmbracoContextAccessor umbracoContextAccessor)
        : base(logger, compositeViewEngine, umbracoContextAccessor)
    {
    }

    /// <inheritdoc />
    public override IActionResult Index() => TestPageRenderer.Render(this.CurrentPage);
}
