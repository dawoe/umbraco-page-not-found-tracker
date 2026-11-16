// Copyright (c) Dave Woestenborghs and contributors. Licensed under the MIT License. See LICENSE in the project root for license information.

namespace Umbraco.Community.PageNotFoundTracker.BackOffice.Models;

/// <summary>
/// The answer to a successful delete tracked URLs request.
/// </summary>
public sealed class DeleteTrackedUrlsResponseModel
{
    /// <summary>
    /// Gets the number of tracked URLs that were deleted.
    /// </summary>
    public int Deleted { get; init; }
}
