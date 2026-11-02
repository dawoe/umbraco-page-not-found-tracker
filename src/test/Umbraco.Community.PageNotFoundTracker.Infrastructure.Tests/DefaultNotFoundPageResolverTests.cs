// Copyright (c) Dave Woestenborghs and contributors. Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.Options;
using Moq;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Core.PublishedCache;
using Umbraco.Cms.Core.Routing;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core.Services.Navigation;
using Umbraco.Community.PageNotFoundTracker.Core.Configuration;
using Umbraco.Community.PageNotFoundTracker.Core.NotFound;
using Umbraco.Community.PageNotFoundTracker.Infrastructure.NotFound;

namespace Umbraco.Community.PageNotFoundTracker.Infrastructure.Tests;

/// <summary>
/// Unit tests for the <see cref="DefaultNotFoundPageResolver"/> class.
/// </summary>
[TestFixture]
internal sealed class DefaultNotFoundPageResolverTests
{
    private const string Alias = "notFound";

    private static readonly NotFoundRequestContext Request = new("/missing", "localhost", 44401, false);

    private readonly Dictionary<Guid, IPublishedContent> published = new();
    private readonly Dictionary<int, Guid> keysById = new();
    private readonly Dictionary<Guid, List<Guid>> childrenOfType = new();
    private readonly Dictionary<Guid, List<Guid>> descendantsOfType = new();
    private List<Guid> roots = new();
    private string alias = Alias;
    private string defaultIso = "en-US";

    private delegate bool TryKeys(Guid key, string type, out IEnumerable<Guid> keys);

    private delegate bool TryRoots(out IEnumerable<Guid> keys);

    /// <summary>
    /// Resets the fake content tree.
    /// </summary>
    [SetUp]
    public void Reset()
    {
        this.published.Clear();
        this.keysById.Clear();
        this.childrenOfType.Clear();
        this.descendantsOfType.Clear();
        this.roots = new List<Guid>();
        this.alias = Alias;
        this.defaultIso = "en-US";
    }

    /// <summary>
    /// Tests that the root of the passed domain is searched, not the first root.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task Uses_Root_Of_Domain()
    {
        var rootA = this.AddRoot(10);
        var rootB = this.AddRoot(20);
        var pageA = this.AddPage(rootA, direct: true);
        var pageB = this.AddPage(rootB, direct: true);

        var result = await this.CreateResolver().ResolveAsync(Request, Domain(20, "en-US"), CancellationToken.None);

        Assert.That(result?.Content, Is.SameAs(pageB));
        Assert.That(result?.Content, Is.Not.SameAs(pageA));
    }

    /// <summary>
    /// Tests that without a domain only the first root is searched.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task Without_Domain_Uses_First_Root_Only()
    {
        var rootA = this.AddRoot(10);
        var rootB = this.AddRoot(20);
        this.AddPage(rootB, direct: true);

        var resultNoPage = await this.CreateResolver().ResolveAsync(Request, null, CancellationToken.None);
        var pageA = this.AddPage(rootA, direct: true);
        var resultPage = await this.CreateResolver().ResolveAsync(Request, null, CancellationToken.None);

        Assert.That(resultNoPage, Is.Null);
        Assert.That(resultPage?.Content, Is.SameAs(pageA));
    }

    /// <summary>
    /// Tests that a direct child wins over a descendant listed earlier.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task Direct_Child_Beats_Earlier_Descendant()
    {
        var root = this.AddRoot(10);
        this.AddPage(root, direct: false);
        var direct = this.AddPage(root, direct: true);

        var result = await this.CreateResolver().ResolveAsync(Request, Domain(10, null), CancellationToken.None);

        Assert.That(result?.Content, Is.SameAs(direct));
    }

    /// <summary>
    /// Tests that a deeper descendant is used when there is no direct child.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task Uses_Descendant_When_No_Direct_Child()
    {
        var root = this.AddRoot(10);
        var deep = this.AddPage(root, direct: false);

        var result = await this.CreateResolver().ResolveAsync(Request, Domain(10, null), CancellationToken.None);

        Assert.That(result?.Content, Is.SameAs(deep));
    }

    /// <summary>
    /// Tests that the domain culture selects the variant and is returned.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task Domain_Culture_Selects_Variant_And_Is_Returned()
    {
        var root = this.AddRoot(10);
        var page = this.AddPage(root, direct: true, varies: true, publishedCulture: "nl-NL");

        var result = await this.CreateResolver().ResolveAsync(Request, Domain(10, "nl-NL"), CancellationToken.None);

        Assert.That(result?.Content, Is.SameAs(page));
        Assert.That(result?.Culture, Is.EqualTo("nl-NL"));
    }

    /// <summary>
    /// Tests that a domain without culture uses the default language for selection but does not report it.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task Domain_Without_Culture_Uses_Default_Language_Without_Reporting_It()
    {
        var root = this.AddRoot(10);
        var page = this.AddPage(root, direct: true, varies: true, publishedCulture: "en-US");

        var result = await this.CreateResolver().ResolveAsync(Request, Domain(10, null), CancellationToken.None);

        Assert.That(result?.Content, Is.SameAs(page));
        Assert.That(result?.Culture, Is.Null);
    }

    /// <summary>
    /// Tests that a page with no published variant in the culture is not found.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task Page_Without_Published_Variant_In_Culture_Returns_Null()
    {
        var root = this.AddRoot(10);
        this.AddPage(root, direct: true, varies: true, publishedCulture: "en-US");

        var result = await this.CreateResolver().ResolveAsync(Request, Domain(10, "fr-FR"), CancellationToken.None);

        Assert.That(result, Is.Null);
    }

    /// <summary>
    /// Tests that an empty alias returns nothing.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task Empty_Alias_Returns_Null()
    {
        var root = this.AddRoot(10);
        this.AddPage(root, direct: true);
        this.alias = string.Empty;

        var result = await this.CreateResolver().ResolveAsync(Request, Domain(10, null), CancellationToken.None);

        Assert.That(result, Is.Null);
    }

    /// <summary>
    /// Tests that no page of the alias returns nothing.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task No_Page_Of_Alias_Returns_Null()
    {
        this.AddRoot(10);

        var result = await this.CreateResolver().ResolveAsync(Request, Domain(10, null), CancellationToken.None);

        Assert.That(result, Is.Null);
    }

    /// <summary>
    /// Tests that an unpublished page returns nothing.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task Unpublished_Page_Returns_Null()
    {
        var root = this.AddRoot(10);
        var page = this.AddPage(root, direct: true);
        this.published.Remove(page.Key);

        var result = await this.CreateResolver().ResolveAsync(Request, Domain(10, null), CancellationToken.None);

        Assert.That(result, Is.Null);
    }

    private static Domain Domain(int contentId, string? culture)
    {
        return new Domain(1, "localhost", contentId, culture, false, 0);
    }

    private Guid AddRoot(int id)
    {
        var key = Guid.NewGuid();
        var content = new Mock<IPublishedContent>();
        content.SetupGet(c => c.Key).Returns(key);
        this.published[key] = content.Object;
        this.keysById[id] = key;
        this.roots.Add(key);
        this.childrenOfType[key] = new List<Guid>();
        this.descendantsOfType[key] = new List<Guid>();
        return key;
    }

    private IPublishedContent AddPage(Guid root, bool direct, bool varies = false, string? publishedCulture = null)
    {
        var key = Guid.NewGuid();
        var contentType = new Mock<IPublishedContentType>();
        contentType.SetupGet(t => t.Variations)
            .Returns(varies ? ContentVariation.Culture : ContentVariation.Nothing);
        var content = new Mock<IPublishedContent>();
        content.SetupGet(c => c.Key).Returns(key);
        content.SetupGet(c => c.ContentType).Returns(contentType.Object);
        content.Setup(c => c.IsPublished(It.IsAny<string?>()))
            .Returns((string? culture) => !varies || culture == publishedCulture);
        this.published[key] = content.Object;

        // Descendants are listed in tree order, so a non-direct page added first is listed before the direct one.
        this.descendantsOfType[root].Add(key);
        if (direct)
        {
            this.childrenOfType[root].Add(key);
        }

        return content.Object;
    }

    private DefaultNotFoundPageResolver CreateResolver()
    {
        var navigation = new Mock<IDocumentNavigationQueryService>();
        navigation.Setup(n => n.TryGetRootKeys(out It.Ref<IEnumerable<Guid>>.IsAny))
            .Returns(new TryRoots((out IEnumerable<Guid> keys) =>
            {
                keys = this.roots;
                return true;
            }));
        navigation.Setup(n => n.TryGetChildrenKeysOfType(It.IsAny<Guid>(), Alias, out It.Ref<IEnumerable<Guid>>.IsAny))
            .Returns(new TryKeys((Guid key, string type, out IEnumerable<Guid> keys) =>
            {
                keys = this.childrenOfType.GetValueOrDefault(key) ?? new List<Guid>();
                return true;
            }));
        navigation.Setup(n => n.TryGetDescendantsKeysOfType(It.IsAny<Guid>(), Alias, out It.Ref<IEnumerable<Guid>>.IsAny))
            .Returns(new TryKeys((Guid key, string type, out IEnumerable<Guid> keys) =>
            {
                keys = this.descendantsOfType.GetValueOrDefault(key) ?? new List<Guid>();
                return true;
            }));

        var cache = new Mock<IPublishedContentCache>();
        cache.Setup(c => c.GetByIdAsync(It.IsAny<int>(), It.IsAny<bool?>()))
            .ReturnsAsync((int id, bool? _) => this.keysById.TryGetValue(id, out var key) ? this.published[key] : null);
        cache.Setup(c => c.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<bool?>()))
            .ReturnsAsync((Guid key, bool? _) => this.published.GetValueOrDefault(key));

        var languages = new Mock<ILanguageService>();
        languages.Setup(l => l.GetDefaultIsoCodeAsync()).ReturnsAsync(() => this.defaultIso);

        var settings = new Mock<IOptionsMonitor<PageNotFoundTrackerSettings>>();
        settings.SetupGet(s => s.CurrentValue)
            .Returns(() => new PageNotFoundTrackerSettings { NotFoundPageDocumentTypeAlias = this.alias });

        return new DefaultNotFoundPageResolver(settings.Object, cache.Object, navigation.Object, languages.Object);
    }
}
