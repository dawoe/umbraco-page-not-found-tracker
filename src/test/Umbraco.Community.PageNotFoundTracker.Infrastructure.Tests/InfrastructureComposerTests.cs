// Copyright (c) Dave Woestenborghs and contributors. Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Umbraco.Cms.Core.Composing;
using Umbraco.Cms.Core.DependencyInjection;
using Umbraco.Community.PageNotFoundTracker.Infrastructure.NotFound;
using Umbraco.Community.PageNotFoundTracker.Infrastructure.Tracking;

namespace Umbraco.Community.PageNotFoundTracker.Infrastructure.Tests;

/// <summary>
/// Unit tests for the <see cref="InfrastructureComposer"/> class.
/// </summary>
[TestFixture]
internal sealed class InfrastructureComposerTests
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

        new InfrastructureComposer().Compose(builder);

        Assert.DoesNotThrow(() => serviceCollection.BuildServiceProvider().Dispose());
    }

    /// <summary>
    /// Tests that the tracker is the default recorder.
    /// </summary>
    [Test]
    public void Compose_Registers_Tracker_As_Recorder()
    {
        var serviceCollection = new ServiceCollection();
        var config = new ConfigurationBuilder().Build();
        var builder = new UmbracoBuilder(serviceCollection, config, new TypeLoader(Mock.Of<ITypeFinder>(), Mock.Of<ILogger<TypeLoader>>()));

        new InfrastructureComposer().Compose(builder);

        var descriptor = serviceCollection.Single(d => d.ServiceType == typeof(INotFoundRecorder));
        Assert.That(descriptor.ImplementationType, Is.EqualTo(typeof(NotFoundTracker)));
    }
}
