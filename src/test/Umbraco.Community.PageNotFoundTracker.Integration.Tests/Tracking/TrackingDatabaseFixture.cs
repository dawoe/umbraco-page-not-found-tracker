// Copyright (c) Dave Woestenborghs and contributors. Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Umbraco.Community.PageNotFoundTracker.Core.Tracking;
using Umbraco.Community.PageNotFoundTracker.Infrastructure.Tracking;
using Umbraco.Community.PageNotFoundTracker.Persistence.Sqlite;
using Umbraco.Community.PageNotFoundTracker.Persistence.SqlServer;

namespace Umbraco.Community.PageNotFoundTracker.Integration.Tests.Tracking;

/// <summary>
/// Base for tests that run against a real tracking database, once per <see cref="TrackingDatabase"/>. Each fixture
/// gets its own migrated database, and can create extra empty ones; all of them are removed in the one-time teardown,
/// also when a test failed. The tests use only the package's <see cref="PageNotFoundTrackerDbContext"/>, not Umbraco.
/// </summary>
[TestFixtureSource(typeof(TrackingDatabaseFixture), nameof(Databases))]
internal abstract class TrackingDatabaseFixture
{
    private readonly List<Func<ValueTask>> cleanups = [];
    private string? serverConnectionString;

    /// <summary>
    /// Initializes a new instance of the <see cref="TrackingDatabaseFixture"/> class.
    /// </summary>
    /// <param name="database">The database to run on.</param>
    protected TrackingDatabaseFixture(TrackingDatabase database)
    {
        this.Database = database;
        this.Provider = database == TrackingDatabase.Sqlite
            ? new SqliteTrackingDatabaseProvider()
            : new SqlServerTrackingDatabaseProvider();
    }

    /// <summary>
    /// Gets the databases every derived fixture runs on.
    /// </summary>
    public static TrackingDatabase[] Databases { get; } = [TrackingDatabase.Sqlite, TrackingDatabase.SqlServer];

    /// <summary>
    /// Gets the database this fixture instance runs on.
    /// </summary>
    protected TrackingDatabase Database { get; }

    /// <summary>
    /// Gets the provider of <see cref="Database"/>.
    /// </summary>
    protected ITrackingDatabaseProvider Provider { get; }

    /// <summary>
    /// Gets the connection string of the fixture's migrated database.
    /// </summary>
    protected string ConnectionString { get; private set; } = string.Empty;

    /// <summary>
    /// Creates and migrates the fixture's database. Ignores the fixture on SQL Server when no server is configured.
    /// </summary>
    /// <returns>A task.</returns>
    [OneTimeSetUp]
    public async Task CreateFixtureDatabaseAsync()
    {
        if (this.Database == TrackingDatabase.SqlServer)
        {
            this.serverConnectionString = SqlServerTestDatabase.GetServerConnectionString();
            if (this.serverConnectionString is null)
            {
                if (SqlServerTestDatabase.IsRequired())
                {
                    Assert.Fail(
                        $"{SqlServerTestDatabase.RequireVariable} is true but {SqlServerTestDatabase.ConnectionVariable} is not set: " +
                        "the SQL Server tracking tests would be skipped. Check the SQL Server service and the test step's environment.");
                }

                Assert.Ignore("Set PNFT_SQLSERVER_CONNECTION to run the SQL Server tracking tests (see CLAUDE.md).");
            }
        }

        this.ConnectionString = await this.CreateEmptyDatabaseAsync();
        await using PageNotFoundTrackerDbContext dbContext = this.CreateContext(this.ConnectionString);
        await dbContext.Database.MigrateAsync();
    }

    /// <summary>
    /// Removes every database the fixture created.
    /// </summary>
    /// <returns>A task.</returns>
    [OneTimeTearDown]
    public async Task RemoveDatabasesAsync()
    {
        List<Exception> errors = [];
        foreach (Func<ValueTask> cleanup in this.cleanups)
        {
            try
            {
                await cleanup();
            }
            catch (Exception ex)
            {
                errors.Add(ex);
            }
        }

        this.cleanups.Clear();
        if (errors.Count > 0)
        {
            throw new AggregateException("Removing the tracking test databases failed.", errors);
        }
    }

    /// <summary>
    /// Creates a new, empty (not migrated) database that is removed in the one-time teardown.
    /// </summary>
    /// <returns>The connection string of the database.</returns>
    protected async Task<string> CreateEmptyDatabaseAsync()
    {
        if (this.Database == TrackingDatabase.SqlServer)
        {
            SqlServerTestDatabase database = await SqlServerTestDatabase.CreateAsync(this.serverConnectionString!);
            this.cleanups.Add(database.DisposeAsync);
            return database.ConnectionString;
        }

        string path = Path.Combine(Path.GetTempPath(), $"pnft_test_{Guid.NewGuid():N}.sqlite.db");
        this.cleanups.Add(() =>
        {
            foreach (string file in new[] { path, path + "-wal", path + "-shm", path + "-journal" })
            {
                File.Delete(file);
            }

            return ValueTask.CompletedTask;
        });

        // No pooling, so no connection keeps the file open after the tests.
        return new SqliteConnectionStringBuilder { DataSource = path, Pooling = false }.ConnectionString;
    }

    /// <summary>
    /// Creates a context on a database, configured by the fixture's provider as the package configures it.
    /// </summary>
    /// <param name="connectionString">The connection string.</param>
    /// <returns>The context; the caller disposes it.</returns>
    protected PageNotFoundTrackerDbContext CreateContext(string connectionString)
    {
        var options = new DbContextOptionsBuilder<PageNotFoundTrackerDbContext>();
        this.Provider.ConfigureDbContext(options, connectionString);
        return new PageNotFoundTrackerDbContext(options.Options);
    }

    /// <summary>
    /// Creates a context on the fixture's migrated database.
    /// </summary>
    /// <returns>The context; the caller disposes it.</returns>
    protected PageNotFoundTrackerDbContext CreateContext() => this.CreateContext(this.ConnectionString);

    /// <summary>
    /// Lists the tables of a database (on SQL Server only those in the <c>dbo</c> schema).
    /// </summary>
    /// <param name="dbContext">A context on the database.</param>
    /// <returns>The table names.</returns>
    protected async Task<List<string>> GetTableNamesAsync(PageNotFoundTrackerDbContext dbContext)
    {
        ArgumentNullException.ThrowIfNull(dbContext);

        string sql = this.Database == TrackingDatabase.Sqlite
            ? "SELECT name AS Value FROM sqlite_master WHERE type = 'table'"
            : "SELECT TABLE_NAME AS Value FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA = 'dbo'";
        return await dbContext.Database.SqlQueryRaw<string>(sql).ToListAsync();
    }
}
