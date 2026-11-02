// Copyright (c) Dave Woestenborghs and contributors. Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Umbraco.Cms.Core.DependencyInjection;
using Umbraco.Cms.Core.Routing;
using Umbraco.Cms.Tests.Common.Testing;
using Umbraco.Community.PageNotFoundTracker.Core.NotFound;

namespace Umbraco.Community.PageNotFoundTracker.Integration.Tests;

/// <summary>
/// Tests that a resolver that throws on a missing file gives the plain 404, never a 500.
/// </summary>
[TestFixture]
[UmbracoTest(Database = UmbracoTestOptions.Database.NewSchemaPerTest, Boot = true)]
internal sealed class FileNotFoundThrowingResolverTests : PageNotFoundTestBase
{
    /// <inheritdoc />
    protected override void CustomTestSetup(IUmbracoBuilder builder)
    {
        base.CustomTestSetup(builder);
        builder.Services.AddSingleton<INotFoundPageResolver, ThrowingResolver>();
    }

    /// <summary>
    /// Tests that the response is the plain 404 and the occurrence is recorded without a resolved page.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task Throwing_Resolver_Returns_Plain_404()
    {
        using var response = await this.GetAsync("/missing.pdf");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
            Assert.That(body, Is.Empty);
            Assert.That(this.Recorder.Occurrences, Has.Count.EqualTo(1));
        });
        Assert.That(this.Recorder.Occurrences[0].PageResolved, Is.False);
    }

    private sealed class ThrowingResolver : INotFoundPageResolver
    {
        public Task<NotFoundPage?> ResolveAsync(NotFoundRequestContext context, Domain? domain, CancellationToken cancellationToken)
            => throw new InvalidOperationException("The resolver failed on purpose.");
    }
}
