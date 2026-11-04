// Copyright (c) Dave Woestenborghs and contributors. Licensed under the MIT License. See LICENSE in the project root for license information.

using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Models.ContentEditing;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Tests.Common.Testing;
using Umbraco.Community.PageNotFoundTracker.Core.Blocking;

namespace Umbraco.Community.PageNotFoundTracker.Integration.Tests.Blocking;

/// <summary>
/// Tests the rule dialog's scope options in the real composition: they come from Umbraco's domains and languages.
/// </summary>
[TestFixture]
[UmbracoTest(Database = UmbracoTestOptions.Database.NewSchemaPerTest, Boot = true)]
internal sealed class BlockScopeOptionsTests : PageNotFoundTestBase
{
    /// <summary>
    /// The configured domains are listed lower-cased and the languages by ISO code.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task Scope_options_list_configured_domains_and_languages()
    {
        var languageService = this.GetRequiredService<ILanguageService>();
        var created = await languageService.CreateAsync(new Language("nl-NL", "Dutch"), Cms.Core.Constants.Security.SuperUserKey);
        Assert.That(created.Success, Is.True, $"Creating the language failed: {created.Status}.");

        var home = this.GetRequiredService<IContentService>().GetRootContent().Single();
        var model = new DomainsUpdateModel
        {
            Domains =
            [
                new DomainModel { DomainName = "WWW.Site-A.local", IsoCode = "en-US" },
                new DomainModel { DomainName = "localhost:44402/SiteB", IsoCode = "nl-NL" },
            ],
        };
        var updated = await this.GetRequiredService<IDomainService>().UpdateDomainsAsync(home.Key, model);
        Assert.That(updated.Success, Is.True, $"Assigning the domains failed: {updated.Status}.");

        BlockScopeOptions options = await this.GetRequiredService<IBlockRuleService>().GetScopeOptionsAsync(CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(options.Domains, Is.EqualTo(new[] { "localhost:44402/siteb", "www.site-a.local" }));
            Assert.That(options.Cultures, Is.EquivalentTo(new[] { "en-US", "nl-NL" }));
        });
    }
}
