// Copyright (c) Dave Woestenborghs and contributors. Licensed under the MIT License. See LICENSE in the project root for license information.

namespace Umbraco.Community.PageNotFoundTracker.Core.Redirects;

/// <summary>
/// The status of a redirect creation.
/// </summary>
public enum CreateRedirectStatus
{
    /// <summary>The redirect was created and the tracked URL removed.</summary>
    Created,

    /// <summary>The redirect was created, but the tracked URL could not be removed.</summary>
    CreatedRecordNotRemoved,

    /// <summary>The tracked URL does not exist.</summary>
    NotFound,

    /// <summary>The active provider cannot create a redirect for the tracked URL.</summary>
    NotSupported,

    /// <summary>The redirect could not be created.</summary>
    Failed,
}

/// <summary>
/// The outcome of <see cref="ITrackedUrlRedirectService.CreateAsync"/>.
/// </summary>
/// <param name="Status">The status.</param>
/// <param name="Message">A message for the editor, when there is one.</param>
public sealed record CreateRedirectOutcome(CreateRedirectStatus Status, string? Message);
