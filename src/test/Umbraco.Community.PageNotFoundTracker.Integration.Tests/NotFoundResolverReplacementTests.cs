// Copyright (c) Dave Woestenborghs and contributors. Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.DependencyInjection;
using Umbraco.Cms.Core.DependencyInjection;
using Umbraco.Cms.Core.Routing;
using Umbraco.Cms.Tests.Common.Testing;
using Umbraco.Cms.Tests.Integration.Testing;
using Umbraco.Community.PageNotFoundTracker.Core.NotFound;
using Umbraco.Community.PageNotFoundTracker.Infrastructure.Extensions;
using Umbraco.Community.PageNotFoundTracker.Infrastructure.NotFound;

namespace Umbraco.Community.PageNotFoundTracker.Integration.Tests;

/// <summary>
/// Tests that a consumer registration of <see cref="INotFoundPageResolver"/> made before the package is replaced: the
/// package registers with <c>AddUnique</c>, so consumers must compose after <c>PageNotFoundTrackerComposer</c>.
/// </summary>
[TestFixture]
[UmbracoTest(Database = UmbracoTestOptions.Database.NewSchemaPerTest)]
internal sealed class NotFoundResolverReplacementTests : UmbracoIntegrationTest
{
    /// <inheritdoc />
    protected override void CustomTestSetup(IUmbracoBuilder builder)
    {
        // The consumer registers first: the package's AddUnique replaces it.
        builder.Services.AddSingleton<INotFoundPageResolver, StubNotFoundPageResolver>();
        builder.AddPageNotFoundTracker();
    }

    /// <summary>
    /// Tests that the package's default resolver is what the container returns.
    /// </summary>
    [Test]
    public void Consumer_Resolver_Registered_Before_Package_Is_Replaced()
    {
        Assert.That(this.GetRequiredService<INotFoundPageResolver>(), Is.TypeOf<DefaultNotFoundPageResolver>());
    }

    private sealed class StubNotFoundPageResolver : INotFoundPageResolver
    {
        public Task<NotFoundPage?> ResolveAsync(NotFoundRequestContext context, Domain? domain, CancellationToken cancellationToken)
            => Task.FromResult<NotFoundPage?>(null);
    }
}
