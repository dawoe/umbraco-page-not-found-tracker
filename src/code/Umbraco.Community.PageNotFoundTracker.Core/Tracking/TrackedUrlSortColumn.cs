// Copyright (c) Dave Woestenborghs and contributors. Licensed under the MIT License. See LICENSE in the project root for license information.

namespace Umbraco.Community.PageNotFoundTracker.Core.Tracking;

/// <summary>
/// The columns the tracked URL list can be sorted on.
/// </summary>
public enum TrackedUrlSortColumn
{
    /// <summary>The number of hits.</summary>
    Hits,

    /// <summary>The normalised URL.</summary>
    Url,

    /// <summary>The domain.</summary>
    Domain,

    /// <summary>The culture.</summary>
    Culture,

    /// <summary>Whether the request was for a file.</summary>
    IsFile,

    /// <summary>The time of the first hit.</summary>
    FirstSeen,

    /// <summary>The time of the last hit.</summary>
    LastSeen,
}
