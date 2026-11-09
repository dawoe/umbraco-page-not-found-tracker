// Copyright (c) Dave Woestenborghs and contributors. Licensed under the MIT License. See LICENSE in the project root for license information.

namespace Umbraco.Community.PageNotFoundTracker.Core.Blocking;

/// <summary>
/// A rule that makes matching missing URLs answer with a fixed status code, stored in the
/// <see cref="BlockingConstants.TableName"/> table.
/// </summary>
public class BlockRule
{
    /// <summary>
    /// Gets or sets the identifier.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Gets or sets the normalised pattern (maximum 800 characters).
    /// </summary>
    public string Pattern { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the SHA-256 hash (lower-case hex, 64 characters) of the normalised pattern.
    /// </summary>
    public string PatternHash { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the pattern as entered by the editor (maximum 800 characters).
    /// </summary>
    public string DisplayPattern { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the response status code.
    /// </summary>
    public int StatusCode { get; set; }

    /// <summary>
    /// Gets or sets the domain the rule applies to, empty for all (maximum 255 characters).
    /// </summary>
    public string Domain { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the culture the rule applies to, empty for all (maximum 50 characters).
    /// </summary>
    public string Culture { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the UTC time the rule was created.
    /// </summary>
    public DateTime Created { get; set; }
}
