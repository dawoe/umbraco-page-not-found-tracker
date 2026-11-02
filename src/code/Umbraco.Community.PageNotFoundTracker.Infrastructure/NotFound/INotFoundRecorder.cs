// Copyright (c) Dave Woestenborghs and contributors. Licensed under the MIT License. See LICENSE in the project root for license information.

namespace Umbraco.Community.PageNotFoundTracker.Infrastructure.NotFound;

/// <summary>
/// Records requests that ended in a 404. Spec 3 (tracking) replaces the default no-op implementation.
/// </summary>
internal interface INotFoundRecorder
{
    /// <summary>
    /// Records a 404 occurrence.
    /// </summary>
    /// <param name="occurrence">The occurrence to record.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task that completes when the occurrence has been recorded.</returns>
    Task RecordAsync(NotFoundOccurrence occurrence, CancellationToken cancellationToken);
}
