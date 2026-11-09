// Copyright (c) Dave Woestenborghs and contributors. Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;
using Umbraco.Community.PageNotFoundTracker.Core;
using Umbraco.Community.PageNotFoundTracker.Core.Configuration;

namespace Umbraco.Community.PageNotFoundTracker.Infrastructure.Tests.Docs;

/// <summary>
/// Keeps <c>docs/configuration.md</c> in step with <see cref="PageNotFoundTrackerSettings"/>.
/// </summary>
[TestFixture]
internal sealed class ConfigurationDocsTests
{
    /// <summary>
    /// Every public setting appears in the configuration page, so a new setting cannot ship undocumented.
    /// </summary>
    [Test]
    public void Settings_EveryProperty_IsDocumented()
    {
        var page = ReadConfigurationPage();
        var settings = typeof(PageNotFoundTrackerSettings)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(property => property.SetMethod?.IsPublic == true)
            .Select(property => property.Name)
            .ToList();

        var missing = settings.Where(name => !page.Contains($"`{name}`", StringComparison.Ordinal)).ToList();

        Assert.That(missing, Is.Empty, $"docs/configuration.md does not document: {string.Join(", ", missing)}");
    }

    /// <summary>
    /// The appsettings example on the page binds to the settings class, so a wrong name (an unknown key throws) or type fails here.
    /// </summary>
    [Test]
    public void Settings_ExampleAppsettings_BindsToSettingsClass()
    {
        var page = ReadConfigurationPage();
        var example = Regex.Match(page, "```json\r?\n(?<json>.*?)```", RegexOptions.Singleline);
        Assert.That(example.Success, Is.True, "docs/configuration.md has no json example block");

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(example.Groups["json"].Value));
        var configuration = new ConfigurationBuilder().AddJsonStream(stream).Build();

        var settings = configuration.GetSection(Constants.SectionName).Get<PageNotFoundTrackerSettings>(binder => binder.ErrorOnUnknownConfiguration = true);

        Assert.That(settings, Is.Not.Null, $"the example has no {Constants.SectionName} section");
        Assert.Multiple(() =>
        {
            Assert.That(settings!.NotFoundPageDocumentTypeAlias, Is.EqualTo("error"));
            Assert.That(settings.TrackingEnabled, Is.True);
            Assert.That(settings.FlushInterval, Is.EqualTo(TimeSpan.FromMinutes(5)));
            Assert.That(settings.IncludeQueryString, Is.True);
            Assert.That(settings.ChannelCapacity, Is.EqualTo(20000));
            Assert.That(settings.DashboardUserGroups, Is.EqualTo(new[] { "admin", "editor", "writer" }));
        });
    }

    private static string ReadConfigurationPage()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "src", "Umbraco.Community.PageNotFoundTracker.slnx")))
        {
            directory = directory.Parent;
        }

        Assert.That(directory, Is.Not.Null, "repository root not found above the test output folder");

        var path = Path.Combine(directory!.FullName, "docs", "configuration.md");
        Assert.That(File.Exists(path), Is.True, "docs/configuration.md does not exist");
        return File.ReadAllText(path);
    }
}
