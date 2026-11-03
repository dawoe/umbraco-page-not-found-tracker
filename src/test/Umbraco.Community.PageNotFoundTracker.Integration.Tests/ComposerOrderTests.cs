// Copyright (c) Dave Woestenborghs and contributors. Licensed under the MIT License. See LICENSE in the project root for license information.

using Umbraco.Cms.Tests.Common.Testing;
using Umbraco.Cms.Tests.Integration.TestServerTest;
using Umbraco.Community.PageNotFoundTracker.Core;

namespace Umbraco.Community.PageNotFoundTracker.Integration.Tests;

/// <summary>
/// Tests, through Umbraco's own composer ordering, that a composer marked
/// <c>[ComposeAfter(typeof(PageNotFoundTrackerComposer))]</c> runs after every package composer.
/// </summary>
[TestFixture]
[UmbracoTest(Database = UmbracoTestOptions.Database.NewSchemaPerFixture)]
internal sealed class ComposerOrderTests : UmbracoTestServerTestBase
{
    /// <summary>
    /// Tests that the package registrations (Infrastructure and both persistence composers) exist when the consumer composer runs.
    /// </summary>
    [Test]
    public void Consumer_Composer_Runs_After_Package_Composers()
    {
        var probe = this.GetRequiredService<ComposerOrderProbe>();

        Assert.Multiple(() =>
        {
            Assert.That(probe.ResolverRegistered, Is.True, "InfrastructureComposer ran before the consumer composer.");
            Assert.That(probe.ProviderCount, Is.EqualTo(2), "Both persistence composers ran before the consumer composer.");
        });
    }
}
