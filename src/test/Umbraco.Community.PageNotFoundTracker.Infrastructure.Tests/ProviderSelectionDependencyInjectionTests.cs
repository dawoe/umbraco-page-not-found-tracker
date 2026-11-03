// Copyright (c) Dave Woestenborghs and contributors. Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Umbraco.Cms.Core.Composing;
using Umbraco.Cms.Core.Configuration.Models;
using Umbraco.Cms.Core.DependencyInjection;
using Umbraco.Community.PageNotFoundTracker.Core.Tracking;
using Umbraco.Community.PageNotFoundTracker.Infrastructure.Tracking;
using Umbraco.Community.PageNotFoundTracker.Persistence.Sqlite;

namespace Umbraco.Community.PageNotFoundTracker.Infrastructure.Tests;

/// <summary>
/// Provider selection through the real service registrations: the package's context factory is configured by the
/// provider an implementer registered for Umbraco's provider name.
/// </summary>
[TestFixture]
internal sealed class ProviderSelectionDependencyInjectionTests
{
    private const string ConnectionString = "Data Source=:memory:";

    /// <summary>
    /// A provider registered for a custom provider name configures the context the package's factory creates.
    /// </summary>
    [Test]
    public void Custom_Provider_Configures_The_Context_Factory()
    {
        var fake = new FakeProvider("Custom.Provider");
        using ServiceProvider services = BuildServices("Custom.Provider", builder => builder.Services.AddSingleton<ITrackingDatabaseProvider>(fake));

        using PageNotFoundTrackerDbContext context = CreateContext(services);

        Assert.That(fake.ConfiguredWith, Is.EqualTo(new[] { ConnectionString }));
    }

    /// <summary>
    /// A provider registered after the built-in SQLite provider for the same name replaces it.
    /// </summary>
    [Test]
    public void Provider_Registered_After_Built_In_One_Replaces_It()
    {
        var fake = new FakeProvider("Microsoft.Data.Sqlite");
        using ServiceProvider services = BuildServices("Microsoft.Data.Sqlite", builder =>
        {
            new SqlitePersistenceComposer().Compose(builder);
            builder.Services.AddSingleton<ITrackingDatabaseProvider>(fake);
        });

        using PageNotFoundTrackerDbContext context = CreateContext(services);

        Assert.That(fake.ConfiguredWith, Is.EqualTo(new[] { ConnectionString }));
    }

    private static ServiceProvider BuildServices(string providerName, Action<IUmbracoBuilder> addProviders)
    {
        var serviceCollection = new ServiceCollection();
        var builder = new UmbracoBuilder(serviceCollection, new ConfigurationBuilder().Build(), new TypeLoader(Mock.Of<ITypeFinder>(), Mock.Of<ILogger<TypeLoader>>()));
        new InfrastructureComposer().Compose(builder);
        addProviders(builder);
        serviceCollection.AddLogging();
        serviceCollection.Configure<ConnectionStrings>(c =>
        {
            c.ConnectionString = ConnectionString;
            c.ProviderName = providerName;
        });
        return serviceCollection.BuildServiceProvider();
    }

    private static PageNotFoundTrackerDbContext CreateContext(IServiceProvider services) =>
        services.GetRequiredService<IDbContextFactory<PageNotFoundTrackerDbContext>>().CreateDbContext();

    private sealed class FakeProvider(string providerName) : ITrackingDatabaseProvider
    {
        public string ProviderName { get; } = providerName;

        public List<string> ConfiguredWith { get; } = [];

        public void ConfigureDbContext(DbContextOptionsBuilder options, string connectionString)
        {
            this.ConfiguredWith.Add(connectionString);
            options.UseSqlite(new SqliteConnection(connectionString));
        }
    }
}
