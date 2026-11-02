// Copyright (c) Dave Woestenborghs and contributors. Licensed under the MIT License. See LICENSE in the project root for license information.

using Umbraco.Cms.Core.Models.PublishedContent;

namespace Umbraco.Community.PageNotFoundTracker.Core.NotFound;

/// <summary>
/// The page chosen to answer a 404, with the culture it is rendered in.
/// </summary>
/// <param name="Content">The published 404 page.</param>
/// <param name="Culture">The culture of the language variant, or <c>null</c> for the default culture or an invariant page.</param>
public sealed record NotFoundPage(IPublishedContent Content, string? Culture);
