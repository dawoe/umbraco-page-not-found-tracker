// Copyright (c) Dave Woestenborghs and contributors. Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Runtime.CompilerServices;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using Moq;
using Umbraco.Cms.Core;
using Umbraco.Community.PageNotFoundTracker.BackOffice.Controllers;
using Umbraco.Community.PageNotFoundTracker.BackOffice.Export;
using Umbraco.Community.PageNotFoundTracker.Core.Tracking;

namespace Umbraco.Community.PageNotFoundTracker.BackOffice.Tests;

/// <summary>
/// Unit tests for <see cref="ExportTrackedUrlsController"/>.
/// </summary>
[TestFixture]
internal sealed class ExportTrackedUrlsControllerTests
{
    private static readonly DateTime Moment = new(2026, 11, 16, 10, 30, 0, DateTimeKind.Utc);

    private Mock<ITrackedUrlExporter> exporter = null!;
    private Mock<ITrackingPersistenceState> state = null!;
    private Mock<ILogger<ExportTrackedUrlsController>> logger = null!;
    private MemoryStream body = null!;
    private DefaultHttpContext httpContext = null!;
    private FakeLifetime lifetime = null!;
    private TrackedUrlExportQuery? lastQuery;
    private Func<CancellationToken, IAsyncEnumerable<TrackedUrl>> rows = null!;

    /// <summary>
    /// Creates available persistence, an exporter that streams the configured rows and a writable response.
    /// </summary>
    [SetUp]
    public void SetUp()
    {
        this.lastQuery = null;
        this.rows = token => Stream(token);
        this.state = new Mock<ITrackingPersistenceState>();
        this.state.SetupGet(s => s.Status).Returns(TrackingPersistenceStatus.Available);
        this.logger = new Mock<ILogger<ExportTrackedUrlsController>>();
        this.exporter = new Mock<ITrackedUrlExporter>();
        this.exporter
            .Setup(e => e.StreamAsync(It.IsAny<TrackedUrlExportQuery>(), It.IsAny<CancellationToken>()))
            .Callback<TrackedUrlExportQuery, CancellationToken>((query, _) => this.lastQuery = query)
            .Returns<TrackedUrlExportQuery, CancellationToken>((_, token) => this.rows(token));
        this.body = new MemoryStream();
        this.lifetime = new FakeLifetime();
        this.httpContext = new DefaultHttpContext();
        this.httpContext.Response.Body = this.body;
        this.httpContext.Features.Set<IHttpRequestLifetimeFeature>(this.lifetime);
    }

    /// <summary>
    /// A contradicting filter is a bad request and the exporter is never called.
    /// </summary>
    /// <param name="domain">The domain.</param>
    /// <param name="noDomain">Whether no domain is asked for.</param>
    /// <param name="culture">The culture.</param>
    /// <param name="noCulture">Whether no culture is asked for.</param>
    /// <returns>A task.</returns>
    [TestCase("a", true, null, false)]
    [TestCase(null, false, "en-US", true)]
    public async Task Export_ContradictingFilter_Returns400(string? domain, bool noDomain, string? culture, bool noCulture)
    {
        IActionResult result = await this.CreateController().ExportTrackedUrls(CancellationToken.None, domain: domain, noDomain: noDomain, culture: culture, noCulture: noCulture);

        Assert.That(result, Is.InstanceOf<ObjectResult>().With.Property("StatusCode").EqualTo(StatusCodes.Status400BadRequest));
        this.exporter.Verify(e => e.StreamAsync(It.IsAny<TrackedUrlExportQuery>(), It.IsAny<CancellationToken>()), Times.Never);
        Assert.That(this.body.Length, Is.Zero);
    }

    /// <summary>
    /// Unavailable persistence is a 503 decided before any byte is written.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task Export_PersistenceUnavailable_Returns503BeforeAnyByte()
    {
        this.state.SetupGet(s => s.Status).Returns(TrackingPersistenceStatus.Unavailable);

        IActionResult result = await this.CreateController().ExportTrackedUrls(CancellationToken.None);

        Assert.That(result, Is.InstanceOf<ObjectResult>().With.Property("StatusCode").EqualTo(StatusCodes.Status503ServiceUnavailable));
        Assert.That(this.body.Length, Is.Zero);
        this.exporter.Verify(e => e.StreamAsync(It.IsAny<TrackedUrlExportQuery>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// The file is the BOM, the header and one row per streamed tracked URL, with the CSV headers.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task Export_Rows_WritesBomHeaderAndRows()
    {
        TrackedUrl first = Row("/a");
        TrackedUrl second = Row("/b;c");
        this.rows = token => Stream(token, first, second);

        IActionResult result = await this.CreateController().ExportTrackedUrls(CancellationToken.None);
        await this.ExecuteAsync(result);

        byte[] written = this.body.ToArray();
        string text = new UTF8Encoding(false).GetString(written, 3, written.Length - 3);
        Assert.Multiple(() =>
        {
            Assert.That(result, Is.InstanceOf<TrackedUrlCsvResult>());
            Assert.That(written.Take(3), Is.EqualTo(new byte[] { 0xEF, 0xBB, 0xBF }));
            Assert.That(text, Is.EqualTo(TrackedUrlCsvWriter.HeaderRow + TrackedUrlCsvWriter.FormatRow(first) + TrackedUrlCsvWriter.FormatRow(second)));
            Assert.That(this.httpContext.Response.ContentType, Is.EqualTo("text/csv; charset=utf-8"));
            Assert.That(this.httpContext.Response.Headers.ContentDisposition.ToString(), Is.EqualTo("attachment; filename=\"page-not-found-20261116-103000.csv\""));
        });
    }

    /// <summary>
    /// No rows is a valid file: the BOM and the header only.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task Export_NoRows_WritesBomAndHeaderOnly()
    {
        IActionResult result = await this.CreateController().ExportTrackedUrls(CancellationToken.None);
        await this.ExecuteAsync(result);

        byte[] expected = [.. TrackedUrlCsvWriter.Preamble.ToArray(), .. new UTF8Encoding(false).GetBytes(TrackedUrlCsvWriter.HeaderRow)];
        Assert.That(result, Is.InstanceOf<TrackedUrlCsvResult>());
        Assert.That(this.body.ToArray(), Is.EqualTo(expected));
    }

    /// <summary>
    /// The query parameters become the export query.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task Export_PassesTheFilterToTheExporter()
    {
        IActionResult result = await this.CreateController().ExportTrackedUrls(
            CancellationToken.None,
            TrackedUrlSortColumn.Url,
            Direction.Ascending,
            "about",
            "localhost:44402/sitea",
            noDomain: false,
            culture: null,
            noCulture: true);
        await this.ExecuteAsync(result);

        Assert.That(
            this.lastQuery,
            Is.EqualTo(new TrackedUrlExportQuery(TrackedUrlSortColumn.Url, false, "about", TrackedUrlFilter.Exactly("localhost:44402/sitea"), TrackedUrlFilter.Empty)));
    }

    /// <summary>
    /// Without parameters the export is hits descending and unfiltered, like the list.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task Export_Defaults_HitsDescendingUnfiltered()
    {
        await this.ExecuteAsync(await this.CreateController().ExportTrackedUrls(CancellationToken.None));

        Assert.That(this.lastQuery, Is.EqualTo(new TrackedUrlExportQuery(TrackedUrlSortColumn.Hits, true, null, TrackedUrlFilter.Any, TrackedUrlFilter.Any)));
    }

    /// <summary>
    /// A failure after the first byte is logged and aborts the connection, so the client sees a failed download.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task Export_ExporterThrowsAfterFirstRow_LogsAndAbortsTheConnection()
    {
        this.rows = token => ThrowAfterFirstRow(token);

        await this.ExecuteAsync(await this.CreateController().ExportTrackedUrls(CancellationToken.None));

        Assert.That(this.lifetime.Aborted, Is.True);
        this.logger.Verify(
            l => l.Log(LogLevel.Error, It.IsAny<EventId>(), It.IsAny<It.IsAnyType>(), It.IsAny<Exception>(), It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    /// <summary>
    /// A client that goes away ends the read quietly: no abort, no error.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task Export_CancelledByClient_StopsReadingAndDoesNotAbort()
    {
        using var cts = new CancellationTokenSource();
        int produced = 0;
        this.rows = token => CancelAfterFirstRow(cts, token, () => produced++);

        this.httpContext.RequestAborted = cts.Token;
        IActionResult result = await this.CreateController().ExportTrackedUrls(CancellationToken.None);
        await this.ExecuteAsync(result);

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.InstanceOf<TrackedUrlCsvResult>());
            Assert.That(produced, Is.EqualTo(1));
            Assert.That(this.lifetime.Aborted, Is.False);
        });
        this.logger.Verify(
            l => l.Log(LogLevel.Error, It.IsAny<EventId>(), It.IsAny<It.IsAnyType>(), It.IsAny<Exception>(), It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Never);
    }

    private static TrackedUrl Row(string url) =>
        new()
        {
            Url = url,
            UrlHash = "h",
            DisplayUrl = url,
            RelativePath = url,
            HitCount = 1,
            FirstSeen = Moment,
            LastSeen = Moment,
        };

    private static async IAsyncEnumerable<TrackedUrl> Stream([EnumeratorCancellation] CancellationToken token, params TrackedUrl[] rows)
    {
        foreach (TrackedUrl row in rows)
        {
            token.ThrowIfCancellationRequested();
            yield return row;
        }

        await Task.CompletedTask;
    }

    private static async IAsyncEnumerable<TrackedUrl> ThrowAfterFirstRow([EnumeratorCancellation] CancellationToken token)
    {
        await Task.CompletedTask;
        token.ThrowIfCancellationRequested();
        yield return Row("/first");
        throw new InvalidOperationException("the database went away");
    }

    private static async IAsyncEnumerable<TrackedUrl> CancelAfterFirstRow(CancellationTokenSource cts, [EnumeratorCancellation] CancellationToken token, Action produced)
    {
        await Task.CompletedTask;
        produced();
        yield return Row("/first");
        await cts.CancelAsync();
        token.ThrowIfCancellationRequested();
        produced();
        yield return Row("/second");
    }

    private async Task ExecuteAsync(IActionResult result) =>
        await result.ExecuteResultAsync(new ActionContext(this.httpContext, new RouteData(), new ActionDescriptor()));

    private ExportTrackedUrlsController CreateController() =>
        new(this.exporter.Object, this.state.Object, new FixedTimeProvider(Moment), this.logger.Object)
        {
            ControllerContext = new ControllerContext { HttpContext = this.httpContext },
        };

    private sealed class FixedTimeProvider(DateTime utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(utcNow, TimeSpan.Zero);
    }

    private sealed class FakeLifetime : IHttpRequestLifetimeFeature
    {
        public bool Aborted { get; private set; }

        public CancellationToken RequestAborted { get; set; }

        public void Abort() => this.Aborted = true;
    }
}
