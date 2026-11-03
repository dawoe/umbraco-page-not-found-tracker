// Copyright (c) Dave Woestenborghs and contributors. Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Umbraco.Cms.Api.Common.ViewModels.Pagination;
using Umbraco.Cms.Core;
using Umbraco.Community.PageNotFoundTracker.BackOffice.Controllers;
using Umbraco.Community.PageNotFoundTracker.BackOffice.Models;
using Umbraco.Community.PageNotFoundTracker.Core.Tracking;

namespace Umbraco.Community.PageNotFoundTracker.BackOffice.Tests;

/// <summary>
/// Unit tests for <see cref="GetTrackedUrlsController"/> and <see cref="GetTrackedUrlFilterOptionsController"/>.
/// </summary>
[TestFixture]
internal sealed class TrackedUrlsControllerTests
{
    private Mock<ITrackedUrlQueryService> service = null!;
    private Mock<ITrackingPersistenceState> state = null!;
    private TrackedUrlQuery? lastQuery;

    /// <summary>
    /// Creates available persistence and a service that returns an empty page.
    /// </summary>
    [SetUp]
    public void SetUp()
    {
        this.lastQuery = null;
        this.state = new Mock<ITrackingPersistenceState>();
        this.state.SetupGet(s => s.Status).Returns(TrackingPersistenceStatus.Available);
        this.service = new Mock<ITrackedUrlQueryService>();
        this.service
            .Setup(s => s.GetPageAsync(It.IsAny<TrackedUrlQuery>(), It.IsAny<CancellationToken>()))
            .Callback<TrackedUrlQuery, CancellationToken>((query, _) => this.lastQuery = query)
            .ReturnsAsync(new TrackedUrlPage([], 0));
        this.service
            .Setup(s => s.GetFilterOptionsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TrackedUrlFilterOptions(["localhost:44402/sitea"], ["en-US"], true, false));
    }

    /// <summary>
    /// Without query parameters the list is the first 20 rows by hits, descending, unfiltered.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task Get_Defaults_QueriesHitsDescendingSkip0Take20()
    {
        IActionResult result = await this.CreateListController().GetTrackedUrls(CancellationToken.None);

        Assert.That(result, Is.InstanceOf<OkObjectResult>());
        Assert.That(this.lastQuery, Is.EqualTo(new TrackedUrlQuery(0, 20, TrackedUrlSortColumn.Hits, true, null, TrackedUrlFilter.Any, TrackedUrlFilter.Any)));
    }

    /// <summary>
    /// A page size outside 1 to 100 is a bad request and never reaches the service.
    /// </summary>
    /// <param name="take">The page size.</param>
    /// <returns>A task.</returns>
    [TestCase(0)]
    [TestCase(-1)]
    [TestCase(101)]
    public async Task Get_TakeZeroNegativeOrAbove100_ReturnsBadRequest(int take)
    {
        IActionResult result = await this.CreateListController().GetTrackedUrls(CancellationToken.None, take: take);

        AssertProblem(result, StatusCodes.Status400BadRequest);
        this.VerifyNoQuery();
    }

    /// <summary>
    /// The page size limits themselves are allowed.
    /// </summary>
    /// <param name="take">The page size.</param>
    /// <returns>A task.</returns>
    [TestCase(1)]
    [TestCase(100)]
    public async Task Get_TakeAtLimits_IsAllowed(int take)
    {
        IActionResult result = await this.CreateListController().GetTrackedUrls(CancellationToken.None, take: take);

        Assert.That(result, Is.InstanceOf<OkObjectResult>());
        Assert.That(this.lastQuery!.Take, Is.EqualTo(take));
    }

    /// <summary>
    /// A negative skip is a bad request.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task Get_NegativeSkip_ReturnsBadRequest()
    {
        IActionResult result = await this.CreateListController().GetTrackedUrls(CancellationToken.None, skip: -1);

        AssertProblem(result, StatusCodes.Status400BadRequest);
        this.VerifyNoQuery();
    }

    /// <summary>
    /// A domain and the "no domain" marker together are contradictory.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task Get_DomainAndNoDomainTogether_ReturnsBadRequest()
    {
        IActionResult result = await this.CreateListController().GetTrackedUrls(CancellationToken.None, domain: "localhost", noDomain: true);

        AssertProblem(result, StatusCodes.Status400BadRequest);
        this.VerifyNoQuery();
    }

    /// <summary>
    /// A culture and the "no culture" marker together are contradictory.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task Get_CultureAndNoCultureTogether_ReturnsBadRequest()
    {
        IActionResult result = await this.CreateListController().GetTrackedUrls(CancellationToken.None, culture: "en-US", noCulture: true);

        AssertProblem(result, StatusCodes.Status400BadRequest);
        this.VerifyNoQuery();
    }

    /// <summary>
    /// The "no domain" and "no culture" markers select rows without a value; plain values select exactly that value.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task Get_NoDomainTrue_MapsToEmptyFilter()
    {
        await this.CreateListController().GetTrackedUrls(CancellationToken.None, noDomain: true, culture: "nl-NL");
        TrackedUrlQuery first = this.lastQuery!;
        await this.CreateListController().GetTrackedUrls(CancellationToken.None, domain: "localhost:44402/sitea", noCulture: true);
        TrackedUrlQuery second = this.lastQuery!;

        Assert.Multiple(() =>
        {
            Assert.That(first.Domain, Is.EqualTo(TrackedUrlFilter.Empty));
            Assert.That(first.Culture, Is.EqualTo(TrackedUrlFilter.Exactly("nl-NL")));
            Assert.That(second.Domain, Is.EqualTo(TrackedUrlFilter.Exactly("localhost:44402/sitea")));
            Assert.That(second.Culture, Is.EqualTo(TrackedUrlFilter.Empty));
        });
    }

    /// <summary>
    /// Sort column, direction, search and paging are passed on.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task Get_PassesSortSearchAndPaging()
    {
        await this.CreateListController().GetTrackedUrls(CancellationToken.None, 40, 20, TrackedUrlSortColumn.LastSeen, Direction.Ascending, "about");

        Assert.That(this.lastQuery, Is.EqualTo(new TrackedUrlQuery(40, 20, TrackedUrlSortColumn.LastSeen, false, "about", TrackedUrlFilter.Any, TrackedUrlFilter.Any)));
    }

    /// <summary>
    /// When the tracking persistence is unavailable both endpoints answer 503 with problem details.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task Get_PersistenceUnavailable_Returns503()
    {
        this.state.SetupGet(s => s.Status).Returns(TrackingPersistenceStatus.Unavailable);

        IActionResult list = await this.CreateListController().GetTrackedUrls(CancellationToken.None);
        IActionResult options = await this.CreateFilterOptionsController().GetTrackedUrlFilterOptions(CancellationToken.None);

        AssertProblem(list, StatusCodes.Status503ServiceUnavailable);
        AssertProblem(options, StatusCodes.Status503ServiceUnavailable);
        this.VerifyNoQuery();
        this.service.Verify(s => s.GetFilterOptionsAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// While the persistence is pending the request waits (with the request token) and then queries.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task Get_PersistencePending_WaitsThenQueries()
    {
        using var cancellation = new CancellationTokenSource();
        this.state.SetupSequence(s => s.Status)
            .Returns(TrackingPersistenceStatus.Pending)
            .Returns(TrackingPersistenceStatus.Available);
        this.state.Setup(s => s.WaitUntilReadyAsync(cancellation.Token)).ReturnsAsync(true);

        IActionResult result = await this.CreateListController().GetTrackedUrls(cancellation.Token);

        Assert.That(result, Is.InstanceOf<OkObjectResult>());
        this.state.Verify(s => s.WaitUntilReadyAsync(cancellation.Token), Times.Once);
        this.service.Verify(s => s.GetPageAsync(It.IsAny<TrackedUrlQuery>(), cancellation.Token), Times.Once);
    }

    /// <summary>
    /// When the persistence turns out unavailable after waiting, the answer is 503.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task Get_PersistencePendingThenUnavailable_Returns503()
    {
        this.state.SetupSequence(s => s.Status)
            .Returns(TrackingPersistenceStatus.Pending)
            .Returns(TrackingPersistenceStatus.Unavailable);
        this.state.Setup(s => s.WaitUntilReadyAsync(It.IsAny<CancellationToken>())).ReturnsAsync(false);

        IActionResult result = await this.CreateListController().GetTrackedUrls(CancellationToken.None);

        AssertProblem(result, StatusCodes.Status503ServiceUnavailable);
        this.VerifyNoQuery();
    }

    /// <summary>
    /// A row maps to the response model with the display URL, the relative path and UTC timestamps.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task Get_MapsRelativePathAndUtcTimestamps()
    {
        var row = new TrackedUrl
        {
            Id = 7,
            Url = "/sitea/missing",
            UrlHash = "hash",
            RelativePath = "/missing",
            DisplayUrl = "/siteA/Missing",
            Domain = "localhost:44402/sitea",
            Culture = "en-US",
            IsFile = true,
            HitCount = 12,

            // As EF Core reads them from SQLite: the kind is lost.
            FirstSeen = new DateTime(2026, 11, 1, 8, 30, 0, DateTimeKind.Unspecified),
            LastSeen = new DateTime(2026, 11, 2, 9, 45, 0, DateTimeKind.Unspecified),
        };
        this.service
            .Setup(s => s.GetPageAsync(It.IsAny<TrackedUrlQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TrackedUrlPage([row], 41));

        IActionResult result = await this.CreateListController().GetTrackedUrls(CancellationToken.None);

        var page = (PagedViewModel<TrackedUrlResponseModel>)((OkObjectResult)result).Value!;
        TrackedUrlResponseModel item = page.Items.Single();
        Assert.Multiple(() =>
        {
            Assert.That(page.Total, Is.EqualTo(41));
            Assert.That(item.Id, Is.EqualTo(7));
            Assert.That(item.Url, Is.EqualTo("/siteA/Missing"));
            Assert.That(item.RelativePath, Is.EqualTo("/missing"));
            Assert.That(item.Domain, Is.EqualTo("localhost:44402/sitea"));
            Assert.That(item.Culture, Is.EqualTo("en-US"));
            Assert.That(item.IsFile, Is.True);
            Assert.That(item.HitCount, Is.EqualTo(12));
            Assert.That(item.FirstSeen, Is.EqualTo(new DateTime(2026, 11, 1, 8, 30, 0, DateTimeKind.Utc)));
            Assert.That(item.FirstSeen.Kind, Is.EqualTo(DateTimeKind.Utc));
            Assert.That(item.LastSeen, Is.EqualTo(new DateTime(2026, 11, 2, 9, 45, 0, DateTimeKind.Utc)));
            Assert.That(item.LastSeen.Kind, Is.EqualTo(DateTimeKind.Utc));
        });
    }

    /// <summary>
    /// The filter options are passed on as the response model.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task GetFilterOptions_MapsOptions()
    {
        IActionResult result = await this.CreateFilterOptionsController().GetTrackedUrlFilterOptions(CancellationToken.None);

        var model = (TrackedUrlFilterOptionsResponseModel)((OkObjectResult)result).Value!;
        Assert.Multiple(() =>
        {
            Assert.That(model.Domains, Is.EqualTo(new[] { "localhost:44402/sitea" }));
            Assert.That(model.Cultures, Is.EqualTo(new[] { "en-US" }));
            Assert.That(model.HasEmptyDomain, Is.True);
            Assert.That(model.HasEmptyCulture, Is.False);
        });
    }

    private static void AssertProblem(IActionResult result, int statusCode)
    {
        Assert.That(result, Is.InstanceOf<ObjectResult>());
        var objectResult = (ObjectResult)result;
        Assert.Multiple(() =>
        {
            Assert.That(objectResult.StatusCode, Is.EqualTo(statusCode));
            Assert.That(objectResult.Value, Is.InstanceOf<ProblemDetails>());
        });
    }

    private void VerifyNoQuery() =>
        this.service.Verify(s => s.GetPageAsync(It.IsAny<TrackedUrlQuery>(), It.IsAny<CancellationToken>()), Times.Never);

    private GetTrackedUrlsController CreateListController() => new(this.service.Object, this.state.Object);

    private GetTrackedUrlFilterOptionsController CreateFilterOptionsController() => new(this.service.Object, this.state.Object);
}
