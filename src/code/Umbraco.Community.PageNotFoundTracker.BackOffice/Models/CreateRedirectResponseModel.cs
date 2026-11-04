// Copyright (c) Dave Woestenborghs and contributors. Licensed under the MIT License. See LICENSE in the project root for license information.

namespace Umbraco.Community.PageNotFoundTracker.BackOffice.Models;

/// <summary>
/// The answer to a successful create redirect request.
/// </summary>
public sealed class CreateRedirectResponseModel
{
    /// <summary>
    /// Gets a value indicating whether the tracked URL was removed after the redirect was created.
    /// </summary>
    public bool RecordRemoved { get; init; }

    /// <summary>
    /// Gets a message for the editor, set when the tracked URL could not be removed.
    /// </summary>
    public string? Message { get; init; }
}
