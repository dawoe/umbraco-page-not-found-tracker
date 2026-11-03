// Copyright (c) Dave Woestenborghs and contributors. Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Umbraco.Cms.Core.Composing;
using Umbraco.Cms.Core.DependencyInjection;
using Umbraco.Cms.Core.Routing;
using Umbraco.Community.PageNotFoundTracker.Core;
using Umbraco.Community.PageNotFoundTracker.Core.NotFound;
using Umbraco.Community.PageNotFoundTracker.Infrastructure.NotFound;
using Umbraco.Community.PageNotFoundTracker.Persistence.Sqlite;
using Umbraco.Community.PageNotFoundTracker.Persistence.SqlServer;
using Umbraco.Extensions;

namespace Umbraco.Community.PageNotFoundTracker.Infrastructure.Tests;

/// <summary>
/// Tests the composer order contract: every package composer runs before <see cref="PageNotFoundTrackerComposer"/>, and
/// replaceable services are registered with <c>AddUnique</c>, so a consumer composer that runs after it wins.
/// </summary>
[TestFixture]
internal sealed class ComposerOrderTests
{
    /// <summary>
    /// Tests that the anchor composer is public and sealed, so consumers can reference it in <c>[ComposeAfter]</c>.
    /// </summary>
    [Test]
    public void Anchor_Composer_Is_Public()
    {
        Assert.Multiple(() =>
        {
            Assert.That(typeof(PageNotFoundTrackerComposer).IsPublic, Is.True);
            Assert.That(typeof(PageNotFoundTrackerComposer).IsSealed, Is.True);
        });
    }

    /// <summary>
    /// Tests that each package composer runs before the anchor composer.
    /// </summary>
    /// <param name="composerType">The package composer.</param>
    [TestCase(typeof(InfrastructureComposer))]
    [TestCase(typeof(SqlitePersistenceComposer))]
    [TestCase(typeof(SqlServerPersistenceComposer))]
    public void Package_Composer_Runs_Before_The_Anchor_Composer(Type composerType)
    {
        ArgumentNullException.ThrowIfNull(composerType);

        var composeBefore = composerType.GetCustomAttributes(typeof(ComposeBeforeAttribute), inherit: false)
            .Cast<ComposeBeforeAttribute>()
            .Select(attribute => attribute.RequiringType);

        Assert.That(composeBefore, Does.Contain(typeof(PageNotFoundTrackerComposer)));
    }

    /// <summary>
    /// Tests that a replaceable service registered by the package is replaced by a later <c>AddUnique</c>, and that the
    /// package itself replaces an earlier registration (the order of composers decides, not TryAdd).
    /// </summary>
    [Test]
    public void Replaceable_Services_Use_Last_Registration()
    {
        var earlier = CreateBuilder();
        earlier.Services.AddUnique<INotFoundPageResolver, StubNotFoundPageResolver>();
        new InfrastructureComposer().Compose(earlier);

        var later = CreateBuilder();
        new InfrastructureComposer().Compose(later);
        later.Services.AddUnique<INotFoundPageResolver, StubNotFoundPageResolver>();

        Assert.Multiple(() =>
        {
            Assert.That(Single<INotFoundPageResolver>(earlier).ImplementationType, Is.EqualTo(typeof(DefaultNotFoundPageResolver)));
            Assert.That(Single<INotFoundPageResolver>(later).ImplementationType, Is.EqualTo(typeof(StubNotFoundPageResolver)));
            Assert.That(Single<IRequestDomainMatcher>(later).ImplementationType, Is.EqualTo(typeof(RequestDomainMatcher)));
        });
    }

    private static UmbracoBuilder CreateBuilder() =>
        new(new ServiceCollection(), new ConfigurationBuilder().Build(), new TypeLoader(Mock.Of<ITypeFinder>(), Mock.Of<ILogger<TypeLoader>>()));

    private static ServiceDescriptor Single<TService>(UmbracoBuilder builder) =>
        builder.Services.Single(descriptor => descriptor.ServiceType == typeof(TService));

    private sealed class StubNotFoundPageResolver : INotFoundPageResolver
    {
        public Task<NotFoundPage?> ResolveAsync(NotFoundRequestContext context, Domain? domain, CancellationToken cancellationToken)
            => Task.FromResult<NotFoundPage?>(null);
    }
}
