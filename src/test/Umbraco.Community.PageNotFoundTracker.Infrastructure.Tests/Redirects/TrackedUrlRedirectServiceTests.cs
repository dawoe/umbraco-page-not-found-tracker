// Copyright (c) Dave Woestenborghs and contributors. Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Text.Json;
using Moq;
using Umbraco.Community.PageNotFoundTracker.Core.Redirects;
using Umbraco.Community.PageNotFoundTracker.Core.Tracking;
using Umbraco.Community.PageNotFoundTracker.Infrastructure.Redirects;
using Umbraco.Community.PageNotFoundTracker.Infrastructure.Tests.TestSupport;

namespace Umbraco.Community.PageNotFoundTracker.Infrastructure.Tests.Redirects;

/// <summary>
/// Unit tests for the <see cref="TrackedUrlRedirectService"/> class.
/// </summary>
[TestFixture]
internal sealed class TrackedUrlRedirectServiceTests
{
    private Mock<ITrackedUrlQueryService> query = null!;
    private Mock<IRedirectProvider> provider = null!;
    private Mock<ITrackedUrlRemover> remover = null!;
    private CapturingLogger<TrackedUrlRedirectService> logger = null!;
    private TrackedUrl url = null!;
    private TrackedUrlRedirectService service = null!;

    /// <summary>
    /// Creates the service with a found record and a provider that handles and succeeds.
    /// </summary>
    [SetUp]
    public void SetUp()
    {
        this.url = new TrackedUrl { Id = 7, Url = "/old" };
        this.query = new Mock<ITrackedUrlQueryService>();
        this.query.Setup(q => q.GetByIdAsync(7, It.IsAny<CancellationToken>())).ReturnsAsync(this.url);
        this.provider = new Mock<IRedirectProvider>();
        this.provider.Setup(p => p.CanHandle(this.url)).Returns(true);
        this.provider.Setup(p => p.CreateAsync(this.url, It.IsAny<JsonElement?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(RedirectResult.Success());
        this.remover = new Mock<ITrackedUrlRemover>();
        this.remover.Setup(r => r.DeleteAsync(this.url, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        this.logger = new CapturingLogger<TrackedUrlRedirectService>();
        this.service = new TrackedUrlRedirectService(this.query.Object, this.provider.Object, this.remover.Object, this.logger);
    }

    /// <summary>
    /// An unknown id is not found and the provider is not called.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task Unknown_id_is_NotFound_and_provider_not_called()
    {
        CreateRedirectOutcome outcome = await this.service.CreateAsync(99, null, CancellationToken.None);

        Assert.That(outcome.Status, Is.EqualTo(CreateRedirectStatus.NotFound));
        this.provider.Verify(p => p.CanHandle(It.IsAny<TrackedUrl>()), Times.Never);
        this.provider.Verify(p => p.CreateAsync(It.IsAny<TrackedUrl>(), It.IsAny<JsonElement?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// A provider that cannot handle the URL gives NotSupported and nothing is deleted.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task Provider_cannot_handle_is_NotSupported_and_nothing_deleted()
    {
        this.provider.Setup(p => p.CanHandle(this.url)).Returns(false);

        CreateRedirectOutcome outcome = await this.service.CreateAsync(7, null, CancellationToken.None);

        Assert.That(outcome.Status, Is.EqualTo(CreateRedirectStatus.NotSupported));
        Assert.That(outcome.Message, Is.Not.Empty);
        this.provider.Verify(p => p.CreateAsync(It.IsAny<TrackedUrl>(), It.IsAny<JsonElement?>(), It.IsAny<CancellationToken>()), Times.Never);
        this.remover.Verify(r => r.DeleteAsync(It.IsAny<TrackedUrl>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// A failed provider result is returned with its message and nothing is deleted.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task Provider_failure_returns_message_and_deletes_nothing()
    {
        this.provider.Setup(p => p.CreateAsync(this.url, It.IsAny<JsonElement?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(RedirectResult.Failure("Target is missing."));

        CreateRedirectOutcome outcome = await this.service.CreateAsync(7, null, CancellationToken.None);

        Assert.That(outcome.Status, Is.EqualTo(CreateRedirectStatus.Failed));
        Assert.That(outcome.Message, Is.EqualTo("Target is missing."));
        this.remover.Verify(r => r.DeleteAsync(It.IsAny<TrackedUrl>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// A success deletes the record and returns Created.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task Provider_success_deletes_record_and_returns_Created()
    {
        CreateRedirectOutcome outcome = await this.service.CreateAsync(7, null, CancellationToken.None);

        Assert.That(outcome.Status, Is.EqualTo(CreateRedirectStatus.Created));
        this.remover.Verify(r => r.DeleteAsync(this.url, It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// A throwing provider gives Failed with a generic message that hides the exception, and is logged.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task Provider_throws_is_Failed_with_generic_message_and_logged_and_nothing_deleted()
    {
        this.provider.Setup(p => p.CreateAsync(this.url, It.IsAny<JsonElement?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("secret detail"));

        CreateRedirectOutcome outcome = await this.service.CreateAsync(7, null, CancellationToken.None);

        Assert.That(outcome.Status, Is.EqualTo(CreateRedirectStatus.Failed));
        Assert.That(outcome.Message, Is.Not.Empty);
        Assert.That(outcome.Message, Does.Not.Contain("secret detail"));
        Assert.That(this.logger.Errors, Has.Count.EqualTo(1));
        this.remover.Verify(r => r.DeleteAsync(It.IsAny<TrackedUrl>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// The delete after a success ignores the caller's cancellation, so an aborted request cannot skip the removal.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task Delete_after_success_uses_no_cancellation_token()
    {
        using var cts = new CancellationTokenSource();

        await this.service.CreateAsync(7, null, cts.Token);

        this.remover.Verify(r => r.DeleteAsync(this.url, CancellationToken.None), Times.Once);
    }

    /// <summary>
    /// A failing delete after a success gives CreatedRecordNotRemoved and is logged.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task Delete_throws_after_success_is_CreatedRecordNotRemoved_and_logged()
    {
        this.remover.Setup(r => r.DeleteAsync(this.url, It.IsAny<CancellationToken>())).ThrowsAsync(new InvalidOperationException("db down"));

        CreateRedirectOutcome outcome = await this.service.CreateAsync(7, null, CancellationToken.None);

        Assert.That(outcome.Status, Is.EqualTo(CreateRedirectStatus.CreatedRecordNotRemoved));
        Assert.That(outcome.Message, Is.EqualTo("The redirect was created, but the tracked URL could not be removed from the list."));
        Assert.That(this.logger.Errors, Has.Count.EqualTo(1));
    }

    /// <summary>
    /// A record that was already gone still counts as created.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task Delete_returns_false_after_success_is_still_Created()
    {
        this.remover.Setup(r => r.DeleteAsync(this.url, It.IsAny<CancellationToken>())).ReturnsAsync(false);

        CreateRedirectOutcome outcome = await this.service.CreateAsync(7, null, CancellationToken.None);

        Assert.That(outcome.Status, Is.EqualTo(CreateRedirectStatus.Created));
    }
}
