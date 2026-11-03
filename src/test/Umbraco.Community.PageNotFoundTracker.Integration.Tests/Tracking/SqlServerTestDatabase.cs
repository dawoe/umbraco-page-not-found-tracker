// Copyright (c) Dave Woestenborghs and contributors. Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Data.SqlClient;

namespace Umbraco.Community.PageNotFoundTracker.Integration.Tests.Tracking;

/// <summary>
/// A throwaway SQL Server database named <c>pnft_test_&lt;guid&gt;</c>, created on the server from
/// <see cref="ConnectionVariable"/> and dropped on dispose. It never touches any other database.
/// </summary>
internal sealed class SqlServerTestDatabase : IAsyncDisposable
{
    /// <summary>
    /// The environment variable with the server-level connection string (no <c>Database=</c>).
    /// </summary>
    public const string ConnectionVariable = "PNFT_SQLSERVER_CONNECTION";

    /// <summary>
    /// The environment variable that, when <c>true</c> (CI), makes a missing <see cref="ConnectionVariable"/> a failure
    /// instead of a skip.
    /// </summary>
    public const string RequireVariable = "PNFT_REQUIRE_SQLSERVER";

    /// <summary>
    /// The prefix of every database this class creates; it only ever drops databases with this prefix.
    /// </summary>
    private const string NamePrefix = "pnft_test_";

    private readonly string serverConnectionString;

    private SqlServerTestDatabase(string serverConnectionString, string name)
    {
        this.serverConnectionString = serverConnectionString;
        this.Name = name;
        this.ConnectionString = new SqlConnectionStringBuilder(serverConnectionString) { InitialCatalog = name }.ConnectionString;
    }

    /// <summary>
    /// Gets the name of the database.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the connection string of the database.
    /// </summary>
    public string ConnectionString { get; }

    /// <summary>
    /// Gets the server-level connection string from <see cref="ConnectionVariable"/>.
    /// </summary>
    /// <returns>The connection string, or <c>null</c> when the variable is not set.</returns>
    public static string? GetServerConnectionString()
    {
        string? value = Environment.GetEnvironmentVariable(ConnectionVariable);
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    /// <summary>
    /// Gets a value indicating whether the SQL Server tests are required (<see cref="RequireVariable"/> is <c>true</c>).
    /// </summary>
    /// <returns><c>true</c> when a missing connection must fail the tests.</returns>
    public static bool IsRequired() =>
        bool.TryParse(Environment.GetEnvironmentVariable(RequireVariable), out bool required) && required;

    /// <summary>
    /// Creates a new empty database.
    /// </summary>
    /// <param name="serverConnectionString">The server-level connection string.</param>
    /// <returns>The database.</returns>
    public static async Task<SqlServerTestDatabase> CreateAsync(string serverConnectionString)
    {
        var builder = new SqlConnectionStringBuilder(serverConnectionString);
        if (!string.IsNullOrEmpty(builder.InitialCatalog))
        {
            throw new InvalidOperationException($"{ConnectionVariable} must be a server-level connection string without a database.");
        }

        var database = new SqlServerTestDatabase(serverConnectionString, NamePrefix + Guid.NewGuid().ToString("N"));
        await database.ExecuteOnMasterAsync($"CREATE DATABASE [{database.Name}]");
        return database;
    }

    /// <summary>
    /// Drops the database.
    /// </summary>
    /// <returns>A task.</returns>
    public async ValueTask DisposeAsync()
    {
        if (!this.Name.StartsWith(NamePrefix, StringComparison.Ordinal))
        {
            return;
        }

        // Pooled connections keep the database in use; release them before dropping it.
        SqlConnection.ClearAllPools();
        await this.ExecuteOnMasterAsync(
            $"IF DB_ID(N'{this.Name}') IS NOT NULL BEGIN ALTER DATABASE [{this.Name}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{this.Name}]; END");
    }

    private async Task ExecuteOnMasterAsync(string sql)
    {
        string master = new SqlConnectionStringBuilder(this.serverConnectionString) { InitialCatalog = "master", Pooling = false }.ConnectionString;
        await using var connection = new SqlConnection(master);
        await connection.OpenAsync();
        await using SqlCommand command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync();
    }
}
