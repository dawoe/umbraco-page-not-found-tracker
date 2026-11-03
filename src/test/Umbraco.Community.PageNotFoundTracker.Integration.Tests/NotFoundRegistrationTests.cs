// Copyright (c) Dave Woestenborghs and contributors. Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.Options;
using Umbraco.Cms.Core.DependencyInjection;
using Umbraco.Cms.Tests.Common.Testing;
using Umbraco.Cms.Tests.Integration.Testing;
using Umbraco.Community.PageNotFoundTracker.Core.Configuration;
using Umbraco.Community.PageNotFoundTracker.Core.NotFound;
using Umbraco.Community.PageNotFoundTracker.Infrastructure.Extensions;
using Umbraco.Community.PageNotFoundTracker.Infrastructure.NotFound;
using Umbraco.Community.PageNotFoundTracker.Infrastructure.Tracking;

namespace Umbraco.Community.PageNotFoundTracker.Integration.Tests;

/// <summary>
/// Tests that the 404 handling services are registered with their defaults and that settings bind (the alias comes from appsettings.Tests.json).
/// </summary>
[TestFixture]
[UmbracoTest(Database = UmbracoTestOptions.Database.NewSchemaPerTest)]
internal sealed class NotFoundRegistrationTests : UmbracoIntegrationTest
{
    /// <inheritdoc />
    protected override void CustomTestSetup(IUmbracoBuilder builder) => builder.AddPageNotFoundTracker();

    /// <summary>
    /// Tests that the settings bind from the package configuration section.
    /// </summary>
    [Test]
    public void Settings_Bind_From_Configuration_Section()
    {
        var settings = this.GetRequiredService<IOptionsMonitor<PageNotFoundTrackerSettings>>().CurrentValue;

        Assert.That(settings.NotFoundPageDocumentTypeAlias, Is.EqualTo("notFoundPage"));
    }

    /// <summary>
    /// Tests that the default services are registered.
    /// </summary>
    [Test]
    public void Default_Services_Are_Registered()
    {
        Assert.Multiple(() =>
        {
            Assert.That(this.GetRequiredService<INotFoundPageResolver>(), Is.TypeOf<DefaultNotFoundPageResolver>());
            Assert.That(this.GetRequiredService<IRequestDomainMatcher>(), Is.TypeOf<RequestDomainMatcher>());
            Assert.That(this.GetRequiredService<INotFoundRecorder>(), Is.TypeOf<NotFoundTracker>());
        });
    }
}
