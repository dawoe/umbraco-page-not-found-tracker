// Copyright (c) Dave Woestenborghs and contributors. Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Text.Json;
using Moq;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Core.PublishedCache;
using Umbraco.Cms.Core.Routing;
using Umbraco.Cms.Core.Services;
using Umbraco.Community.PageNotFoundTracker.Core.Redirects;
using Umbraco.Community.PageNotFoundTracker.Core.Tracking;
using Umbraco.Community.PageNotFoundTracker.Infrastructure.Redirects;

namespace Umbraco.Community.PageNotFoundTracker.Infrastructure.Tests.Redirects;

/// <summary>
/// Unit tests for the <see cref="UmbracoRedirectProvider"/> class.
/// </summary>
[TestFixture]
internal sealed class UmbracoRedirectProviderTests
{
    private static readonly Guid TargetKey = new("11111111-1111-1111-1111-111111111111");

    private Mock<IRedirectUrlService> redirects = null!;
    private Mock<IDomainCache> domains = null!;
    private Mock<IPublishedContentCache> content = null!;
    private UmbracoRedirectProvider provider = null!;

    /// <summary>
    /// Creates the provider with a published target and no domains.
    /// </summary>
    [SetUp]
    public void SetUp()
    {
        this.redirects = new Mock<IRedirectUrlService>();
        this.domains = new Mock<IDomainCache>();
        this.domains.Setup(d => d.GetAll(false)).Returns([]);
        this.content = new Mock<IPublishedContentCache>();
        this.content.Setup(c => c.GetByIdAsync(TargetKey, false)).ReturnsAsync(Target(variesByCulture: false));
        this.provider = new UmbracoRedirectProvider(this.redirects.Object, this.domains.Object, this.content.Object);
    }

    /// <summary>
    /// File rows are not handled.
    /// </summary>
    [Test]
    public void File_rows_are_not_handled() =>
        Assert.That(this.provider.CanHandle(new TrackedUrl { Url = "/a.css", IsFile = true }), Is.False);

    /// <summary>
    /// Rows with a query string are not handled.
    /// </summary>
    [Test]
    public void Query_string_rows_are_not_handled() =>
        Assert.That(this.provider.CanHandle(new TrackedUrl { Url = "/a?x=1" }), Is.False);

    /// <summary>
    /// Plain page rows are handled.
    /// </summary>
    [Test]
    public void Page_rows_are_handled() =>
        Assert.That(this.provider.CanHandle(new TrackedUrl { Url = "/a" }), Is.True);

    /// <summary>
    /// A domain that does not exist fails with a message naming it.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task Unknown_domain_fails_with_message()
    {
        RedirectResult result = await this.provider.CreateAsync(Row("/old", "localhost:1/gone", string.Empty), Payload(TargetKey), CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Message, Does.Contain("localhost:1/gone"));
        });
        this.redirects.Verify(r => r.Register(It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<string>()), Times.Never);
    }

    /// <summary>
    /// With two domains of one name the record's culture picks one.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task Two_domains_same_name_culture_picks_one()
    {
        this.domains.Setup(d => d.GetAll(false)).Returns([
            new Domain(1, "localhost:1/en", 100, "en-US", false, 0),
            new Domain(2, "localhost:1/en", 200, "nl-NL", false, 0)]);

        RedirectResult result = await this.provider.CreateAsync(Row("/old", "localhost:1/en", "nl-NL"), Payload(TargetKey), CancellationToken.None);

        Assert.That(result.Succeeded, Is.True);
        this.redirects.Verify(r => r.Register("200/old", TargetKey, "nl-nl"), Times.Once);
    }

    /// <summary>
    /// With several domains of one name and none for the record's culture, creation fails.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task Two_domains_same_name_no_culture_match_fails()
    {
        this.domains.Setup(d => d.GetAll(false)).Returns([
            new Domain(1, "localhost:1/en", 100, "en-US", false, 0),
            new Domain(2, "localhost:1/en", 200, "nl-NL", false, 0)]);

        RedirectResult result = await this.provider.CreateAsync(Row("/old", "localhost:1/en", "fr-FR"), Payload(TargetKey), CancellationToken.None);

        Assert.That(result.Succeeded, Is.False);
        this.redirects.Verify(r => r.Register(It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<string>()), Times.Never);
    }

    /// <summary>
    /// A file row fails and nothing is registered, even when the provider is called directly.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task File_row_fails_without_registering()
    {
        var row = new TrackedUrl { Url = "/a.css", RelativePath = "/a.css", IsFile = true };

        RedirectResult result = await this.provider.CreateAsync(row, Payload(TargetKey), CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Message, Is.Not.Empty);
        });
        this.redirects.Verify(r => r.Register(It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<string>()), Times.Never);
    }

    /// <summary>
    /// A query string row fails and nothing is registered, even when the provider is called directly.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task Query_string_row_fails_without_registering()
    {
        var row = new TrackedUrl { Url = "/a?x=1", RelativePath = "/a?x=1" };

        RedirectResult result = await this.provider.CreateAsync(row, Payload(TargetKey), CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Message, Is.Not.Empty);
        });
        this.redirects.Verify(r => r.Register(It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<string>()), Times.Never);
    }

    /// <summary>
    /// A record without a culture registers a culture-less redirect.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task Record_without_culture_uses_invariant()
    {
        this.domains.Setup(d => d.GetAll(false)).Returns([new Domain(1, "localhost:1/sitea", 100, "en-US", false, 0)]);

        await this.provider.CreateAsync(Row("/old", "localhost:1/sitea", string.Empty), Payload(TargetKey), CancellationToken.None);

        this.redirects.Verify(r => r.Register("100/old", TargetKey, null), Times.Once);
    }

    /// <summary>
    /// A record without a domain registers the bare route.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task No_domain_registers_the_bare_route()
    {
        RedirectResult result = await this.provider.CreateAsync(Row("/old", string.Empty, string.Empty), Payload(TargetKey), CancellationToken.None);

        Assert.That(result.Succeeded, Is.True);
        this.redirects.Verify(r => r.Register("/old", TargetKey, null), Times.Once);
    }

    /// <summary>
    /// A domain without a root node fails.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task Domain_without_root_fails()
    {
        this.domains.Setup(d => d.GetAll(false)).Returns([new Domain(1, "localhost:1/sitea", 0, "en-US", false, 0)]);

        RedirectResult result = await this.provider.CreateAsync(Row("/old", "localhost:1/sitea", string.Empty), Payload(TargetKey), CancellationToken.None);

        Assert.That(result.Succeeded, Is.False);
        this.redirects.Verify(r => r.Register(It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<string>()), Times.Never);
    }

    /// <summary>
    /// A missing target fails.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task Missing_target_fails()
    {
        RedirectResult result = await this.provider.CreateAsync(Row("/old", string.Empty, string.Empty), Payload(Guid.NewGuid()), CancellationToken.None);

        Assert.That(result.Succeeded, Is.False);
        this.redirects.Verify(r => r.Register(It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<string>()), Times.Never);
    }

    /// <summary>
    /// An unpublished target (not in the published cache) fails.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task Unpublished_target_fails()
    {
        var key = Guid.NewGuid();
        this.content.Setup(c => c.GetByIdAsync(key, false)).ReturnsAsync((IPublishedContent?)null);

        RedirectResult result = await this.provider.CreateAsync(Row("/old", string.Empty, string.Empty), Payload(key), CancellationToken.None);

        Assert.That(result.Succeeded, Is.False);
        this.redirects.Verify(r => r.Register(It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<string>()), Times.Never);
    }

    /// <summary>
    /// A garbage or missing payload fails without throwing.
    /// </summary>
    /// <param name="json">The payload JSON, or <c>null</c> for no payload.</param>
    /// <returns>A task.</returns>
    [TestCase(null)]
    [TestCase("\"text\"")]
    [TestCase("[]")]
    [TestCase("{}")]
    [TestCase("{\"targetKey\":null}")]
    [TestCase("{\"targetKey\":\"not-a-guid\"}")]
    [TestCase("{\"targetKey\":12}")]
    [TestCase("{\"targetKey\":\"00000000-0000-0000-0000-000000000000\"}")]
    public async Task Garbage_or_missing_payload_fails(string? json)
    {
        JsonElement? payload = json is null ? null : JsonDocument.Parse(json).RootElement.Clone();

        RedirectResult result = await this.provider.CreateAsync(Row("/old", string.Empty, string.Empty), payload, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Message, Is.Not.Empty);
        });
        this.redirects.Verify(r => r.Register(It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<string>()), Times.Never);
    }

    /// <summary>
    /// An existing redirect for the same route and culture is deleted before the new one is registered.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task Existing_redirect_for_route_and_culture_is_replaced()
    {
        var existing = new Mock<IRedirectUrl>();
        var existingKey = Guid.NewGuid();
        existing.SetupGet(e => e.Key).Returns(existingKey);
        existing.SetupGet(e => e.Url).Returns("/old");
        existing.SetupGet(e => e.Culture).Returns("en-us");
        this.redirects.Setup(r => r.GetMostRecentRedirectUrl("/old", "en-us")).Returns(existing.Object);

        await this.provider.CreateAsync(Row("/old", string.Empty, "en-US"), Payload(TargetKey), CancellationToken.None);

        this.redirects.Verify(r => r.Delete(existingKey), Times.Once);
        this.redirects.Verify(r => r.Register("/old", TargetKey, "en-us"), Times.Once);
    }

    /// <summary>
    /// A culture-variant target that is not published in the record's culture fails and registers nothing.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task Variant_target_unpublished_in_record_culture_fails()
    {
        var key = Guid.NewGuid();
        this.content.Setup(c => c.GetByIdAsync(key, false)).ReturnsAsync(Target(variesByCulture: true, "en-US"));

        RedirectResult result = await this.provider.CreateAsync(Row("/old", string.Empty, "nl-NL"), Payload(key), CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Message, Does.Contain("nl-NL"));
        });
        this.redirects.Verify(r => r.Register(It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<string>()), Times.Never);
    }

    /// <summary>
    /// A culture-variant target that is published in the record's culture (compared case-insensitively) succeeds.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task Variant_target_published_in_record_culture_succeeds()
    {
        var key = Guid.NewGuid();
        this.content.Setup(c => c.GetByIdAsync(key, false)).ReturnsAsync(Target(variesByCulture: true, "en-US", "nl-nl"));

        RedirectResult result = await this.provider.CreateAsync(Row("/old", string.Empty, "nl-NL"), Payload(key), CancellationToken.None);

        Assert.That(result.Succeeded, Is.True);
        this.redirects.Verify(r => r.Register("/old", key, "nl-nl"), Times.Once);
    }

    /// <summary>
    /// An invariant target succeeds for a record with a culture.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task Invariant_target_succeeds_for_record_with_culture()
    {
        var key = Guid.NewGuid();
        this.content.Setup(c => c.GetByIdAsync(key, false)).ReturnsAsync(Target(variesByCulture: false));

        RedirectResult result = await this.provider.CreateAsync(Row("/old", string.Empty, "nl-NL"), Payload(key), CancellationToken.None);

        Assert.That(result.Succeeded, Is.True);
        this.redirects.Verify(r => r.Register("/old", key, "nl-nl"), Times.Once);
    }

    private static IPublishedContent Target(bool variesByCulture, params string[] publishedCultures)
    {
        var type = new Mock<IPublishedContentType>();
        type.SetupGet(t => t.Variations).Returns(variesByCulture ? ContentVariation.Culture : ContentVariation.Nothing);

        var cultures = publishedCultures.ToDictionary(
            c => c,
            c => new PublishedCultureInfo(c, c, "/", DateTime.UtcNow),
            StringComparer.OrdinalIgnoreCase);

        var target = new Mock<IPublishedContent>();
        target.SetupGet(t => t.ContentType).Returns(type.Object);
        target.SetupGet(t => t.Cultures).Returns(cultures);
        return target.Object;
    }

    private static TrackedUrl Row(string relativePath, string domain, string culture) =>
        new() { Url = relativePath, RelativePath = relativePath, Domain = domain, Culture = culture };

    private static JsonElement Payload(Guid key) =>
        JsonDocument.Parse($"{{\"targetKey\":\"{key}\"}}").RootElement.Clone();
}
