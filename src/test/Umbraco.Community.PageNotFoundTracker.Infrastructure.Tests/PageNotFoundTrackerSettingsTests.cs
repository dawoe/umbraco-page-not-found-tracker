// Copyright (c) Dave Woestenborghs and contributors. Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.Configuration;
using Umbraco.Community.PageNotFoundTracker.Core.Configuration;

namespace Umbraco.Community.PageNotFoundTracker.Infrastructure.Tests;

/// <summary>
/// Unit tests for the <see cref="PageNotFoundTrackerSettings"/> class.
/// </summary>
[TestFixture]
internal sealed class PageNotFoundTrackerSettingsTests
{
    /// <summary>
    /// Tests that the 404 page document type alias is empty by default.
    /// </summary>
    [Test]
    public void NotFoundPageDocumentTypeAlias_Is_Empty_By_Default()
    {
        Assert.That(new PageNotFoundTrackerSettings().NotFoundPageDocumentTypeAlias, Is.Empty);
    }

    /// <summary>
    /// Tests the default values of the tracking settings.
    /// </summary>
    [Test]
    public void Tracking_Settings_Have_Expected_Defaults()
    {
        var settings = new PageNotFoundTrackerSettings();

        Assert.Multiple(() =>
        {
            Assert.That(settings.TrackingEnabled, Is.True);
            Assert.That(settings.FlushInterval, Is.EqualTo(TimeSpan.FromMinutes(1)));
            Assert.That(settings.IncludeQueryString, Is.False);
            Assert.That(settings.ChannelCapacity, Is.EqualTo(10000));
        });
    }

    /// <summary>
    /// Tests that the tracking settings bind from configuration.
    /// </summary>
    [Test]
    public void Tracking_Settings_Bind_From_Configuration()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Umbraco:Community:PageNotFoundTracker:FlushInterval"] = "00:00:30",
                ["Umbraco:Community:PageNotFoundTracker:ChannelCapacity"] = "500",
                ["Umbraco:Community:PageNotFoundTracker:TrackingEnabled"] = "false",
                ["Umbraco:Community:PageNotFoundTracker:IncludeQueryString"] = "true",
            })
            .Build();

        var settings = configuration.GetSection("Umbraco:Community:PageNotFoundTracker").Get<PageNotFoundTrackerSettings>()!;

        Assert.Multiple(() =>
        {
            Assert.That(settings.FlushInterval, Is.EqualTo(TimeSpan.FromSeconds(30)));
            Assert.That(settings.ChannelCapacity, Is.EqualTo(500));
            Assert.That(settings.TrackingEnabled, Is.False);
            Assert.That(settings.IncludeQueryString, Is.True);
        });
    }

    /// <summary>
    /// Tests that a flush interval below one second is raised to one second.
    /// </summary>
    [Test]
    public void EffectiveFlushInterval_Clamps_To_One_Second()
    {
        var settings = new PageNotFoundTrackerSettings { FlushInterval = TimeSpan.FromSeconds(0.2) };

        Assert.That(settings.EffectiveFlushInterval, Is.EqualTo(TimeSpan.FromSeconds(1)));
    }

    /// <summary>
    /// Tests that a non-positive flush interval falls back to the default.
    /// </summary>
    /// <param name="interval">The configured interval.</param>
    [TestCase("00:00:00")]
    [TestCase("-00:00:05")]
    public void EffectiveFlushInterval_Uses_Default_For_Non_Positive(string interval)
    {
        var settings = new PageNotFoundTrackerSettings { FlushInterval = TimeSpan.Parse(interval) };

        Assert.That(settings.EffectiveFlushInterval, Is.EqualTo(TimeSpan.FromMinutes(1)));
    }

    /// <summary>
    /// Tests that a channel capacity below one falls back to the default.
    /// </summary>
    /// <param name="capacity">The configured capacity.</param>
    [TestCase(0)]
    [TestCase(-1)]
    public void EffectiveChannelCapacity_Uses_Default_Below_One(int capacity)
    {
        var settings = new PageNotFoundTrackerSettings { ChannelCapacity = capacity };

        Assert.That(settings.EffectiveChannelCapacity, Is.EqualTo(10000));
    }

    /// <summary>
    /// Tests that a valid channel capacity is used as configured.
    /// </summary>
    [Test]
    public void EffectiveChannelCapacity_Keeps_Valid_Value()
    {
        var settings = new PageNotFoundTrackerSettings { ChannelCapacity = 500 };

        Assert.That(settings.EffectiveChannelCapacity, Is.EqualTo(500));
    }

    /// <summary>
    /// Tests that the clean-up is on by default and removes URLs not seen for 365 days.
    /// </summary>
    [Test]
    public void Cleanup_Settings_Have_Expected_Defaults()
    {
        var settings = new PageNotFoundTrackerSettings();

        Assert.Multiple(() =>
        {
            Assert.That(settings.CleanupEnabled, Is.True);
            Assert.That(settings.CleanupAfterDays, Is.EqualTo(365));
            Assert.That(settings.EffectiveCleanupAfterDays, Is.EqualTo(365));
        });
    }

    /// <summary>
    /// Tests that the clean-up settings bind from configuration.
    /// </summary>
    [Test]
    public void Cleanup_Settings_Bind_From_Configuration()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Umbraco:Community:PageNotFoundTracker:CleanupEnabled"] = "false",
                ["Umbraco:Community:PageNotFoundTracker:CleanupAfterDays"] = "30",
            })
            .Build();

        var settings = configuration.GetSection("Umbraco:Community:PageNotFoundTracker").Get<PageNotFoundTrackerSettings>()!;

        Assert.Multiple(() =>
        {
            Assert.That(settings.CleanupEnabled, Is.False);
            Assert.That(settings.CleanupAfterDays, Is.EqualTo(30));
        });
    }

    /// <summary>
    /// Tests that a clean-up age below one day falls back to the default.
    /// </summary>
    /// <param name="days">The configured number of days.</param>
    [TestCase(0)]
    [TestCase(-5)]
    public void EffectiveCleanupAfterDays_Uses_Default_Below_One(int days)
    {
        var settings = new PageNotFoundTrackerSettings { CleanupAfterDays = days };

        Assert.That(settings.EffectiveCleanupAfterDays, Is.EqualTo(365));
    }

    /// <summary>
    /// Tests that a valid clean-up age is used as configured.
    /// </summary>
    [Test]
    public void EffectiveCleanupAfterDays_Keeps_Valid_Value()
    {
        var settings = new PageNotFoundTrackerSettings { CleanupAfterDays = 30 };

        Assert.That(settings.EffectiveCleanupAfterDays, Is.EqualTo(30));
    }
}
