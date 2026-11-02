// Copyright (c) Dave Woestenborghs and contributors. Licensed under the MIT License. See LICENSE in the project root for license information.

namespace Umbraco.Community.PageNotFoundTracker.Infrastructure.NotFound;

/// <summary>
/// The default <see cref="INotFoundRecorder"/>, which records nothing.
/// </summary>
internal sealed class NoOpNotFoundRecorder : INotFoundRecorder
{
    /// <inheritdoc />
    public Task RecordAsync(NotFoundOccurrence occurrence, CancellationToken cancellationToken) => Task.CompletedTask;
}
