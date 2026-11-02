// Copyright (c) Dave Woestenborghs and contributors. Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Net;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewEngines;
using Microsoft.Extensions.Logging;
using Umbraco.Cms.Core.Web;
using Umbraco.Cms.Web.Common.Controllers;

namespace Umbraco.Community.PageNotFoundTracker.Integration.Tests;

/// <summary>
/// Hijacks the route of the test link page document type. It generates a URL with <c>Url.Action</c>, as templates do
/// with <c>Url.Action</c>, <c>Html.ActionLink</c> and the <c>asp-action</c> tag helpers, to prove link generation works.
/// </summary>
public sealed class LinkPageController : RenderController
{
    /// <summary>
    /// The marker written before the generated URL.
    /// </summary>
    public const string LinkMarker = "generated-link:";

    /// <summary>
    /// Initializes a new instance of the <see cref="LinkPageController"/> class.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="compositeViewEngine">The view engine.</param>
    /// <param name="umbracoContextAccessor">The Umbraco context accessor.</param>
    public LinkPageController(ILogger<RenderController> logger, ICompositeViewEngine compositeViewEngine, IUmbracoContextAccessor umbracoContextAccessor)
        : base(logger, compositeViewEngine, umbracoContextAccessor)
    {
    }

    /// <inheritdoc />
    public override IActionResult Index()
    {
        var link = this.Url.Action("Index") ?? string.Empty;
        var name = WebUtility.HtmlEncode(this.CurrentPage?.Name ?? string.Empty);
        return this.Content($"<html><body><h1>{name}</h1><p>{LinkMarker}{WebUtility.HtmlEncode(link)}</p></body></html>", "text/html");
    }
}
