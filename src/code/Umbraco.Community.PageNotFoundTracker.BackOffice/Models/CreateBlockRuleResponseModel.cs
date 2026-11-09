// Copyright (c) Dave Woestenborghs and contributors. Licensed under the MIT License. See LICENSE in the project root for license information.

namespace Umbraco.Community.PageNotFoundTracker.BackOffice.Models;

/// <summary>
/// The answer to a successful create block rule request.
/// </summary>
public sealed class CreateBlockRuleResponseModel
{
    /// <summary>
    /// Gets a value indicating whether the tracked records the rule covers were removed.
    /// </summary>
    public bool RecordsRemoved { get; init; }

    /// <summary>
    /// Gets a message for the editor, or <c>null</c>.
    /// </summary>
    public string? Message { get; init; }
}
