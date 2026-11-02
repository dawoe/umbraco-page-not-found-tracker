// Copyright (c) Dave Woestenborghs and contributors. Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Umbraco.Cms.Core.DependencyInjection;
using Umbraco.Cms.Core.PublishedCache;
using Umbraco.Cms.Core.Routing;
using Umbraco.Cms.Tests.Common.Testing;
using Umbraco.Community.PageNotFoundTracker.Core.NotFound;

namespace Umbraco.Community.PageNotFoundTracker.Integration.Tests;

/// <summary>
/// Tests that replacing <see cref="INotFoundPageResolver"/> through DI changes which page answers a missing file.
/// </summary>
[TestFixture]
[UmbracoTest(Database = UmbracoTestOptions.Database.NewSchemaPerTest, Boot = true)]
internal sealed class FileNotFoundStubResolverTests : PageNotFoundTestBase
{
    /// <inheritdoc />
    protected override void CustomTestSetup(IUmbracoBuilder builder)
    {
        base.CustomTestSetup(builder);
        builder.Services.AddSingleton<INotFoundPageResolver, OtherPageResolver>();
    }

    /// <summary>
    /// Tests that the replacement resolver's page is shown with status 404, and that it got the file request.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task Replacement_Resolver_Chooses_The_Page()
    {
        using var response = await this.GetAsync("/missing.pdf");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
            Assert.That(body, Does.Contain(OtherPageTitle));
            Assert.That(body, Does.Not.Contain(NotFoundPageTitle));
            Assert.That(OtherPageResolver.LastContext?.PathAndQuery, Is.EqualTo("/missing.pdf"));
            Assert.That(OtherPageResolver.LastContext?.IsFile, Is.True);
        });
    }

    private sealed class OtherPageResolver : INotFoundPageResolver
    {
        private readonly IPublishedContentCache contentCache;

        public OtherPageResolver(IPublishedContentCache contentCache) => this.contentCache = contentCache;

        public static NotFoundRequestContext? LastContext { get; private set; }

        public async Task<NotFoundPage?> ResolveAsync(NotFoundRequestContext context, Domain? domain, CancellationToken cancellationToken)
        {
            LastContext = context;
            var content = await this.contentCache.GetByIdAsync(OtherPageKey, false);
            return content is null ? null : new NotFoundPage(content, null);
        }
    }
}
