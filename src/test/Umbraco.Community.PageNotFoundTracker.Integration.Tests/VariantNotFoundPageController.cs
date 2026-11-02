// Copyright (c) Dave Woestenborghs and contributors. Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewEngines;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Umbraco.Cms.Core.Dictionary;
using Umbraco.Cms.Core.Web;
using Umbraco.Cms.Web.Common.Controllers;

namespace Umbraco.Community.PageNotFoundTracker.Integration.Tests;

/// <summary>
/// Hijacks the route of the culture-variant test 404 page document type, so its pages render without a Razor view.
/// </summary>
/// <remarks>
/// Like a Razor template that shows a dictionary item, it reads one from Umbraco's culture dictionary, which picks its
/// language from the thread's UI culture and throws when that culture is the invariant culture.
/// </remarks>
public sealed class VariantNotFoundPageController : RenderController
{
    /// <summary>
    /// The key of the dictionary item the controller reads.
    /// </summary>
    public const string DictionaryKey = "NotFound.Missing";

    /// <summary>
    /// Initializes a new instance of the <see cref="VariantNotFoundPageController"/> class.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="compositeViewEngine">The view engine.</param>
    /// <param name="umbracoContextAccessor">The Umbraco context accessor.</param>
    public VariantNotFoundPageController(ILogger<RenderController> logger, ICompositeViewEngine compositeViewEngine, IUmbracoContextAccessor umbracoContextAccessor)
        : base(logger, compositeViewEngine, umbracoContextAccessor)
    {
    }

    /// <inheritdoc />
    public override IActionResult Index()
    {
        // The same lookup a template does with Umbraco.GetDictionaryValue("...").
        _ = this.HttpContext.RequestServices.GetRequiredService<ICultureDictionaryFactory>().CreateDictionary()[DictionaryKey];
        return TestPageRenderer.Render(this.CurrentPage);
    }
}
