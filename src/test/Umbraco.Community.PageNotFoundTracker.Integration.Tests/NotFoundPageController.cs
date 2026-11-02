// Copyright (c) Dave Woestenborghs and contributors. Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewEngines;
using Microsoft.Extensions.Logging;
using Umbraco.Cms.Core.Web;
using Umbraco.Cms.Web.Common.Controllers;

namespace Umbraco.Community.PageNotFoundTracker.Integration.Tests;

/// <summary>
/// Hijacks the route of the test 404 page document type, so pages of that type render without a Razor view.
/// </summary>
public sealed class NotFoundPageController : RenderController
{
    /// <summary>
    /// Initializes a new instance of the <see cref="NotFoundPageController"/> class.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="compositeViewEngine">The view engine.</param>
    /// <param name="umbracoContextAccessor">The Umbraco context accessor.</param>
    public NotFoundPageController(ILogger<RenderController> logger, ICompositeViewEngine compositeViewEngine, IUmbracoContextAccessor umbracoContextAccessor)
        : base(logger, compositeViewEngine, umbracoContextAccessor)
    {
    }

    /// <inheritdoc />
    public override IActionResult Index() => TestPageRenderer.Render(this.CurrentPage);
}
