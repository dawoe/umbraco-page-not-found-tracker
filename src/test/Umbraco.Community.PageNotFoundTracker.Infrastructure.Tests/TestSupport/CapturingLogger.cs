// Copyright (c) Dave Woestenborghs and contributors. Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.Logging;

namespace Umbraco.Community.PageNotFoundTracker.Infrastructure.Tests.TestSupport;

/// <summary>
/// A logger that keeps warning and error messages.
/// </summary>
/// <typeparam name="T">The category type.</typeparam>
internal sealed class CapturingLogger<T> : ILogger<T>
{
    /// <summary>
    /// Gets the warning messages.
    /// </summary>
    public List<string> Warnings { get; } = [];

    /// <summary>
    /// Gets the error messages.
    /// </summary>
    public List<string> Errors { get; } = [];

    /// <inheritdoc />
    public IDisposable? BeginScope<TState>(TState state)
        where TState : notnull => null;

    /// <inheritdoc />
    public bool IsEnabled(LogLevel logLevel) => true;

    /// <inheritdoc />
    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        if (logLevel == LogLevel.Warning)
        {
            this.Warnings.Add(formatter(state, exception));
        }
        else if (logLevel == LogLevel.Error)
        {
            this.Errors.Add(formatter(state, exception));
        }
    }
}
