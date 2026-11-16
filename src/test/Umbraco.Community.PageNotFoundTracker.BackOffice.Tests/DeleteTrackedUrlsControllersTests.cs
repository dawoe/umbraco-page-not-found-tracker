// Copyright (c) Dave Woestenborghs and contributors. Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Umbraco.Community.PageNotFoundTracker.BackOffice.Controllers;
using Umbraco.Community.PageNotFoundTracker.BackOffice.Models;
using Umbraco.Community.PageNotFoundTracker.Core.Tracking;

namespace Umbraco.Community.PageNotFoundTracker.BackOffice.Tests;

/// <summary>
/// Unit tests for the delete tracked URL controllers.
/// </summary>
[TestFixture]
internal sealed class DeleteTrackedUrlsControllersTests
{
    private Mock<ITrackedUrlRemover> remover = null!;
    private Mock<ITrackingPersistenceState> state = null!;

    /// <summary>
    /// Creates available persistence and a remover that deletes everything it is asked to.
    /// </summary>
    [SetUp]
    public void SetUp()
    {
        this.state = new Mock<ITrackingPersistenceState>();
        this.state.SetupGet(s => s.Status).Returns(TrackingPersistenceStatus.Available);
        this.remover = new Mock<ITrackedUrlRemover>();
        this.remover
            .Setup(r => r.DeleteAsync(It.IsAny<IReadOnlyCollection<int>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyCollection<int> ids, CancellationToken _) => ids.Count);
        this.remover
            .Setup(r => r.DeleteMatchingAsync(It.IsAny<TrackedUrlCriteria>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(7);
    }

    /// <summary>
    /// Deleting one row answers 200 with a count of one.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task DeleteTrackedUrl_returns_200_with_one()
    {
        IActionResult result = await this.Single().DeleteTrackedUrl(5, CancellationToken.None);

        Assert.That(((DeleteTrackedUrlsResponseModel)((OkObjectResult)result).Value!).Deleted, Is.EqualTo(1));
        this.remover.Verify(r => r.DeleteAsync(It.Is<IReadOnlyCollection<int>>(i => i.SequenceEqual(new[] { 5 })), It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// A row that is already gone is a 404.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task DeleteTrackedUrl_returns_404_when_the_row_is_gone()
    {
        this.remover
            .Setup(r => r.DeleteAsync(It.IsAny<IReadOnlyCollection<int>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        IActionResult result = await this.Single().DeleteTrackedUrl(5, CancellationToken.None);

        var objectResult = (ObjectResult)result;
        Assert.Multiple(() =>
        {
            Assert.That(objectResult.StatusCode, Is.EqualTo(StatusCodes.Status404NotFound));
            Assert.That(((ProblemDetails)objectResult.Value!).Title, Is.EqualTo("Tracked URL not found"));
        });
    }

    /// <summary>
    /// Duplicate ids are removed before the remover is called.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task DeleteTrackedUrls_deduplicates_ids_before_calling_the_remover()
    {
        IActionResult result = await this.Many().DeleteTrackedUrls(new DeleteTrackedUrlsRequestModel { Ids = [3, 4, 3, 4, 5] }, CancellationToken.None);

        Assert.That(((DeleteTrackedUrlsResponseModel)((OkObjectResult)result).Value!).Deleted, Is.EqualTo(3));
        this.remover.Verify(r => r.DeleteAsync(It.Is<IReadOnlyCollection<int>>(i => i.SequenceEqual(new[] { 3, 4, 5 })), It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// No ids (absent or empty) is a 400.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task DeleteTrackedUrls_rejects_empty_ids()
    {
        IActionResult absent = await this.Many().DeleteTrackedUrls(new DeleteTrackedUrlsRequestModel(), CancellationToken.None);
        IActionResult empty = await this.Many().DeleteTrackedUrls(new DeleteTrackedUrlsRequestModel { Ids = [] }, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(absent, Is.InstanceOf<BadRequestObjectResult>());
            Assert.That(empty, Is.InstanceOf<BadRequestObjectResult>());
        });
        this.remover.VerifyNoOtherCalls();
    }

    /// <summary>
    /// An id below one is a 400.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task DeleteTrackedUrls_rejects_an_id_below_one()
    {
        IActionResult result = await this.Many().DeleteTrackedUrls(new DeleteTrackedUrlsRequestModel { Ids = [4, 0] }, CancellationToken.None);

        Assert.That(result, Is.InstanceOf<BadRequestObjectResult>());
        this.remover.VerifyNoOtherCalls();
    }

    /// <summary>
    /// More than 100 distinct ids is a 400; 100 are accepted and duplicates do not count.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task DeleteTrackedUrls_rejects_more_than_100_ids()
    {
        IActionResult tooMany = await this.Many().DeleteTrackedUrls(new DeleteTrackedUrlsRequestModel { Ids = Enumerable.Range(1, 101).ToArray() }, CancellationToken.None);
        IActionResult atLimit = await this.Many().DeleteTrackedUrls(new DeleteTrackedUrlsRequestModel { Ids = Enumerable.Range(1, 100).Concat(Enumerable.Range(1, 50)).ToArray() }, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(tooMany, Is.InstanceOf<BadRequestObjectResult>());
            Assert.That(atLimit, Is.InstanceOf<OkObjectResult>());
        });
    }

    /// <summary>
    /// No filter and no <c>All</c> is a 400 and nothing is deleted.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task DeleteMatching_without_a_filter_and_without_all_is_400_and_does_not_call_the_remover()
    {
        IActionResult none = await this.Matching().DeleteMatchingTrackedUrls(new DeleteMatchingTrackedUrlsRequestModel(), CancellationToken.None);
        IActionResult blank = await this.Matching().DeleteMatchingTrackedUrls(new DeleteMatchingTrackedUrlsRequestModel { Search = "  ", Domain = string.Empty }, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(none, Is.InstanceOf<BadRequestObjectResult>());
            Assert.That(blank, Is.InstanceOf<BadRequestObjectResult>());
        });
        this.remover.VerifyNoOtherCalls();
    }

    /// <summary>
    /// <c>All</c> together with any filter is a 400.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task DeleteMatching_with_all_and_a_filter_is_400()
    {
        IActionResult result = await this.Matching().DeleteMatchingTrackedUrls(new DeleteMatchingTrackedUrlsRequestModel { All = true, NoCulture = true }, CancellationToken.None);

        Assert.That(result, Is.InstanceOf<BadRequestObjectResult>());
        this.remover.VerifyNoOtherCalls();
    }

    /// <summary>
    /// <c>All</c> alone deletes with an empty criteria.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task DeleteMatching_with_all_calls_the_remover_with_an_empty_criteria()
    {
        IActionResult result = await this.Matching().DeleteMatchingTrackedUrls(new DeleteMatchingTrackedUrlsRequestModel { All = true }, CancellationToken.None);

        Assert.That(((DeleteTrackedUrlsResponseModel)((OkObjectResult)result).Value!).Deleted, Is.EqualTo(7));
        this.remover.Verify(
            r => r.DeleteMatchingAsync(new TrackedUrlCriteria(null, TrackedUrlFilter.Any, TrackedUrlFilter.Any), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    /// <summary>
    /// Search, domain and culture reach the remover as the criteria the list would use.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task DeleteMatching_maps_search_domain_and_culture_to_the_criteria()
    {
        await this.Matching().DeleteMatchingTrackedUrls(new DeleteMatchingTrackedUrlsRequestModel { Search = "old", Domain = "d.com", NoCulture = true }, CancellationToken.None);

        this.remover.Verify(
            r => r.DeleteMatchingAsync(new TrackedUrlCriteria("old", TrackedUrlFilter.Exactly("d.com"), TrackedUrlFilter.Empty), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    /// <summary>
    /// A domain together with <c>NoDomain</c> (and a culture with <c>NoCulture</c>) is a 400.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task DeleteMatching_rejects_domain_with_noDomain()
    {
        IActionResult domain = await this.Matching().DeleteMatchingTrackedUrls(new DeleteMatchingTrackedUrlsRequestModel { Domain = "d.com", NoDomain = true }, CancellationToken.None);
        IActionResult culture = await this.Matching().DeleteMatchingTrackedUrls(new DeleteMatchingTrackedUrlsRequestModel { Culture = "en-US", NoCulture = true }, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(domain, Is.InstanceOf<BadRequestObjectResult>());
            Assert.That(culture, Is.InstanceOf<BadRequestObjectResult>());
        });
        this.remover.VerifyNoOtherCalls();
    }

    /// <summary>
    /// An unavailable database is a 503 for every operation, and nothing is deleted.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task Every_controller_answers_503_when_persistence_is_unavailable()
    {
        this.state.SetupGet(s => s.Status).Returns(TrackingPersistenceStatus.Unavailable);

        IActionResult single = await this.Single().DeleteTrackedUrl(1, CancellationToken.None);
        IActionResult many = await this.Many().DeleteTrackedUrls(new DeleteTrackedUrlsRequestModel { Ids = [1] }, CancellationToken.None);
        IActionResult matching = await this.Matching().DeleteMatchingTrackedUrls(new DeleteMatchingTrackedUrlsRequestModel { All = true }, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(((ObjectResult)single).StatusCode, Is.EqualTo(StatusCodes.Status503ServiceUnavailable));
            Assert.That(((ObjectResult)many).StatusCode, Is.EqualTo(StatusCodes.Status503ServiceUnavailable));
            Assert.That(((ObjectResult)matching).StatusCode, Is.EqualTo(StatusCodes.Status503ServiceUnavailable));
        });
        this.remover.VerifyNoOtherCalls();
    }

    private DeleteTrackedUrlController Single() => new(this.remover.Object, this.state.Object);

    private DeleteTrackedUrlsController Many() => new(this.remover.Object, this.state.Object);

    private DeleteMatchingTrackedUrlsController Matching() => new(this.remover.Object, this.state.Object);
}
