// Copyright (c) Dave Woestenborghs and contributors. Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.EntityFrameworkCore;
using Umbraco.Community.PageNotFoundTracker.Core.Tracking;
using Umbraco.Community.PageNotFoundTracker.Infrastructure.Tracking;

namespace Umbraco.Community.PageNotFoundTracker.Infrastructure.Tests;

/// <summary>
/// Unit tests for the <see cref="TrackingDatabaseProviderSelector"/> class.
/// </summary>
[TestFixture]
internal sealed class TrackingDatabaseProviderSelectorTests
{
    /// <summary>
    /// The provider registered for the name is selected.
    /// </summary>
    [Test]
    public void Select_Picks_Provider_By_Name()
    {
        var sqlite = new FakeProvider("Microsoft.Data.Sqlite");
        var sqlServer = new FakeProvider("Microsoft.Data.SqlClient");
        var selector = new TrackingDatabaseProviderSelector([sqlite, sqlServer]);

        Assert.That(selector.Select("Microsoft.Data.Sqlite"), Is.SameAs(sqlite));
    }

    /// <summary>
    /// The name is matched case-insensitively.
    /// </summary>
    [Test]
    public void Select_Is_Case_Insensitive()
    {
        var sqlite = new FakeProvider("Microsoft.Data.Sqlite");
        var selector = new TrackingDatabaseProviderSelector([sqlite]);

        Assert.That(selector.Select("Microsoft.Data.SQLite"), Is.SameAs(sqlite));
    }

    /// <summary>
    /// An unknown or missing name selects nothing.
    /// </summary>
    /// <param name="providerName">The provider name.</param>
    [TestCase("Npgsql")]
    [TestCase("")]
    [TestCase(null)]
    public void Select_Unknown_Returns_Null(string? providerName)
    {
        var selector = new TrackingDatabaseProviderSelector([new FakeProvider("Microsoft.Data.Sqlite")]);

        Assert.That(selector.Select(providerName), Is.Null);
    }

    /// <summary>
    /// With two providers for the same name the last registered wins, so a custom provider replaces a built-in one.
    /// </summary>
    [Test]
    public void Select_Returns_Last_Registered()
    {
        var builtIn = new FakeProvider("Microsoft.Data.Sqlite");
        var custom = new FakeProvider("microsoft.data.sqlite");
        var selector = new TrackingDatabaseProviderSelector([builtIn, custom]);

        Assert.That(selector.Select("Microsoft.Data.Sqlite"), Is.SameAs(custom));
    }

    private sealed class FakeProvider(string providerName) : ITrackingDatabaseProvider
    {
        public string ProviderName { get; } = providerName;

        public void ConfigureDbContext(DbContextOptionsBuilder options, string connectionString)
        {
        }
    }
}
