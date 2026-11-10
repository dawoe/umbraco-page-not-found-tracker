// Copyright (c) Dave Woestenborghs and contributors. Licensed under the MIT License. See LICENSE in the project root for license information.

namespace Umbraco.Community.PageNotFoundTracker.BackOffice.Models;

/// <summary>
/// The body of a delete tracked URLs request.
/// </summary>
public sealed class DeleteTrackedUrlsRequestModel
{
    /// <summary>
    /// Gets the ids of the tracked URLs to delete; 1 to 100 distinct ids.
    /// </summary>
    public int[]? Ids { get; init; }
}
