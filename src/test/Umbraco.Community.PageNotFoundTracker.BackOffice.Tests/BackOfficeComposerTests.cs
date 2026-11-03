// Copyright (c) Dave Woestenborghs and contributors. Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Umbraco.Cms.Core.Composing;
using Umbraco.Cms.Core.DependencyInjection;
using Umbraco.Community.PageNotFoundTracker.Core;

namespace Umbraco.Community.PageNotFoundTracker.BackOffice.Tests;

/// <summary>
/// Unit tests for the <see cref="BackOfficeComposer"/> class.
/// </summary>
[TestFixture]
internal sealed class BackOfficeComposerTests
{
    /// <summary>
    /// Tests that composing registers the services without error and the provider can be built.
    /// </summary>
    [Test]
    public void Compose_Registers_Without_Error()
    {
        var serviceCollection = new ServiceCollection();
        var config = new ConfigurationBuilder().Build();
        var builder = new UmbracoBuilder(serviceCollection, config, new TypeLoader(Mock.Of<ITypeFinder>(), Mock.Of<ILogger<TypeLoader>>()));

        new BackOfficeComposer().Compose(builder);

        Assert.DoesNotThrow(() => serviceCollection.BuildServiceProvider().Dispose());
    }

    /// <summary>
    /// Tests that the composer runs before <see cref="PageNotFoundTrackerComposer"/>, so consumers can replace its registrations.
    /// </summary>
    [Test]
    public void Composer_Runs_Before_The_Package_Anchor_Composer()
    {
        var composeBefore = typeof(BackOfficeComposer).GetCustomAttributes(typeof(ComposeBeforeAttribute), inherit: false)
            .Cast<ComposeBeforeAttribute>()
            .Select(attribute => attribute.RequiringType);

        Assert.That(composeBefore, Does.Contain(typeof(PageNotFoundTrackerComposer)));
    }
}
