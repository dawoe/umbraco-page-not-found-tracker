// Copyright (c) Dave Woestenborghs and contributors. Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Umbraco.Community.PageNotFoundTracker.BackOffice.Controllers;
using Umbraco.Community.PageNotFoundTracker.BackOffice.Models;
using Umbraco.Community.PageNotFoundTracker.Core.Redirects;
using Umbraco.Community.PageNotFoundTracker.Core.Tracking;

namespace Umbraco.Community.PageNotFoundTracker.BackOffice.Tests;

/// <summary>
/// Unit tests for <see cref="CreateTrackedUrlRedirectController"/>.
/// </summary>
[TestFixture]
internal sealed class CreateTrackedUrlRedirectControllerTests
{
    private Mock<ITrackedUrlRedirectService> service = null!;
    private Mock<ITrackingPersistenceState> state = null!;

    /// <summary>
    /// Creates available persistence and a service that reports Created.
    /// </summary>
    [SetUp]
    public void SetUp()
    {
        this.state = new Mock<ITrackingPersistenceState>();
        this.state.SetupGet(s => s.Status).Returns(TrackingPersistenceStatus.Available);
        this.service = new Mock<ITrackedUrlRedirectService>();
        this.Returns(CreateRedirectStatus.Created, null);
    }

    /// <summary>
    /// Created answers 200 with the record removed.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task Created_Returns200RecordRemoved()
    {
        IActionResult result = await this.CreateController().CreateTrackedUrlRedirect(5, new CreateRedirectRequestModel(), CancellationToken.None);

        var model = (CreateRedirectResponseModel)((OkObjectResult)result).Value!;
        Assert.Multiple(() =>
        {
            Assert.That(model.RecordRemoved, Is.True);
            Assert.That(model.Message, Is.Null);
        });
    }

    /// <summary>
    /// CreatedRecordNotRemoved answers 200 with the message and the record kept.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task CreatedRecordNotRemoved_Returns200WithMessage()
    {
        this.Returns(CreateRedirectStatus.CreatedRecordNotRemoved, "kept");

        IActionResult result = await this.CreateController().CreateTrackedUrlRedirect(5, new CreateRedirectRequestModel(), CancellationToken.None);

        var model = (CreateRedirectResponseModel)((OkObjectResult)result).Value!;
        Assert.Multiple(() =>
        {
            Assert.That(model.RecordRemoved, Is.False);
            Assert.That(model.Message, Is.EqualTo("kept"));
        });
    }

    /// <summary>
    /// NotFound, NotSupported and Failed answer 404, 409 and 422 problem details carrying the message.
    /// </summary>
    /// <param name="status">The service status.</param>
    /// <param name="httpStatus">The expected HTTP status.</param>
    /// <returns>A task.</returns>
    [TestCase(CreateRedirectStatus.NotFound, StatusCodes.Status404NotFound)]
    [TestCase(CreateRedirectStatus.NotSupported, StatusCodes.Status409Conflict)]
    [TestCase(CreateRedirectStatus.Failed, StatusCodes.Status422UnprocessableEntity)]
    public async Task Failure_ReturnsProblemDetails(CreateRedirectStatus status, int httpStatus)
    {
        this.Returns(status, "the reason");

        IActionResult result = await this.CreateController().CreateTrackedUrlRedirect(5, new CreateRedirectRequestModel(), CancellationToken.None);

        var objectResult = (ObjectResult)result;
        var problem = (ProblemDetails)objectResult.Value!;
        Assert.Multiple(() =>
        {
            Assert.That(objectResult.StatusCode, Is.EqualTo(httpStatus));
            Assert.That(problem.Status, Is.EqualTo(httpStatus));
            Assert.That(problem.Detail, Is.EqualTo("the reason"));
        });
    }

    /// <summary>
    /// A missing body is the same as a null payload.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task MissingBody_PassesNullPayload()
    {
        await this.CreateController().CreateTrackedUrlRedirect(5, null, CancellationToken.None);

        this.service.Verify(s => s.CreateAsync(5, null, It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// The payload and the cancellation token reach the service.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task Payload_IsPassedToTheService()
    {
        JsonElement payload = JsonDocument.Parse("{\"targetKey\":\"x\"}").RootElement.Clone();
        using var cancellation = new CancellationTokenSource();

        await this.CreateController().CreateTrackedUrlRedirect(5, new CreateRedirectRequestModel { Payload = payload }, cancellation.Token);

        this.service.Verify(
            s => s.CreateAsync(5, It.Is<JsonElement?>(p => p.HasValue && p.Value.GetProperty("targetKey").GetString() == "x"), cancellation.Token),
            Times.Once);
    }

    /// <summary>
    /// Unavailable persistence answers 503 and never reaches the service.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task PersistenceUnavailable_Returns503()
    {
        this.state.SetupGet(s => s.Status).Returns(TrackingPersistenceStatus.Unavailable);

        IActionResult result = await this.CreateController().CreateTrackedUrlRedirect(5, new CreateRedirectRequestModel(), CancellationToken.None);

        Assert.That(((ObjectResult)result).StatusCode, Is.EqualTo(StatusCodes.Status503ServiceUnavailable));
        this.service.Verify(s => s.CreateAsync(It.IsAny<int>(), It.IsAny<JsonElement?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private void Returns(CreateRedirectStatus status, string? message) =>
        this.service
            .Setup(s => s.CreateAsync(It.IsAny<int>(), It.IsAny<JsonElement?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CreateRedirectOutcome(status, message));

    private CreateTrackedUrlRedirectController CreateController() => new(this.service.Object, this.state.Object);
}
