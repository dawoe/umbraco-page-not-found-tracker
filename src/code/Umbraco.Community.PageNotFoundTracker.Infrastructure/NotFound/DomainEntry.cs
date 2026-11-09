// Copyright (c) Dave Woestenborghs and contributors. Licensed under the MIT License. See LICENSE in the project root for license information.

namespace Umbraco.Community.PageNotFoundTracker.Infrastructure.NotFound;

/// <summary>
/// An Umbraco domain parsed for request matching.
/// </summary>
/// <param name="Name">The domain name lower-cased, as tracking stores it (including port and alias path).</param>
/// <param name="Host">The host, compared ignoring case.</param>
/// <param name="Port">The port, or <c>null</c> when the domain has none.</param>
/// <param name="Path">The domain's own path without a trailing slash, empty for a bare host.</param>
/// <param name="Culture">The domain's culture, empty when it has none.</param>
internal sealed record DomainEntry(string Name, string Host, int? Port, string Path, string Culture);
