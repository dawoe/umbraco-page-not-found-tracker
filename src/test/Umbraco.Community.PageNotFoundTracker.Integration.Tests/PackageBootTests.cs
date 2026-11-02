// Copyright (c) Dave Woestenborghs and contributors. Licensed under the MIT License. See LICENSE in the project root for license information.

using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Composing;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Tests.Common.Testing;
using Umbraco.Cms.Tests.Integration.Testing;

namespace Umbraco.Community.PageNotFoundTracker.Integration.Tests;

/// <summary>
/// Smoke tests that boot Umbraco on SQLite with the package composed in.
/// </summary>
[TestFixture]
[UmbracoTest(Database = UmbracoTestOptions.Database.NewSchemaPerTest)]
internal sealed class PackageBootTests : UmbracoIntegrationTest
{
    /// <summary>
    /// Tests that Umbraco boots to the run level and the package composers were discovered and composed.
    /// </summary>
    [Test]
    public void Umbraco_Boots_With_Package_Composed_In()
    {
        var runtimeState = this.GetRequiredService<IRuntimeState>();
        var composerNames = this.GetRequiredService<TypeLoader>()
            .GetTypes<IComposer>()
            .Select(type => type.FullName)
            .ToList();

        Assert.Multiple(() =>
        {
            Assert.That(runtimeState.Level, Is.EqualTo(RuntimeLevel.Run));
            Assert.That(composerNames, Does.Contain("Umbraco.Community.PageNotFoundTracker.Infrastructure.InfrastructureComposer"));
            Assert.That(composerNames, Does.Contain("Umbraco.Community.PageNotFoundTracker.BackOffice.BackOfficeComposer"));
        });
    }
}
