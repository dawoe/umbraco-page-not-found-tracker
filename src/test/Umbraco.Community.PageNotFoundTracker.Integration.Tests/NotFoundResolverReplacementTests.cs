// Copyright (c) Dave Woestenborghs and contributors. Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.DependencyInjection;
using Umbraco.Cms.Core.DependencyInjection;
using Umbraco.Cms.Core.Routing;
using Umbraco.Cms.Tests.Common.Testing;
using Umbraco.Cms.Tests.Integration.Testing;
using Umbraco.Community.PageNotFoundTracker.Core.NotFound;

namespace Umbraco.Community.PageNotFoundTracker.Integration.Tests;

/// <summary>
/// Tests that a consumer registration of <see cref="INotFoundPageResolver"/> replaces the default one.
/// </summary>
[TestFixture]
[UmbracoTest(Database = UmbracoTestOptions.Database.NewSchemaPerTest)]
internal sealed class NotFoundResolverReplacementTests : UmbracoIntegrationTest
{
    /// <inheritdoc />
    protected override void CustomTestSetup(IUmbracoBuilder builder)
        => builder.Services.AddSingleton<INotFoundPageResolver, StubNotFoundPageResolver>();

    /// <summary>
    /// Tests that the registered resolver is what the container returns.
    /// </summary>
    [Test]
    public void Consumer_Resolver_Replaces_Default()
    {
        Assert.That(this.GetRequiredService<INotFoundPageResolver>(), Is.TypeOf<StubNotFoundPageResolver>());
    }

    private sealed class StubNotFoundPageResolver : INotFoundPageResolver
    {
        public Task<NotFoundPage?> ResolveAsync(NotFoundRequestContext context, Domain? domain, CancellationToken cancellationToken)
            => Task.FromResult<NotFoundPage?>(null);
    }
}
