// Copyright (c) Dave Woestenborghs and contributors. Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Umbraco.Community.PageNotFoundTracker.Core;
using Umbraco.Community.PageNotFoundTracker.Core.Configuration;
using Umbraco.Community.PageNotFoundTracker.Infrastructure.Dashboard;

namespace Umbraco.Community.PageNotFoundTracker.Infrastructure.Tests;

/// <summary>
/// Unit tests for the <see cref="DashboardAccess"/> class.
/// </summary>
[TestFixture]
internal sealed class DashboardAccessTests
{
    /// <summary>
    /// With the default settings admin and editor pass and writer does not.
    /// </summary>
    [Test]
    public void IsAllowed_DefaultSettings_AdminAndEditorPass_WriterFails()
    {
        var access = Create(new PageNotFoundTrackerSettings(), out _);

        Assert.Multiple(() =>
        {
            Assert.That(access.IsAllowed(["admin"]), Is.True);
            Assert.That(access.IsAllowed(["editor"]), Is.True);
            Assert.That(access.IsAllowed(["writer"]), Is.False);
        });
    }

    /// <summary>
    /// The admin group always passes, also when the setting leaves it out.
    /// </summary>
    [Test]
    public void IsAllowed_AdminAlwaysPasses_WhenSettingOmitsIt()
    {
        var access = Create(new PageNotFoundTrackerSettings { DashboardUserGroups = ["editor"] }, out _);

        Assert.That(access.IsAllowed(["admin"]), Is.True);
    }

    /// <summary>
    /// Alias matching ignores case.
    /// </summary>
    [Test]
    public void IsAllowed_IsCaseInsensitiveOnAlias()
    {
        var access = Create(new PageNotFoundTrackerSettings { DashboardUserGroups = ["Editor"] }, out _);

        Assert.That(access.IsAllowed(["editor"]), Is.True);
    }

    /// <summary>
    /// A user without groups fails.
    /// </summary>
    [Test]
    public void IsAllowed_NoGroups_Fails()
    {
        var access = Create(new PageNotFoundTrackerSettings(), out _);

        Assert.That(access.IsAllowed([]), Is.False);
    }

    /// <summary>
    /// A null or whitespace-only setting falls back to the defaults and logs one warning.
    /// </summary>
    /// <param name="case">Which invalid setting to use.</param>
    [TestCase("null")]
    [TestCase("blank")]
    public void Constructor_NullOrWhitespaceOnlySetting_FallsBackToDefaultsAndLogsOneWarning(string @case)
    {
        var settings = new PageNotFoundTrackerSettings
        {
            DashboardUserGroups = @case switch
            {
                "null" => null!,
                _ => [" ", string.Empty],
            },
        };

        var access = Create(settings, out var logger);

        Assert.Multiple(() =>
        {
            Assert.That(access.IsAllowed(["editor"]), Is.True);
            Assert.That(access.IsAllowed(["admin"]), Is.True);
            Assert.That(access.IsAllowed(["writer"]), Is.False);
        });
        VerifyWarnings(logger, Times.Once());
    }

    /// <summary>
    /// An empty list (nothing configured) uses the defaults without a warning.
    /// </summary>
    [Test]
    public void Constructor_EmptyList_UsesDefaultsWithoutWarning()
    {
        var access = Create(new PageNotFoundTrackerSettings { DashboardUserGroups = [] }, out var logger);

        Assert.Multiple(() =>
        {
            Assert.That(access.IsAllowed(["editor"]), Is.True);
            Assert.That(access.IsAllowed(["writer"]), Is.False);
        });
        VerifyWarnings(logger, Times.Never());
    }

    /// <summary>
    /// Configuration binding replaces the defaults instead of appending to them.
    /// </summary>
    [Test]
    public void Binding_ConfiguredGroups_RestrictBelowTheDefaults()
    {
        var access = CreateBound(new Dictionary<string, string?> { ["Umbraco:Community:PageNotFoundTracker:DashboardUserGroups:0"] = "writer" });

        Assert.Multiple(() =>
        {
            Assert.That(access.IsAllowed(["writer"]), Is.True);
            Assert.That(access.IsAllowed(["admin"]), Is.True);
            Assert.That(access.IsAllowed(["editor"]), Is.False);
        });
    }

    /// <summary>
    /// With nothing configured, binding leaves admin and editor allowed.
    /// </summary>
    [Test]
    public void Binding_NothingConfigured_AdminAndEditorPass()
    {
        var access = CreateBound(new Dictionary<string, string?>());

        Assert.Multiple(() =>
        {
            Assert.That(access.IsAllowed(["admin"]), Is.True);
            Assert.That(access.IsAllowed(["editor"]), Is.True);
            Assert.That(access.IsAllowed(["writer"]), Is.False);
        });
    }

    /// <summary>
    /// Unknown aliases are valid input: they are kept, admin still passes and nothing is logged.
    /// </summary>
    [Test]
    public void Constructor_UnknownAliasesOnly_AreKeptAndAdminStillPasses()
    {
        var access = Create(new PageNotFoundTrackerSettings { DashboardUserGroups = ["  nobody "] }, out var logger);

        Assert.Multiple(() =>
        {
            Assert.That(access.IsAllowed(["nobody"]), Is.True);
            Assert.That(access.IsAllowed(["admin"]), Is.True);
            Assert.That(access.IsAllowed(["editor"]), Is.False);
        });
        VerifyWarnings(logger, Times.Never());
    }

    /// <summary>
    /// A valid setting logs no warning.
    /// </summary>
    [Test]
    public void Constructor_ValidSetting_LogsNoWarning()
    {
        Create(new PageNotFoundTrackerSettings { DashboardUserGroups = ["admin", "editor", "writer"] }, out var logger);

        VerifyWarnings(logger, Times.Never());
    }

    private static DashboardAccess Create(PageNotFoundTrackerSettings settings, out Mock<ILogger<DashboardAccess>> logger)
    {
        logger = new Mock<ILogger<DashboardAccess>>();
        return new DashboardAccess(Options.Create(settings), logger.Object);
    }

    private static DashboardAccess CreateBound(Dictionary<string, string?> values)
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(values).Build();
        var services = new ServiceCollection();
        services.AddOptions<PageNotFoundTrackerSettings>().Bind(config.GetSection(Constants.SectionName));
        using var provider = services.BuildServiceProvider();
        return new DashboardAccess(
            provider.GetRequiredService<IOptions<PageNotFoundTrackerSettings>>(),
            new Mock<ILogger<DashboardAccess>>().Object);
    }

    private static void VerifyWarnings(Mock<ILogger<DashboardAccess>> logger, Times times) =>
        logger.Verify(
            l => l.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            times);
}
