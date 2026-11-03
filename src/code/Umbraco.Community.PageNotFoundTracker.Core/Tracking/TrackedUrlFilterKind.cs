// Copyright (c) Dave Woestenborghs and contributors. Licensed under the MIT License. See LICENSE in the project root for license information.

namespace Umbraco.Community.PageNotFoundTracker.Core.Tracking;

/// <summary>
/// How a <see cref="TrackedUrlFilter"/> selects rows.
/// </summary>
public enum TrackedUrlFilterKind
{
    /// <summary>No filtering (the default).</summary>
    Any = 0,

    /// <summary>Only rows whose value is the empty string.</summary>
    Empty,

    /// <summary>Only rows whose value equals the filter's value.</summary>
    Exactly,
}
