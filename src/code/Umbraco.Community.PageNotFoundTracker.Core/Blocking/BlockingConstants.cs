// Copyright (c) Dave Woestenborghs and contributors. Licensed under the MIT License. See LICENSE in the project root for license information.

namespace Umbraco.Community.PageNotFoundTracker.Core.Blocking;

/// <summary>
/// Constants for blocking rules.
/// </summary>
public static class BlockingConstants
{
    /// <summary>
    /// The name of the block rule table.
    /// </summary>
    public const string TableName = "pageNotFoundTrackerBlockRule";

    /// <summary>
    /// The lowest allowed response status code.
    /// </summary>
    public const int MinStatusCode = 400;

    /// <summary>
    /// The highest allowed response status code.
    /// </summary>
    public const int MaxStatusCode = 499;

    /// <summary>
    /// The default response status code.
    /// </summary>
    public const int DefaultStatusCode = 410;

    /// <summary>
    /// The maximum length of a normalised pattern.
    /// </summary>
    public const int MaxPatternLength = 800;

    /// <summary>
    /// The message when a rule was saved but the tracked records it covers could not be removed.
    /// </summary>
    public const string RecordsNotRemovedMessage = "The rule was saved, but the tracked URLs it covers could not be removed.";
}
