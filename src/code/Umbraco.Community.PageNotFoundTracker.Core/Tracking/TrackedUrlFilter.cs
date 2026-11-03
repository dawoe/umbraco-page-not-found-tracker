// Copyright (c) Dave Woestenborghs and contributors. Licensed under the MIT License. See LICENSE in the project root for license information.

namespace Umbraco.Community.PageNotFoundTracker.Core.Tracking;

/// <summary>
/// A filter on a column that is the empty string when there is no value (domain, culture). The default value is
/// <see cref="Any"/>; "empty" is a distinct choice, so a real value can never be mistaken for the empty marker.
/// </summary>
public readonly record struct TrackedUrlFilter
{
    /// <summary>
    /// Gets a filter that does not filter (the default).
    /// </summary>
    public static TrackedUrlFilter Any => default;

    /// <summary>
    /// Gets a filter that selects rows without a value.
    /// </summary>
    public static TrackedUrlFilter Empty => new() { Kind = TrackedUrlFilterKind.Empty };

    /// <summary>
    /// Gets how the filter selects rows.
    /// </summary>
    public TrackedUrlFilterKind Kind { get; private init; }

    /// <summary>
    /// Gets the value rows must have, only set for <see cref="TrackedUrlFilterKind.Exactly"/>.
    /// </summary>
    public string? Value { get; private init; }

    /// <summary>
    /// Creates a filter that selects rows with exactly this value.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>The filter.</returns>
    public static TrackedUrlFilter Exactly(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new TrackedUrlFilter { Kind = TrackedUrlFilterKind.Exactly, Value = value };
    }
}
