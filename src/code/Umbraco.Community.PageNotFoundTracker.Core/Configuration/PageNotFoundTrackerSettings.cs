// Copyright (c) Dave Woestenborghs and contributors. Licensed under the MIT License. See LICENSE in the project root for license information.

namespace Umbraco.Community.PageNotFoundTracker.Core.Configuration;

/// <summary>
/// Settings of the Page Not Found Tracker, bound to the <see cref="Constants.SectionName"/> configuration section.
/// </summary>
public sealed class PageNotFoundTrackerSettings
{
    /// <summary>
    /// Gets or sets the alias of the document type used for the 404 page. Empty by default, in which case no
    /// 404 page is resolved and Umbraco's default 404 behaviour applies.
    /// </summary>
    public string NotFoundPageDocumentTypeAlias { get; set; } = string.Empty;
}
