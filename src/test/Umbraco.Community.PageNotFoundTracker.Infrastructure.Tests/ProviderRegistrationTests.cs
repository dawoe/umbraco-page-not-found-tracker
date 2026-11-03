// Copyright (c) Dave Woestenborghs and contributors. Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Umbraco.Cms.Core.Composing;
using Umbraco.Cms.Core.DependencyInjection;
using Umbraco.Community.PageNotFoundTracker.Core.Tracking;
using Umbraco.Community.PageNotFoundTracker.Infrastructure.Tracking;
using Umbraco.Community.PageNotFoundTracker.Persistence.Sqlite;
using Umbraco.Community.PageNotFoundTracker.Persistence.SqlServer;
using EFCoreConstants = Umbraco.Cms.Persistence.EFCore.Constants;

namespace Umbraco.Community.PageNotFoundTracker.Infrastructure.Tests;

/// <summary>
/// Unit tests for the SQLite and SQL Server tracking database providers and their registration.
/// </summary>
[TestFixture]
internal sealed class ProviderRegistrationTests
{
    /// <summary>
    /// Each provider serves Umbraco's provider name.
    /// </summary>
    [Test]
    public void ProviderName_Matches_Umbraco_Provider_Name()
    {
        Assert.Multiple(() =>
        {
            Assert.That(new SqliteTrackingDatabaseProvider().ProviderName, Is.EqualTo(EFCoreConstants.ProviderNames.SQLLite));
            Assert.That(new SqlServerTrackingDatabaseProvider().ProviderName, Is.EqualTo(EFCoreConstants.ProviderNames.SQLServer));
        });
    }

    /// <summary>
    /// The SQLite provider configures its own migrations assembly and the package's history table.
    /// </summary>
    [Test]
    public void Sqlite_Configures_Migrations_Assembly_And_History_Table()
    {
        var extension = Configure(new SqliteTrackingDatabaseProvider(), "Data Source=:memory:");

        Assert.Multiple(() =>
        {
            Assert.That(extension.MigrationsAssembly, Is.EqualTo(typeof(SqliteTrackingDatabaseProvider).Assembly.GetName().Name));
            Assert.That(extension.MigrationsHistoryTableName, Is.EqualTo(TrackingConstants.MigrationsHistoryTable));
        });
    }

    /// <summary>
    /// The SQL Server provider configures its own migrations assembly and the package's history table in <c>dbo</c>.
    /// </summary>
    [Test]
    public void SqlServer_Configures_Migrations_Assembly_And_History_Table()
    {
        var extension = Configure(new SqlServerTrackingDatabaseProvider(), "Server=localhost;Database=Dummy;Integrated Security=true;TrustServerCertificate=true");

        Assert.Multiple(() =>
        {
            Assert.That(extension.MigrationsAssembly, Is.EqualTo(typeof(SqlServerTrackingDatabaseProvider).Assembly.GetName().Name));
            Assert.That(extension.MigrationsHistoryTableName, Is.EqualTo(TrackingConstants.MigrationsHistoryTable));
            Assert.That(extension.MigrationsHistoryTableSchema, Is.EqualTo("dbo"));
        });
    }

    /// <summary>
    /// Each provider assembly holds exactly one <c>InitialCreate</c> migration for the package's context.
    /// </summary>
    /// <param name="providerType">A type from the provider assembly.</param>
    [TestCase(typeof(SqliteTrackingDatabaseProvider))]
    [TestCase(typeof(SqlServerTrackingDatabaseProvider))]
    public void Provider_Assembly_Contains_One_InitialCreate_Migration(Type providerType)
    {
        ArgumentNullException.ThrowIfNull(providerType);

        var migrations = providerType.Assembly.GetTypes()
            .Where(t => typeof(Migration).IsAssignableFrom(t)
                && t.GetCustomAttribute<DbContextAttribute>()?.ContextType == typeof(PageNotFoundTrackerDbContext)
                && t.GetCustomAttribute<MigrationAttribute>()?.Id.EndsWith("_InitialCreate", StringComparison.Ordinal) == true)
            .ToList();

        Assert.That(migrations, Has.Count.EqualTo(1));
    }

    /// <summary>
    /// The composer of each provider adds the provider next to existing ones (Add, not TryAdd).
    /// </summary>
    /// <param name="composerType">The composer type.</param>
    /// <param name="providerType">The provider type.</param>
    [TestCase(typeof(SqlitePersistenceComposer), typeof(SqliteTrackingDatabaseProvider))]
    [TestCase(typeof(SqlServerPersistenceComposer), typeof(SqlServerTrackingDatabaseProvider))]
    public void Composer_Adds_Provider_Alongside_Existing_Registrations(Type composerType, Type providerType)
    {
        ArgumentNullException.ThrowIfNull(composerType);
        ArgumentNullException.ThrowIfNull(providerType);

        var serviceCollection = new ServiceCollection();
        serviceCollection.AddSingleton(Mock.Of<ITrackingDatabaseProvider>());
        var builder = new UmbracoBuilder(serviceCollection, new ConfigurationBuilder().Build(), new TypeLoader(Mock.Of<ITypeFinder>(), Mock.Of<ILogger<TypeLoader>>()));

        var composer = (IComposer)Activator.CreateInstance(composerType)!;
        composer.Compose(builder);

        var registrations = serviceCollection.Where(d => d.ServiceType == typeof(ITrackingDatabaseProvider)).ToList();
        Assert.Multiple(() =>
        {
            Assert.That(composerType.IsPublic, Is.True, "Public so implementers can use [ComposeAfter] on it.");
            Assert.That(composerType.Assembly, Is.SameAs(providerType.Assembly));
            Assert.That(registrations, Has.Count.EqualTo(2));
            Assert.That(registrations[1].ImplementationType, Is.EqualTo(providerType));
            Assert.That(registrations[1].Lifetime, Is.EqualTo(ServiceLifetime.Singleton));
        });
    }

    private static RelationalOptionsExtension Configure(ITrackingDatabaseProvider provider, string connectionString)
    {
        var builder = new DbContextOptionsBuilder<PageNotFoundTrackerDbContext>();
        provider.ConfigureDbContext(builder, connectionString);
        return RelationalOptionsExtension.Extract(builder.Options);
    }
}
