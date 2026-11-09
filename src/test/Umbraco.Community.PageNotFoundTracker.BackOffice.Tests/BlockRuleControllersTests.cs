// Copyright (c) Dave Woestenborghs and contributors. Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Umbraco.Cms.Api.Common.ViewModels.Pagination;
using Umbraco.Cms.Core;
using Umbraco.Community.PageNotFoundTracker.BackOffice.Controllers;
using Umbraco.Community.PageNotFoundTracker.BackOffice.Models;
using Umbraco.Community.PageNotFoundTracker.Core.Blocking;
using Umbraco.Community.PageNotFoundTracker.Core.Tracking;

namespace Umbraco.Community.PageNotFoundTracker.BackOffice.Tests;

/// <summary>
/// Unit tests for the block rule controllers.
/// </summary>
[TestFixture]
internal sealed class BlockRuleControllersTests
{
    private Mock<IBlockRuleService> service = null!;
    private Mock<ITrackingPersistenceState> state = null!;

    /// <summary>
    /// Creates available persistence and a service that returns an empty page.
    /// </summary>
    [SetUp]
    public void SetUp()
    {
        this.state = new Mock<ITrackingPersistenceState>();
        this.state.SetupGet(s => s.Status).Returns(TrackingPersistenceStatus.Available);
        this.service = new Mock<IBlockRuleService>();
        this.service
            .Setup(s => s.QueryAsync(It.IsAny<BlockRuleQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BlockRulePage([], 0));
    }

    /// <summary>
    /// The list maps the rule, uses the display pattern and marks the time as UTC.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task List_MapsRulesAndSetsUtc()
    {
        var rule = new BlockRule
        {
            Id = 3,
            Pattern = "/wp-*",
            DisplayPattern = "/WP-*",
            StatusCode = 410,
            Domain = "d",
            Culture = "en-US",
            Created = new DateTime(2026, 11, 1, 8, 0, 0, DateTimeKind.Unspecified),
        };
        this.service
            .Setup(s => s.QueryAsync(It.IsAny<BlockRuleQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BlockRulePage([rule], 7));

        IActionResult result = await this.ListController().GetBlockRules(CancellationToken.None);

        var page = (PagedViewModel<BlockRuleResponseModel>)((OkObjectResult)result).Value!;
        BlockRuleResponseModel item = page.Items.Single();
        Assert.Multiple(() =>
        {
            Assert.That(page.Total, Is.EqualTo(7));
            Assert.That(item.Id, Is.EqualTo(3));
            Assert.That(item.Pattern, Is.EqualTo("/WP-*"));
            Assert.That(item.StatusCode, Is.EqualTo(410));
            Assert.That(item.Domain, Is.EqualTo("d"));
            Assert.That(item.Culture, Is.EqualTo("en-US"));
            Assert.That(item.Created.Kind, Is.EqualTo(DateTimeKind.Utc));
        });
    }

    /// <summary>
    /// The defaults reach the service: newest first, 20 rows, no filter.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task List_DefaultsToCreatedDescending()
    {
        await this.ListController().GetBlockRules(CancellationToken.None);

        this.service.Verify(
            s => s.QueryAsync(
                It.Is<BlockRuleQuery>(q => q.Skip == 0 && q.Take == 20 && q.SortColumn == BlockRuleSortColumn.Created && q.Descending && q.Search == null && q.Domain == TrackedUrlFilter.Any && q.Culture == TrackedUrlFilter.Any),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    /// <summary>
    /// The filters and the sort are passed through.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task List_PassesFiltersAndSort()
    {
        await this.ListController().GetBlockRules(CancellationToken.None, 5, 10, BlockRuleSortColumn.Pattern, Direction.Ascending, "wp", "d", false, null, true);

        this.service.Verify(
            s => s.QueryAsync(
                It.Is<BlockRuleQuery>(q => q.Skip == 5 && q.Take == 10 && q.SortColumn == BlockRuleSortColumn.Pattern && !q.Descending && q.Search == "wp" && q.Domain == TrackedUrlFilter.Exactly("d") && q.Culture == TrackedUrlFilter.Empty),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    /// <summary>
    /// Invalid paging or contradicting filters are a 400 and never reach the service.
    /// </summary>
    /// <param name="skip">The skip.</param>
    /// <param name="take">The take.</param>
    /// <param name="domain">The domain.</param>
    /// <param name="noDomain">The no domain flag.</param>
    /// <returns>A task.</returns>
    [TestCase(-1, 20, null, false)]
    [TestCase(0, 0, null, false)]
    [TestCase(0, 101, null, false)]
    [TestCase(0, 20, "d", true)]
    public async Task List_InvalidInput_Returns400(int skip, int take, string? domain, bool noDomain)
    {
        IActionResult result = await this.ListController().GetBlockRules(CancellationToken.None, skip, take, domain: domain, noDomain: noDomain);

        Assert.That(((ObjectResult)result).StatusCode, Is.EqualTo(StatusCodes.Status400BadRequest));
        this.service.Verify(s => s.QueryAsync(It.IsAny<BlockRuleQuery>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// Unavailable persistence is a 503 for every endpoint.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task Unavailable_Returns503ForAllEndpoints()
    {
        this.state.SetupGet(s => s.Status).Returns(TrackingPersistenceStatus.Unavailable);
        var request = new CreateBlockRuleRequestModel { Pattern = "/x*", StatusCode = 410 };

        IActionResult[] results =
        [
            await this.ListController().GetBlockRules(CancellationToken.None),
            await new CreateBlockRuleController(this.service.Object, this.state.Object).CreateBlockRule(request, CancellationToken.None),
            await new DeleteBlockRuleController(this.service.Object, this.state.Object).DeleteBlockRule(1, CancellationToken.None),
            await this.UpdateController().UpdateBlockRule(1, new CreateBlockRuleRequestModel { Pattern = "/a", StatusCode = 410 }, CancellationToken.None),
            await new GetBlockScopeOptionsController(this.service.Object, this.state.Object).GetBlockScopeOptions(CancellationToken.None),
        ];

        Assert.That(results.Select(r => ((ObjectResult)r).StatusCode), Is.All.EqualTo(StatusCodes.Status503ServiceUnavailable));
        this.service.VerifyNoOtherCalls();
    }

    /// <summary>
    /// Saved answers 200 with the outcome and passes the values through unchanged.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task Create_Saved_Returns200()
    {
        this.service
            .Setup(s => s.CreateAsync("/wp-*", 410, "d", "en-us", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CreateBlockRuleOutcome(CreateBlockRuleStatus.Saved, null, true));

        IActionResult result = await this.CreateController().CreateBlockRule(
            new CreateBlockRuleRequestModel { Pattern = "/wp-*", StatusCode = 410, Domain = "d", Culture = "en-us" },
            CancellationToken.None);

        var model = (CreateBlockRuleResponseModel)((OkObjectResult)result).Value!;
        Assert.Multiple(() =>
        {
            Assert.That(model.RecordsRemoved, Is.True);
            Assert.That(model.Message, Is.Null);
        });
    }

    /// <summary>
    /// A missing domain and culture reach the service as empty strings.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task Create_NullScope_PassesEmptyStrings()
    {
        this.service
            .Setup(s => s.CreateAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CreateBlockRuleOutcome(CreateBlockRuleStatus.Saved, null, false));

        await this.CreateController().CreateBlockRule(new CreateBlockRuleRequestModel { Pattern = "/a*", StatusCode = 404 }, CancellationToken.None);

        this.service.Verify(s => s.CreateAsync("/a*", 404, string.Empty, string.Empty, It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// A saved rule whose records were not removed always carries a message for the editor.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task Create_SavedRecordsNotRemovedWithoutMessage_GetsFallbackMessage()
    {
        this.service
            .Setup(s => s.CreateAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CreateBlockRuleOutcome(CreateBlockRuleStatus.Saved, null, false));

        IActionResult result = await this.CreateController().CreateBlockRule(new CreateBlockRuleRequestModel { Pattern = "/a*", StatusCode = 410 }, CancellationToken.None);

        var model = (CreateBlockRuleResponseModel)((OkObjectResult)result).Value!;
        Assert.Multiple(() =>
        {
            Assert.That(model.RecordsRemoved, Is.False);
            Assert.That(model.Message, Is.Not.Null.And.Not.Empty);
        });
    }

    /// <summary>
    /// An invalid outcome is a 400 with the service's message as detail.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task Create_Invalid_Returns400WithDetail()
    {
        this.service
            .Setup(s => s.CreateAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CreateBlockRuleOutcome(CreateBlockRuleStatus.Invalid, "too broad", false));

        IActionResult result = await this.CreateController().CreateBlockRule(new CreateBlockRuleRequestModel { Pattern = "/*", StatusCode = 410 }, CancellationToken.None);

        var bad = (BadRequestObjectResult)result;
        Assert.That(((ProblemDetails)bad.Value!).Detail, Is.EqualTo("too broad"));
    }

    /// <summary>
    /// A null body or a blank pattern is a 400 and never reaches the service.
    /// </summary>
    /// <param name="pattern">The pattern.</param>
    /// <returns>A task.</returns>
    [TestCase(null)]
    [TestCase("")]
    [TestCase("   ")]
    public async Task Create_BlankPattern_Returns400(string? pattern)
    {
        IActionResult blank = await this.CreateController().CreateBlockRule(new CreateBlockRuleRequestModel { Pattern = pattern, StatusCode = 410 }, CancellationToken.None);
        IActionResult noBody = await this.CreateController().CreateBlockRule(null, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(blank, Is.TypeOf<BadRequestObjectResult>());
            Assert.That(noBody, Is.TypeOf<BadRequestObjectResult>());
        });
        this.service.Verify(
            s => s.CreateAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    /// <summary>
    /// Saved answers 200 with the outcome and passes the id and values through unchanged.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task Update_Saved_Returns200()
    {
        this.service
            .Setup(s => s.UpdateAsync(5, "/wp-*", 410, "d", "en-us", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CreateBlockRuleOutcome(CreateBlockRuleStatus.Saved, null, true));

        IActionResult result = await this.UpdateController().UpdateBlockRule(
            5,
            new CreateBlockRuleRequestModel { Pattern = "/wp-*", StatusCode = 410, Domain = "d", Culture = "en-us" },
            CancellationToken.None);

        var model = (CreateBlockRuleResponseModel)((OkObjectResult)result).Value!;
        Assert.Multiple(() =>
        {
            Assert.That(model.RecordsRemoved, Is.True);
            Assert.That(model.Message, Is.Null);
        });
    }

    /// <summary>
    /// A missing domain and culture reach the service as empty strings.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task Update_NullScope_PassesEmptyStrings()
    {
        this.SetupUpdate(new CreateBlockRuleOutcome(CreateBlockRuleStatus.Saved, null, true));

        await this.UpdateController().UpdateBlockRule(5, new CreateBlockRuleRequestModel { Pattern = "/a*", StatusCode = 404 }, CancellationToken.None);

        this.service.Verify(s => s.UpdateAsync(5, "/a*", 404, string.Empty, string.Empty, It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// An invalid outcome is a 400 with the service's message as detail.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task Update_Invalid_Returns400WithDetail()
    {
        this.SetupUpdate(new CreateBlockRuleOutcome(CreateBlockRuleStatus.Invalid, "too broad", false));

        IActionResult result = await this.UpdateController().UpdateBlockRule(5, new CreateBlockRuleRequestModel { Pattern = "/*", StatusCode = 410 }, CancellationToken.None);

        var bad = (BadRequestObjectResult)result;
        Assert.That(((ProblemDetails)bad.Value!).Detail, Is.EqualTo("too broad"));
    }

    /// <summary>
    /// An unknown rule is a 404.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task Update_NotFound_Returns404()
    {
        this.SetupUpdate(new CreateBlockRuleOutcome(CreateBlockRuleStatus.NotFound, null, false));

        IActionResult result = await this.UpdateController().UpdateBlockRule(5, new CreateBlockRuleRequestModel { Pattern = "/a", StatusCode = 410 }, CancellationToken.None);

        var objectResult = (ObjectResult)result;
        Assert.Multiple(() =>
        {
            Assert.That(objectResult.StatusCode, Is.EqualTo(StatusCodes.Status404NotFound));
            Assert.That(((ProblemDetails)objectResult.Value!).Title, Is.EqualTo("Block rule not found"));
        });
    }

    /// <summary>
    /// A conflicting identity is a 409 with the message as detail.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task Update_Conflict_Returns409WithDetail()
    {
        this.SetupUpdate(new CreateBlockRuleOutcome(CreateBlockRuleStatus.Conflict, BlockingConstants.RuleExistsMessage, false));

        IActionResult result = await this.UpdateController().UpdateBlockRule(5, new CreateBlockRuleRequestModel { Pattern = "/a", StatusCode = 410 }, CancellationToken.None);

        var objectResult = (ObjectResult)result;
        Assert.Multiple(() =>
        {
            Assert.That(objectResult.StatusCode, Is.EqualTo(StatusCodes.Status409Conflict));
            Assert.That(((ProblemDetails)objectResult.Value!).Detail, Is.EqualTo(BlockingConstants.RuleExistsMessage));
        });
    }

    /// <summary>
    /// A null body or a blank pattern is a 400 and never reaches the service.
    /// </summary>
    /// <param name="pattern">The pattern.</param>
    /// <returns>A task.</returns>
    [TestCase(null)]
    [TestCase("")]
    [TestCase("   ")]
    public async Task Update_BlankPattern_Returns400(string? pattern)
    {
        IActionResult blank = await this.UpdateController().UpdateBlockRule(5, new CreateBlockRuleRequestModel { Pattern = pattern, StatusCode = 410 }, CancellationToken.None);
        IActionResult noBody = await this.UpdateController().UpdateBlockRule(5, null, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(blank, Is.TypeOf<BadRequestObjectResult>());
            Assert.That(noBody, Is.TypeOf<BadRequestObjectResult>());
        });
        this.service.Verify(
            s => s.UpdateAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    /// <summary>
    /// Deleting an existing rule answers 200, an unknown one 404.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task Delete_ExistingIs200_UnknownIs404()
    {
        this.service.Setup(s => s.DeleteAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        this.service.Setup(s => s.DeleteAsync(2, It.IsAny<CancellationToken>())).ReturnsAsync(false);
        var controller = new DeleteBlockRuleController(this.service.Object, this.state.Object);

        IActionResult found = await controller.DeleteBlockRule(1, CancellationToken.None);
        IActionResult missing = await controller.DeleteBlockRule(2, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(found, Is.TypeOf<OkResult>());
            Assert.That(((ObjectResult)missing).StatusCode, Is.EqualTo(StatusCodes.Status404NotFound));
        });
    }

    /// <summary>
    /// The scope options are returned as the service gives them.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ScopeOptions_Returns200()
    {
        this.service
            .Setup(s => s.GetScopeOptionsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BlockScopeOptions(["a.local"], ["en-US", "nl-NL"]));

        IActionResult result = await new GetBlockScopeOptionsController(this.service.Object, this.state.Object).GetBlockScopeOptions(CancellationToken.None);

        var model = (BlockScopeOptionsResponseModel)((OkObjectResult)result).Value!;
        Assert.Multiple(() =>
        {
            Assert.That(model.Domains, Is.EqualTo(new[] { "a.local" }));
            Assert.That(model.Cultures, Is.EqualTo(new[] { "en-US", "nl-NL" }));
        });
    }

    private GetBlockRulesController ListController() => new(this.service.Object, this.state.Object);

    private CreateBlockRuleController CreateController() => new(this.service.Object, this.state.Object);

    private UpdateBlockRuleController UpdateController() => new(this.service.Object, this.state.Object);

    private void SetupUpdate(CreateBlockRuleOutcome outcome) =>
        this.service
            .Setup(s => s.UpdateAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(outcome);
}
