# Database providers

The package keeps its tables (tracked URLs and block rules) in the Umbraco database, with its own Entity Framework Core
migrations. SQLite and SQL Server work out of the box. For any other database, add a **tracking database provider**.
Without one the site keeps working but tracking and blocking are off, and an error is logged at startup.

## The contract

`ITrackingDatabaseProvider` (namespace `Umbraco.Community.PageNotFoundTracker.Core.Tracking`):

| Member | Meaning |
|---|---|
| `string ProviderName` | Umbraco's name for the database provider, as in the connection string setting `ConnectionStrings:umbracoDbDSN_ProviderName` (for example `Microsoft.Data.Sqlite` or `Microsoft.Data.SqlClient`) |
| `void ConfigureDbContext(DbContextOptionsBuilder options, string connectionString)` | Configure EF Core for your database: call its `UseXxx`, set `MigrationsAssembly` to the assembly that holds your migrations and `MigrationsHistoryTable(TrackingConstants.MigrationsHistoryTable)` |
| `ITrackedUrlUpsert? Upsert` (optional) | A faster way to write hits. `null` (the default) uses the shared implementation that works on every provider |

## How the package uses it

When Umbraco has reached its running state, the package reads the provider name Umbraco uses, selects the provider with
that `ProviderName`, and runs `Database.MigrateAsync()` on `PageNotFoundTrackerDbContext` with it.

- **No provider for that name:** one log line names the provider (an error, or a warning when `TrackingEnabled` is
  `false`), tracking and blocking stay off, and the site keeps serving.
- **A failed migration:** an error is logged and tracking stays off until the site restarts.
- **Several providers for one name:** the **last registered** one is used. That is how you replace a built-in one.

The dashboards show *Tracking database unsupported or unavailable* in both failure cases.

## Migrations

Your provider must ship migrations for **all** tables of the package, generated against
`PageNotFoundTrackerDbContext` (it is public for this reason), in an assembly that you name in `MigrationsAssembly`.
Later versions of the package add tables, so a new migration may be needed when you update the package.
For example, the version with the automatic clean-up adds the migration `AddLastSeenIndex` (an index on the
`LastSeen` column of the tracked URLs, which the clean-up deletes by): add a migration to your project when you
update, and check it with `dotnet ef migrations has-pending-model-changes`.

Generate them like any EF Core migrations, in your own project, with a design-time factory
(`IDesignTimeDbContextFactory<PageNotFoundTrackerDbContext>`) that configures your provider:

```bash
dotnet ef migrations add InitialCreate --context PageNotFoundTrackerDbContext
```

The history table is `TrackingConstants.MigrationsHistoryTable`. Keep it, so the package finds an existing history.

## Example

A minimal provider. A real one calls its own `UseXxx` instead of `UseSqlite`:

<!-- sample: src/samples/Umbraco.Community.PageNotFoundTracker.Samples/DatabaseProviderSample.cs#db-provider-class -->
```csharp
/// <summary>
/// Sample tracking database provider. A real provider ships EF Core migrations for all package tables, generated
/// against <c>PageNotFoundTrackerDbContext</c> in its own assembly, as the built-in provider projects do.
/// </summary>
public sealed class DatabaseProviderSample : ITrackingDatabaseProvider
{
    /// <summary>
    /// Gets Umbraco's connection string provider name for your database.
    /// </summary>
    public string ProviderName => "My.Data.Provider";

    /// <inheritdoc />
    public void ConfigureDbContext(DbContextOptionsBuilder options, string connectionString) =>
        options.UseSqlite(connectionString, db => db // a real provider calls its own UseXxx here
            .MigrationsAssembly(typeof(DatabaseProviderSample).Assembly.GetName().Name)
            .MigrationsHistoryTable(TrackingConstants.MigrationsHistoryTable));
}
```

## Register it

Register it from a composer marked `[ComposeAfter(typeof(PageNotFoundTrackerComposer))]`. Providers are added with
`AddSingleton`, not `AddUnique` (see [the registration rule](README.md#the-registration-rule)):

<!-- sample: src/samples/Umbraco.Community.PageNotFoundTracker.Samples/DatabaseProviderSampleComposer.cs#db-provider-composer -->
```csharp
/// <summary>
/// Registers the sample provider after the package. Providers are added next to the built-in ones; the last
/// registration for a provider name wins.
/// </summary>
[ComposeAfter(typeof(PageNotFoundTrackerComposer))]
public sealed class DatabaseProviderSampleComposer : IComposer
{
    /// <inheritdoc />
    public void Compose(IUmbracoBuilder builder) =>
        builder.Services.AddSingleton<ITrackingDatabaseProvider, DatabaseProviderSample>();
}
```

## How the built-in providers are built

Use them as the reference:

- `Umbraco.Community.PageNotFoundTracker.Persistence.Sqlite` and `...Persistence.SqlServer` are two small projects.
  Each holds the provider class, an internal composer, a `Migrations` folder and a design-time factory.
- Their composers run *before* the package anchor, which is why a composer that runs after it replaces them.
- The SQLite provider does not set the journal mode: Umbraco already opens SQLite in WAL mode.

## What is guaranteed, and what is not

- A provider is selected by name only, once per start. A change needs a restart.
- Writes go through `ITrackedUrlUpsert`. If you override `Upsert`, return the hits you did **not** write (they stay
  queued and are retried), never count a hit twice, and do not throw on cancellation: return the unwritten hits.
  An implementation that throws counts as having written nothing, so a batching override must be all-or-nothing.
- Blocking and redirects use the same database context, so they need no extra provider code.
