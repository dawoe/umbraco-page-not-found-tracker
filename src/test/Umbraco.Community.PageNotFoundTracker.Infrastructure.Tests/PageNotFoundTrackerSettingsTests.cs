// Copyright (c) Dave Woestenborghs and contributors. Licensed under the MIT License. See LICENSE in the project root for license information.

using Umbraco.Community.PageNotFoundTracker.Core.Configuration;

namespace Umbraco.Community.PageNotFoundTracker.Infrastructure.Tests;

/// <summary>
/// Unit tests for the <see cref="PageNotFoundTrackerSettings"/> class.
/// </summary>
[TestFixture]
internal sealed class PageNotFoundTrackerSettingsTests
{
    /// <summary>
    /// Tests that the 404 page document type alias is empty by default.
    /// </summary>
    [Test]
    public void NotFoundPageDocumentTypeAlias_Is_Empty_By_Default()
    {
        Assert.That(new PageNotFoundTrackerSettings().NotFoundPageDocumentTypeAlias, Is.Empty);
    }
}
