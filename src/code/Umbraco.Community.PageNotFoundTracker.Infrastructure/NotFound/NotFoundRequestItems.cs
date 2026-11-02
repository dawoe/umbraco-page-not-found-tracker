// Copyright (c) Dave Woestenborghs and contributors. Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.AspNetCore.Http;

namespace Umbraco.Community.PageNotFoundTracker.Infrastructure.NotFound;

/// <summary>
/// The <see cref="HttpContext.Items"/> keys shared by the two 404 code paths (the last-chance finder for pages and the
/// middleware for files), so a request is recorded and rendered once in total.
/// </summary>
internal static class NotFoundRequestItems
{
    /// <summary>
    /// Set when the 404 of the request has been reported to <see cref="INotFoundRecorder"/>.
    /// </summary>
    public const string RecordedKey = "Umbraco.Community.PageNotFoundTracker.PageNotFoundRecorded";

    /// <summary>
    /// Set when the file middleware has started handling the request, so it never handles the same request twice.
    /// </summary>
    public const string FileHandledKey = "Umbraco.Community.PageNotFoundTracker.FileNotFoundHandled";

    /// <summary>
    /// Marks the request as recorded.
    /// </summary>
    /// <param name="httpContext">The HTTP context of the request.</param>
    /// <returns><c>true</c> when the caller must record the request; <c>false</c> when it was recorded already.</returns>
    public static bool TryMarkRecorded(HttpContext httpContext)
    {
        if (httpContext.Items.ContainsKey(RecordedKey))
        {
            return false;
        }

        httpContext.Items[RecordedKey] = true;
        return true;
    }
}
