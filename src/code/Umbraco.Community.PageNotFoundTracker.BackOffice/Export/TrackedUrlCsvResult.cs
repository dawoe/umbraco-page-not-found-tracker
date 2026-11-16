// Copyright (c) Dave Woestenborghs and contributors. Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Globalization;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Umbraco.Community.PageNotFoundTracker.Core.Tracking;

namespace Umbraco.Community.PageNotFoundTracker.BackOffice.Export;

/// <summary>
/// An action result that streams the tracked URLs to the response as CSV. It writes when the result executes, not in the
/// action: the Management API's result filters (such as <c>DisableBrowserCache</c>) set response headers after the action
/// returns, which fails once a body byte has been written.
/// </summary>
/// <param name="exporter">The tracked URL exporter.</param>
/// <param name="query">The export query.</param>
/// <param name="now">The moment of the export, for the file name.</param>
/// <param name="logger">The logger.</param>
internal sealed class TrackedUrlCsvResult(ITrackedUrlExporter exporter, TrackedUrlExportQuery query, DateTimeOffset now, ILogger logger) : IActionResult
{
    /// <summary>
    /// The number of rows buffered before they are written to the response and flushed.
    /// </summary>
    private const int FlushEveryRows = 500;

    private static readonly UTF8Encoding Utf8NoBom = new(false);

    /// <inheritdoc />
    public async Task ExecuteResultAsync(ActionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        HttpContext httpContext = context.HttpContext;
        HttpResponse response = httpContext.Response;
        CancellationToken cancellationToken = httpContext.RequestAborted;

        response.ContentType = "text/csv; charset=utf-8";
        response.Headers.ContentDisposition = string.Create(
            CultureInfo.InvariantCulture,
            $"attachment; filename=\"page-not-found-{now:yyyyMMdd-HHmmss}.csv\"");

        // From here on the status is fixed: a failure aborts the connection instead of ending as a short "successful" file.
        try
        {
            // The byte order mark and the header go out before the first read, so even a database that is down fails mid-response.
            await response.Body.WriteAsync(TrackedUrlCsvWriter.Preamble, cancellationToken);
            await response.Body.WriteAsync(Utf8NoBom.GetBytes(TrackedUrlCsvWriter.HeaderRow), cancellationToken);
            await response.Body.FlushAsync(cancellationToken);

            var buffer = new StringBuilder();
            int buffered = 0;
            await foreach (TrackedUrl row in exporter.StreamAsync(query, cancellationToken))
            {
                buffer.Append(TrackedUrlCsvWriter.FormatRow(row));
                if (++buffered >= FlushEveryRows)
                {
                    await WriteAndFlushAsync(response, buffer, cancellationToken);
                    buffered = 0;
                }
            }

            await WriteAndFlushAsync(response, buffer, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // The client went away: the database read has ended with it.
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "The tracked URL export failed after it had started; the connection is aborted.");
            httpContext.Abort();
        }
    }

    private static async Task WriteAndFlushAsync(HttpResponse response, StringBuilder buffer, CancellationToken cancellationToken)
    {
        if (buffer.Length > 0)
        {
            await response.Body.WriteAsync(Utf8NoBom.GetBytes(buffer.ToString()), cancellationToken);
            buffer.Clear();
        }

        await response.Body.FlushAsync(cancellationToken);
    }
}
