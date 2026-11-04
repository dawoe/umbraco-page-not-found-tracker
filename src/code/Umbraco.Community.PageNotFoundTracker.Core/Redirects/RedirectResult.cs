// Copyright (c) Dave Woestenborghs and contributors. Licensed under the MIT License. See LICENSE in the project root for license information.

namespace Umbraco.Community.PageNotFoundTracker.Core.Redirects;

/// <summary>
/// The result of a redirect provider creating a redirect.
/// </summary>
/// <param name="Succeeded">Whether the redirect was created.</param>
/// <param name="Message">A message for the editor, used when the creation failed.</param>
public sealed record RedirectResult(bool Succeeded, string? Message)
{
    /// <summary>
    /// Creates a successful result.
    /// </summary>
    /// <returns>A successful result.</returns>
    public static RedirectResult Success() => new(true, null);

    /// <summary>
    /// Creates a failed result.
    /// </summary>
    /// <param name="message">The message for the editor.</param>
    /// <returns>A failed result.</returns>
    public static RedirectResult Failure(string message) => new(false, message);
}
