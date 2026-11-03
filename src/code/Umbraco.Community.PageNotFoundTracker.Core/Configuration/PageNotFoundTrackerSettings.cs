// Copyright (c) Dave Woestenborghs and contributors. Licensed under the MIT License. See LICENSE in the project root for license information.

namespace Umbraco.Community.PageNotFoundTracker.Core.Configuration;

/// <summary>
/// Settings of the Page Not Found Tracker, bound to the <see cref="Constants.SectionName"/> configuration section.
/// </summary>
public sealed class PageNotFoundTrackerSettings
{
    private const int DefaultChannelCapacity = 10000;

    private static readonly TimeSpan DefaultFlushInterval = TimeSpan.FromMinutes(1);

    private static readonly TimeSpan MinimumFlushInterval = TimeSpan.FromSeconds(1);

    /// <summary>
    /// Gets or sets the alias of the document type used for the 404 page. Empty by default, in which case no
    /// 404 page is resolved and Umbraco's default 404 behaviour applies.
    /// </summary>
    public string NotFoundPageDocumentTypeAlias { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a value indicating whether 404 requests are tracked. True by default.
    /// </summary>
    public bool TrackingEnabled { get; set; } = true;

    /// <summary>
    /// Gets or sets how often tracked hits are written to the database. One minute by default.
    /// Consumers use <see cref="EffectiveFlushInterval"/>.
    /// </summary>
    public TimeSpan FlushInterval { get; set; } = DefaultFlushInterval;

    /// <summary>
    /// Gets or sets a value indicating whether the query string is part of the tracked URL. False by default.
    /// </summary>
    public bool IncludeQueryString { get; set; }

    /// <summary>
    /// Gets or sets the capacity of the in-memory queue of hits waiting to be flushed (10000 by default).
    /// Consumers use <see cref="EffectiveChannelCapacity"/>.
    /// </summary>
    public int ChannelCapacity { get; set; } = DefaultChannelCapacity;

    /// <summary>
    /// Gets or sets the aliases of the user groups that may use the dashboard. <c>admin</c> and <c>editor</c> by
    /// default. The <c>admin</c> group always has access. An empty or blank list falls back to the default with a warning.
    /// </summary>
    public IList<string> DashboardUserGroups { get; set; } = ["admin", "editor"];

    /// <summary>
    /// Gets the flush interval to use: at least one second, and the default when the configured value is not positive.
    /// </summary>
    internal TimeSpan EffectiveFlushInterval
    {
        get
        {
            if (this.FlushInterval <= TimeSpan.Zero)
            {
                return DefaultFlushInterval;
            }

            return this.FlushInterval < MinimumFlushInterval ? MinimumFlushInterval : this.FlushInterval;
        }
    }

    /// <summary>
    /// Gets the channel capacity to use: the default when the configured value is below one.
    /// </summary>
    internal int EffectiveChannelCapacity => this.ChannelCapacity < 1 ? DefaultChannelCapacity : this.ChannelCapacity;
}
