// Copyright (c) Dave Woestenborghs and contributors. Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.DependencyInjection;
using Umbraco.Cms.Core.DependencyInjection;
using Umbraco.Cms.Core.Routing;
using Umbraco.Cms.Tests.Common.Testing;
using Umbraco.Cms.Tests.Integration.Testing;
using Umbraco.Community.PageNotFoundTracker.Core.NotFound;
using Umbraco.Community.PageNotFoundTracker.Infrastructure.Extensions;

namespace Umbraco.Community.PageNotFoundTracker.Integration.Tests;

/// <summary>
/// Tests that a consumer registration of <see cref="INotFoundPageResolver"/> registered before the package wins.
/// </summary>
[TestFixture]
[UmbracoTest(Database = UmbracoTestOptions.Database.NewSchemaPerTest)]
internal sealed class NotFoundResolverReplacementTests : UmbracoIntegrationTest
{
    /// <inheritdoc />
    protected override void CustomTestSetup(IUmbracoBuilder builder)
    {
        // The consumer registers first: the package must not overwrite it.
        builder.Services.AddSingleton<INotFoundPageResolver, StubNotFoundPageResolver>();
        builder.AddPageNotFoundTracker();
    }

    /// <summary>
    /// Tests that the registered resolver is what the container returns.
    /// </summary>
    [Test]
    public void Consumer_Resolver_Registered_Before_Package_Wins()
    {
        Assert.That(this.GetRequiredService<INotFoundPageResolver>(), Is.TypeOf<StubNotFoundPageResolver>());
    }

    private sealed class StubNotFoundPageResolver : INotFoundPageResolver
    {
        public Task<NotFoundPage?> ResolveAsync(NotFoundRequestContext context, Domain? domain, CancellationToken cancellationToken)
            => Task.FromResult<NotFoundPage?>(null);
    }
}
