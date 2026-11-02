// Copyright (c) Dave Woestenborghs and contributors. Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Globalization;
using System.Net;
using Microsoft.AspNetCore.Mvc;
using Umbraco.Cms.Core.Models.PublishedContent;

namespace Umbraco.Community.PageNotFoundTracker.Integration.Tests;

/// <summary>
/// Renders the current Umbraco page for the test controllers. The test server has no Razor views, so the hijacked
/// controllers write the current page's name and title directly; what matters is that Umbraco routed the request to the
/// page and made it the current page.
/// </summary>
internal static class TestPageRenderer
{
    /// <summary>
    /// The marker written before the thread's UI culture name.
    /// </summary>
    public const string UiCultureMarker = "ui-culture:";

    /// <summary>
    /// Renders a page as a small HTML fragment.
    /// </summary>
    /// <param name="page">The current page.</param>
    /// <returns>A content result holding the page's name and title.</returns>
    public static ContentResult Render(IPublishedContent? page)
    {
        var name = WebUtility.HtmlEncode(page?.Name ?? string.Empty);
        var title = WebUtility.HtmlEncode(page?.GetProperty(PageNotFoundTestBase.TitleAlias)?.GetValue()?.ToString() ?? string.Empty);

        // The thread UI culture, which Razor templates use for dictionary items (Umbraco.GetDictionaryValue).
        var uiCulture = WebUtility.HtmlEncode(CultureInfo.CurrentUICulture.Name);

        return new ContentResult
        {
            Content = $"<html><body><h1>{name}</h1><p class=\"title\">{title}</p><p class=\"ui-culture\">{UiCultureMarker}{uiCulture}</p></body></html>",
            ContentType = "text/html",
        };
    }
}
