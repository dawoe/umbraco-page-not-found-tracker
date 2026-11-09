# Retention and Clean-up of Tracked URLs Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** A daily Umbraco distributed background job removes tracked URLs whose `LastSeen` is more than `CleanupAfterDays` (default 365) days ago, on by default, on SQLite and SQL Server, then the public docs describe it.

**Architecture:** Two settings on `PageNotFoundTrackerSettings`. An internal `ITrackedUrlCleaner` (Infrastructure) deletes one batch per call with EF Core on `PageNotFoundTrackerDbContext`. `TrackedUrlCleanupJob : IDistributedBackgroundJob` (period one day, no cap) waits for the persistence gate, computes the cut-off once, loops batches, logs the total. A new index on `LastSeen` (migration `AddLastSeenIndex`, both providers) keeps each batch cheap. Umbraco's `umbracoDistributedJob` table makes one server run it at a time.

**Tech Stack:** .NET 10, Umbraco 17.8 (`Umbraco.Cms.Infrastructure.BackgroundJobs.IDistributedBackgroundJob`), EF Core 10 (SQLite, SQL Server), NUnit 4 + Moq + `Microsoft.Extensions.TimeProvider.Testing` (unit), Umbraco integration tests (NUnit 3.14).

**Spec:** `docs/superpowers/specs/2026-11-09-retention-cleanup-design.md`

## Global Constraints

- Branch `feature/7-retention-cleanup` (already created off `develop`, spec committed).
- Every C# file starts with `// Copyright (c) Dave Woestenborghs and contributors. Licensed under the MIT License. See LICENSE in the project root for license information.`; usings outside the namespace, file-scoped namespaces, braces always, XML docs on all public and internal members.
- `dotnet build` (from `src/`) has **zero analyzer warnings**; fix, never suppress.
- Settings: `CleanupEnabled` (bool, default `true`), `CleanupAfterDays` (int, default `365`, below 1 means 365 through internal `EffectiveCleanupAfterDays`). Read once at startup.
- Clean-up is independent of `TrackingEnabled`.
- A row is removed only when `LastSeen < cutoff` (strictly); `cutoff = TimeProvider UTC now - EffectiveCleanupAfterDays days`.
- Job: name `PageNotFoundTracker.TrackedUrlCleanup` (never change after release), period one day, **no cap per run**, batch size 1000, always registered (also when `CleanupEnabled` is false), never throws into Umbraco's scheduler, does not take `ITrackingFlusher.RunExclusiveAsync` and does not touch `TrackingQueue`.
- Block rules (`pageNotFoundTrackerBlockRule`) are never touched.
- Migrations only through `pwsh scripts/add-migration.ps1 -Name AddLastSeenIndex` (from the repo root); `pwsh scripts/check-pending-model-changes.ps1` must pass.
- `IDbContextFactory<PageNotFoundTrackerDbContext>` is resolved from `IServiceProvider` per call, never in a constructor (the pooled factory must not be resolved before the persistence gate opened).
- Docs rules (spec 7): one page owns each fact; `ConfigurationDocsTests`, `check-doc-samples.ps1`, `check-doc-links.ps1` pass; public pages never link to `CLAUDE.md` or `docs/superpowers/`.
- After running the test site: `git checkout -- test-sites/Website-V17/appsettings.json test-sites/Website-V17/umbraco/Data/*.sqlite.db`; never commit them.
- Commits end with `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`.

## Review Focus

1. A flush updates a row's `LastSeen` between the batch select and the batch delete: the row must survive (the delete re-applies `LastSeen < cutoff`). Pinned in Task 3 (`Delete_keeps_a_row_seen_again_after_it_was_selected`).
2. `CleanupAfterDays` set to a huge value (`int.MaxValue`): computing the cut-off must not throw `ArgumentOutOfRangeException`; nothing is deleted. Pinned in Task 4 (`Huge_age_deletes_nothing_and_does_not_throw`).
3. A row with `LastSeen` in the future (clock skew between servers) and a row exactly at the cut-off: both stay. Pinned in Task 3 (`Deletes_only_rows_strictly_older_than_the_cutoff`).
4. Host shutdown in the middle of a large run: the job stops between batches, logs the count removed so far and does not throw. Pinned in Task 4 (`Cancellation_stops_between_batches_and_logs_partial_count`).
5. Persistence still `Pending` when the first run fires (site just started): the job waits for the gate instead of skipping a whole day; `Unavailable` returns quietly. Pinned in Task 4 (`Waits_for_pending_persistence_then_runs`, `Unavailable_persistence_does_nothing`).

---

### Task 1: Settings and their configuration docs

**Files:**
- Modify: `src/code/Umbraco.Community.PageNotFoundTracker.Core/Configuration/PageNotFoundTrackerSettings.cs`
- Modify: `docs/configuration.md` (new "## Clean-up" section after "## Tracking"; both settings in the full example)
- Test: `src/test/Umbraco.Community.PageNotFoundTracker.Infrastructure.Tests/PageNotFoundTrackerSettingsTests.cs`
- Test: `src/test/Umbraco.Community.PageNotFoundTracker.Infrastructure.Tests/Docs/ConfigurationDocsTests.cs`

**Interfaces:**
- Produces: `public bool CleanupEnabled { get; set; } = true;`, `public int CleanupAfterDays { get; set; } = 365;`, `internal int EffectiveCleanupAfterDays` (365 when `CleanupAfterDays < 1`).

- [ ] **Step 1: Write the failing tests** in `PageNotFoundTrackerSettingsTests`:

```csharp
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

[TestCase(0)]
[TestCase(-5)]
public void EffectiveCleanupAfterDays_Uses_Default_Below_One(int days) =>
    Assert.That(new PageNotFoundTrackerSettings { CleanupAfterDays = days }.EffectiveCleanupAfterDays, Is.EqualTo(365));

[Test]
public void EffectiveCleanupAfterDays_Keeps_Valid_Value() =>
    Assert.That(new PageNotFoundTrackerSettings { CleanupAfterDays = 30 }.EffectiveCleanupAfterDays, Is.EqualTo(30));
```

Extend `Tracking_Settings_Bind_From_Configuration` (or add `Cleanup_Settings_Bind_From_Configuration`) with `CleanupEnabled=false`, `CleanupAfterDays=30` bound from in-memory configuration. In `ConfigurationDocsTests.Settings_ExampleAppsettings_BindsToSettingsClass` add:

```csharp
Assert.That(settings.CleanupEnabled, Is.True);
Assert.That(settings.CleanupAfterDays, Is.EqualTo(180));
```

- [ ] **Step 2: Run to verify they fail**

Run (from `src/`): `dotnet test test/Umbraco.Community.PageNotFoundTracker.Infrastructure.Tests --filter "FullyQualifiedName~PageNotFoundTrackerSettingsTests|FullyQualifiedName~ConfigurationDocsTests"`
Expected: build error (`CleanupEnabled` not defined).

- [ ] **Step 3: Add the two properties and `EffectiveCleanupAfterDays`** (private const `DefaultCleanupAfterDays = 365`, same pattern as `EffectiveChannelCapacity`; XML docs say clean-up is independent of `TrackingEnabled`).

- [ ] **Step 4: Update `docs/configuration.md`**: a "## Clean-up" section with a table in the page's format:

| Setting | Type | Default | Effect |
|---|---|---|---|
| `CleanupEnabled` | boolean | `true` | When `false`, tracked URLs are never removed automatically. |
| `CleanupAfterDays` | integer | `365` | A tracked URL is removed once its last hit is more than this many days ago. A value below 1 means the default. Lowering it on a large table makes the next daily run delete a large backlog, which delays Umbraco's other scheduled jobs (webhooks, scheduled publishing) on that server until it finishes: lower it in steps. |

Then one line linking to `how-it-works.md#clean-up` for how it runs (the anchor is created in Task 5; the link check runs in Task 5). Add `"CleanupEnabled": true,` and `"CleanupAfterDays": 180` to the full example `json` block.

- [ ] **Step 5: Run to verify they pass** (same command). Expected: PASS.

- [ ] **Step 6: Commit**

```bash
git add src/code/Umbraco.Community.PageNotFoundTracker.Core/Configuration/PageNotFoundTrackerSettings.cs docs/configuration.md src/test/Umbraco.Community.PageNotFoundTracker.Infrastructure.Tests/PageNotFoundTrackerSettingsTests.cs src/test/Umbraco.Community.PageNotFoundTracker.Infrastructure.Tests/Docs/ConfigurationDocsTests.cs
git commit -m "feat: add clean-up settings"
```

---

### Task 2: Index on LastSeen

**Files:**
- Modify: `src/code/Umbraco.Community.PageNotFoundTracker.Infrastructure/Tracking/PageNotFoundTrackerDbContext.cs` (after `entity.HasIndex(e => e.RelativePath);`)
- Create (generated): `src/code/Umbraco.Community.PageNotFoundTracker.Persistence.Sqlite/Migrations/<timestamp>_AddLastSeenIndex.cs` + `.Designer.cs`, updated snapshot; same in `.Persistence.SqlServer/Migrations/`
- Test: `src/test/Umbraco.Community.PageNotFoundTracker.Integration.Tests/Retention/LastSeenIndexMigrationTests.cs`

**Interfaces:**
- Produces: index `IX_pageNotFoundTrackerUrl_LastSeen` (EF default name) on both providers.

- [ ] **Step 1: Write the failing test** `LastSeenIndexMigrationTests : TrackingDatabaseFixture` (constructor `(TrackingDatabase database) : base(database)`, like `BlockRuleMigrationTests`):

```csharp
[Test]
public async Task Migration_creates_the_last_seen_index()
{
    await using PageNotFoundTrackerDbContext dbContext = this.CreateContext();
    string sql = this.Database == TrackingDatabase.Sqlite
        ? "SELECT COUNT(*) AS \"Value\" FROM sqlite_master WHERE type = 'index' AND name = 'IX_pageNotFoundTrackerUrl_LastSeen'"
        : "SELECT COUNT(*) AS [Value] FROM sys.indexes WHERE name = 'IX_pageNotFoundTrackerUrl_LastSeen'";
    int count = await dbContext.Database.SqlQueryRaw<int>(sql).SingleAsync();
    Assert.That(count, Is.EqualTo(1));
}
```

- [ ] **Step 2: Run to verify it fails**

Run (from `src/`): `dotnet test test/Umbraco.Community.PageNotFoundTracker.Integration.Tests --filter "FullyQualifiedName~LastSeenIndexMigrationTests"`
Expected: FAIL, count 0 (SQL Server case ignored unless `PNFT_SQLSERVER_CONNECTION` is set; set it if a SQL Server is available, see CLAUDE.md).

- [ ] **Step 3: Add `entity.HasIndex(e => e.LastSeen);`**, then from the repo root: `pwsh scripts/add-migration.ps1 -Name AddLastSeenIndex`.

- [ ] **Step 4: Verify**

Run: `pwsh scripts/check-pending-model-changes.ps1` → no pending changes for either provider; the Step 2 test command → PASS; `dotnet build` (from `src/`) → 0 warnings.

- [ ] **Step 5: Commit**

```bash
git add src/code/Umbraco.Community.PageNotFoundTracker.Infrastructure/Tracking/PageNotFoundTrackerDbContext.cs src/code/Umbraco.Community.PageNotFoundTracker.Persistence.Sqlite/Migrations src/code/Umbraco.Community.PageNotFoundTracker.Persistence.SqlServer/Migrations src/test/Umbraco.Community.PageNotFoundTracker.Integration.Tests/Retention/LastSeenIndexMigrationTests.cs
git commit -m "feat: index tracked URLs on last seen"
```

---

### Task 3: Batch delete (`TrackedUrlCleaner`)

**Files:**
- Create: `src/code/Umbraco.Community.PageNotFoundTracker.Infrastructure/Retention/ITrackedUrlCleaner.cs`
- Create: `src/code/Umbraco.Community.PageNotFoundTracker.Infrastructure/Retention/TrackedUrlCleaner.cs`
- Test: `src/test/Umbraco.Community.PageNotFoundTracker.Integration.Tests/Retention/TrackedUrlCleanerTests.cs`

**Interfaces:**
- Consumes: Task 2's index (performance only).
- Produces (internal):
  - `interface ITrackedUrlCleaner { Task<int> DeleteBatchAsync(DateTime cutoff, int batchSize, CancellationToken cancellationToken); }` — deletes at most `batchSize` rows with `LastSeen < cutoff`, returns the number deleted.
  - `sealed class TrackedUrlCleaner(IServiceProvider serviceProvider) : ITrackedUrlCleaner` with two internal steps used by `DeleteBatchAsync` and by the tests:
    - `internal Task<IReadOnlyList<int>> SelectBatchAsync(DateTime cutoff, int batchSize, CancellationToken ct)`: ids of rows with `LastSeen < cutoff`, ordered by `Id`, `Take(batchSize)`.
    - `internal Task<int> DeleteAsync(IReadOnlyCollection<int> ids, DateTime cutoff, CancellationToken ct)`: `Where(r => ids.Contains(r.Id) && r.LastSeen < cutoff).ExecuteDeleteAsync(ct)`; returns 0 without a query for an empty list.
  - Each call creates its own context from `IDbContextFactory<PageNotFoundTrackerDbContext>` resolved from `serviceProvider` at call time. Keep `batchSize` ≤ 1000 (SQL Server's 2100 parameter limit if EF expands the id list into parameters).

- [ ] **Step 1: Write the failing tests** (`TrackedUrlCleanerTests : TrackingDatabaseFixture`; build the cleaner on a `ServiceCollection` with a private `TestContextFactory` as `TrackedUrlRemoverTests` does; seed rows directly through `CreateContext()`; a private helper `Row(string url, DateTime lastSeen)` builds a `TrackedUrl` with a unique `UrlHash` per url; `Cutoff = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)`). Clear the table in `[SetUp]` (the fixture database is shared by the fixture's tests).

```csharp
[Test]
public async Task Deletes_only_rows_strictly_older_than_the_cutoff()
{
    await this.SeedAsync(
        Row("/old", Cutoff.AddTicks(-10)),          // older: removed
        Row("/at-cutoff", Cutoff),                    // exactly at the cut-off: stays
        Row("/newer", Cutoff.AddDays(1)),             // stays
        Row("/future", Cutoff.AddYears(5)));          // clock skew: stays
    int deleted = await this.cleaner.DeleteBatchAsync(Cutoff, 1000, CancellationToken.None);
    List<string> left = await this.UrlsAsync(); // NUnit 3.14 here: no async Assert.Multiple
    Assert.Multiple(() =>
    {
        Assert.That(deleted, Is.EqualTo(1));
        Assert.That(left, Is.EquivalentTo(new[] { "/at-cutoff", "/newer", "/future" }));
    });
}

[Test]
public async Task Deletes_at_most_one_batch_per_call()
{
    await this.SeedAsync(Enumerable.Range(0, 2500).Select(i => Row($"/bulk-{i}", Cutoff.AddDays(-1))).ToArray());
    int[] batches =
    [
        await this.cleaner.DeleteBatchAsync(Cutoff, 1000, CancellationToken.None),
        await this.cleaner.DeleteBatchAsync(Cutoff, 1000, CancellationToken.None),
        await this.cleaner.DeleteBatchAsync(Cutoff, 1000, CancellationToken.None),
        await this.cleaner.DeleteBatchAsync(Cutoff, 1000, CancellationToken.None),
    ];
    Assert.That(batches, Is.EqualTo(new[] { 1000, 1000, 500, 0 }));
    Assert.That(await this.UrlsAsync(), Is.Empty);
}

[Test]
public async Task Delete_keeps_a_row_seen_again_after_it_was_selected()
{
    await this.SeedAsync(Row("/race", Cutoff.AddDays(-1)), Row("/gone", Cutoff.AddDays(-1)));
    IReadOnlyList<int> ids = await this.cleaner.SelectBatchAsync(Cutoff, 1000, CancellationToken.None);
    await this.SetLastSeenAsync("/race", Cutoff.AddDays(30)); // a flush between select and delete
    int deleted = await this.cleaner.DeleteAsync(ids.ToList(), Cutoff, CancellationToken.None);
    Assert.That(deleted, Is.EqualTo(1));
    Assert.That(await this.UrlsAsync(), Is.EqualTo(new[] { "/race" }));
}

[Test]
public async Task Block_rules_are_untouched()
{
    // seed one old tracked row and one BlockRule (Created long before the cut-off) through CreateContext()
    // after DeleteBatchAsync: tracked row gone, BlockRules.Count() == 1
}
```

- [ ] **Step 2: Run to verify they fail**

Run (from `src/`): `dotnet test test/Umbraco.Community.PageNotFoundTracker.Integration.Tests --filter "FullyQualifiedName~TrackedUrlCleanerTests"`
Expected: build error (`TrackedUrlCleaner` not defined).

- [ ] **Step 3: Implement** `ITrackedUrlCleaner` and `TrackedUrlCleaner` as specified in Interfaces. `DeleteBatchAsync` = `SelectBatchAsync` then `DeleteAsync`.

- [ ] **Step 4: Run to verify they pass** (same command, SQLite; and with `PNFT_SQLSERVER_CONNECTION` set when a SQL Server is available). Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/code/Umbraco.Community.PageNotFoundTracker.Infrastructure/Retention src/test/Umbraco.Community.PageNotFoundTracker.Integration.Tests/Retention/TrackedUrlCleanerTests.cs
git commit -m "feat: delete old tracked URLs in batches"
```

---

### Task 4: The clean-up job and its registration

**Files:**
- Create: `src/code/Umbraco.Community.PageNotFoundTracker.Infrastructure/Retention/TrackedUrlCleanupJob.cs`
- Modify: `src/code/Umbraco.Community.PageNotFoundTracker.Infrastructure/Extensions/UmbracoBuilderExtensions.cs` (a private `AddRetention(IServiceCollection services)` called from `AddPageNotFoundTracker`)
- Test: `src/test/Umbraco.Community.PageNotFoundTracker.Infrastructure.Tests/Retention/TrackedUrlCleanupJobTests.cs`
- Test: `src/test/Umbraco.Community.PageNotFoundTracker.Infrastructure.Tests/InfrastructureComposerTests.cs`
- Test: `src/test/Umbraco.Community.PageNotFoundTracker.Integration.Tests/Retention/CleanupJobRegistrationTests.cs`

**Interfaces:**
- Consumes: `ITrackedUrlCleaner.DeleteBatchAsync(DateTime, int, CancellationToken)` (Task 3), `PageNotFoundTrackerSettings.CleanupEnabled` / `EffectiveCleanupAfterDays` (Task 1), `ITrackingPersistenceState.WaitUntilReadyAsync(CancellationToken)` (returns `false` when `Unavailable`).
- Produces: `internal sealed class TrackedUrlCleanupJob : IDistributedBackgroundJob` with
  - `internal const string JobName = "PageNotFoundTracker.TrackedUrlCleanup";`, `internal const int BatchSize = 1000;`
  - constructor `(IOptions<PageNotFoundTrackerSettings> settings, ITrackingPersistenceState persistenceState, ITrackedUrlCleaner cleaner, TimeProvider timeProvider, ILogger<TrackedUrlCleanupJob> logger)`
  - `Name => JobName`, `Period => TimeSpan.FromDays(1)`, `AlignToClock` left at the interface default
  - `Task ExecuteAsync()` → `ExecuteAsync(CancellationToken.None)`; `Task ExecuteAsync(CancellationToken cancellationToken)` does the work
  - Registration: `services.AddUnique<ITrackedUrlCleaner, TrackedUrlCleaner>();` and `services.AddSingleton<IDistributedBackgroundJob, TrackedUrlCleanupJob>();` (always, regardless of `CleanupEnabled`).

Run algorithm (`ExecuteAsync(ct)`):
1. `!CleanupEnabled` → debug "Clean-up of tracked 404 URLs is switched off.", return.
2. `if (!await persistenceState.WaitUntilReadyAsync(ct)) return;` (no log; `OperationCanceledException` from the wait → return quietly).
3. Cut-off: `now = timeProvider.GetUtcNow().UtcDateTime`; if `EffectiveCleanupAfterDays` exceeds `(now - DateTime.MinValue).TotalDays` use `DateTime.MinValue` (deletes nothing), else `now.AddDays(-days)`. Use `DateTimeKind.Utc`.
4. Loop: `removed += await cleaner.DeleteBatchAsync(cutoff, BatchSize, ct)` until a batch returns fewer than `BatchSize`; check `ct` between batches.
5. Log: removed > 0 → Information "Removed {Count} tracked 404 URLs last seen before {Cutoff}."; 0 → Debug, same template.
6. `OperationCanceledException` when `ct.IsCancellationRequested` → Information (Debug when 0) "Clean-up of tracked 404 URLs stopped by shutdown after removing {Count}."; return.
7. Any other exception → Error with the exception, "Clean-up of tracked 404 URLs failed after removing {Count}."; return (never rethrow).

- [ ] **Step 1: Write the failing unit tests** (`Mock<ITrackedUrlCleaner>`, `FakeTimeProvider` at `2026-11-09T12:00:00Z`, `CapturingLogger<TrackedUrlCleanupJob>` from `TestSupport`, a real `TrackingPersistenceState` set with `SetAvailable()` / `SetUnavailable()` as the existing tests do):

```csharp
[Test] public async Task Disabled_does_nothing()
// CleanupEnabled=false → cleaner never called

[Test] public async Task Unavailable_persistence_does_nothing()
// state.SetUnavailable() → cleaner never called, no Warning/Error logged

[Test] public async Task Waits_for_pending_persistence_then_runs()
// state pending; start ExecuteAsync; cleaner not called yet; state.SetAvailable(); await → cleaner called once

[Test] public async Task Passes_cutoff_of_now_minus_configured_days()
// CleanupAfterDays=30 → DeleteBatchAsync(new DateTime(2026,10,10,12,0,0,DateTimeKind.Utc), 1000, _)

[Test] public async Task Loops_until_a_batch_is_not_full_and_logs_total()
// cleaner returns 1000, 1000, 3 → called 3 times; one Information entry containing "2003"

[Test] public async Task Nothing_due_logs_no_information()
// cleaner returns 0 → called once; no Information entry

[Test] public async Task Huge_age_deletes_nothing_and_does_not_throw()
// CleanupAfterDays=int.MaxValue → no exception; cleaner called with DateTime.MinValue (or not at all)

[Test] public async Task Cancellation_stops_between_batches_and_logs_partial_count()
// cleaner returns 1000 then cancels the token source and returns 1000; next call would throw OperationCanceledException
// → ExecuteAsync completes without throwing; log entry contains "2000"

[Test] public async Task Cleaner_failure_is_logged_and_swallowed()
// cleaner returns 1000 then throws InvalidOperationException → no throw; one Error entry with the exception, containing "1000"

[Test] public void Name_and_period_are_fixed()
// Name == "PageNotFoundTracker.TrackedUrlCleanup", Period == TimeSpan.FromDays(1)
```

In `InfrastructureComposerTests` add `Compose_Registers_Cleanup_Job`: one `IDistributedBackgroundJob` descriptor with `ImplementationType == typeof(TrackedUrlCleanupJob)` and `Lifetime == Singleton`, registered also when the configuration sets `CleanupEnabled=false` (if the existing tests build without configuration, asserting the descriptor exists is enough).

- [ ] **Step 2: Run to verify they fail**

Run (from `src/`): `dotnet test test/Umbraco.Community.PageNotFoundTracker.Infrastructure.Tests --filter "FullyQualifiedName~TrackedUrlCleanupJobTests|FullyQualifiedName~InfrastructureComposerTests"`
Expected: build error (`TrackedUrlCleanupJob` not defined).

- [ ] **Step 3: Implement the job and the registration** as specified above.

- [ ] **Step 4: Run to verify they pass** (same command). Expected: PASS.

- [ ] **Step 5: Write the boot test** `CleanupJobRegistrationTests` (`[UmbracoTest(Database = UmbracoTestOptions.Database.NewSchemaPerTest)]`, derives `UmbracoIntegrationTest` like `PackageBootTests`):

```csharp
[Test]
public async Task Cleanup_job_is_registered_with_umbraco()
{
    IEnumerable<IDistributedBackgroundJob> jobs = this.GetRequiredService<IEnumerable<IDistributedBackgroundJob>>();
    Assert.That(jobs.Select(j => j.Name), Does.Contain("PageNotFoundTracker.TrackedUrlCleanup"));

    await this.GetRequiredService<IDistributedJobService>().EnsureJobsAsync();
    using ICoreScope scope = this.ScopeProvider.CreateCoreScope(); // or the test base's scope accessor
    int rows = /* SELECT COUNT(*) FROM umbracoDistributedJob WHERE <name column> = @0 */;
    Assert.That(rows, Is.EqualTo(1));
}
```

Look up the name column in Umbraco 17.8's `DistributedJobDto` (decompile or read its `[Column]` attribute) before writing the SQL; use the base's `IScopeProvider` + `scope.Database.ExecuteScalar<int>` as other Umbraco integration tests do.

- [ ] **Step 6: Run it**

Run (from `src/`): `dotnet test test/Umbraco.Community.PageNotFoundTracker.Integration.Tests --filter "FullyQualifiedName~CleanupJobRegistrationTests"`
Expected: PASS. Then `dotnet build` → 0 warnings.

- [ ] **Step 7: Commit**

```bash
git add src/code/Umbraco.Community.PageNotFoundTracker.Infrastructure src/test/Umbraco.Community.PageNotFoundTracker.Infrastructure.Tests src/test/Umbraco.Community.PageNotFoundTracker.Integration.Tests/Retention/CleanupJobRegistrationTests.cs
git commit -m "feat: run the tracked URL clean-up as a daily distributed job"
```

---

### Task 5: Documentation

**Files:**
- Modify: `docs/how-it-works.md` (new "## Clean-up" section after "## Batching and the flush interval": the owner of the behaviour)
- Modify: `docs/dashboards.md` (one line in "## Page not found")
- Modify: `docs/troubleshooting.md` (new "## A tracked URL disappeared")
- Modify: `docs/extending/database-providers.md` ("## Migrations": the `AddLastSeenIndex` migration)
- Modify: `README.md` (features list), `src/assets/readme.md` (features list and settings table)
- Modify: `CLAUDE.md` (project overview sentence, spec/plan lists as item 8, a "## Retention (spec 8)" section)

**Interfaces:**
- Consumes: the facts of Tasks 1–4 and the spec's "Umbraco 17.8 scheduler facts".

- [ ] **Step 1: `how-it-works.md` "## Clean-up"** states, in the page's voice: once a day one server removes tracked URLs whose last hit is more than `CleanupAfterDays` days ago (link to `configuration.md#clean-up`), on by default, also when `TrackingEnabled` is off; the first run is about a day after the package is installed or upgraded (restarts do not reset it); deletes in batches of 1000, so the dashboard and tracking keep working; in a load-balanced setup Umbraco's distributed job table makes one server run it, so keep the settings the same on all servers; a rolling deployment where some servers lack the package can push the next run back a day; a run deletes everything due in one go, so the first run after lowering `CleanupAfterDays` on a large table can delay Umbraco's other scheduled jobs (webhooks, scheduled publishing) on that server (lower it in steps); a removed URL that is requested again comes back as a new row from one hit (hits still waiting to be written bring it back too); block rules are never removed; the log line `Removed {Count} tracked 404 URLs ...` at information level.

- [ ] **Step 2: The other pages**: `dashboards.md`: rows not requested for a year (configurable) disappear on their own, link to `how-it-works.md#clean-up`. `troubleshooting.md` "## A tracked URL disappeared": causes in order: the clean-up (look for the log line; `CleanupAfterDays`), a redirect was created, a block covers it. `database-providers.md`: version with the clean-up adds migration `AddLastSeenIndex` (an index on `LastSeen`); generate it in your provider project when you update. Readmes: one feature bullet ("Automatic clean-up of URLs not seen for a year"); `src/assets/readme.md` settings table rows `CleanupEnabled` `true` and `CleanupAfterDays` `365` (absolute GitHub links only, as the file already does). `CLAUDE.md`: settings rows in the Tracking settings table, and a Retention section with the job name, period, batch size, cleaner seam, `LastSeen` index, scheduler facts (first run one period after registration, sequential job loop, row removal on rolling deploy), the tests, and how the manual check was done (Task 6).

- [ ] **Step 3: Run the doc checks** (from the repo root)

```bash
pwsh .github/scripts/check-doc-links.ps1
pwsh .github/scripts/check-doc-samples.ps1
dotnet test src/test/Umbraco.Community.PageNotFoundTracker.Infrastructure.Tests --filter ConfigurationDocsTests
```

Expected: all pass (the `how-it-works.md#clean-up` anchor now resolves).

- [ ] **Step 4: Commit**

```bash
git add docs README.md src/assets/readme.md CLAUDE.md
git commit -m "docs: describe the tracked URL clean-up"
```

---

### Task 6: Full verification and manual check

**Files:** none changed except `CLAUDE.md` (manual check result, if not already written in Task 5).

- [ ] **Step 1: Full build and test** (client first, as CLAUDE.md requires)

```bash
cd src/code/Umbraco.Community.PageNotFoundTracker.BackOffice.Client && npm ci && npm run build
cd ../../ && dotnet build && dotnet test
pwsh ../scripts/check-pending-model-changes.ps1
```

Expected: 0 warnings, all tests pass (SQL Server cases ignored unless `PNFT_SQLSERVER_CONNECTION` is set; run them against LocalDB `(localdb)\pnft-test` if available and say which ran).

- [ ] **Step 2: Manual check on `single-site`** (`cd test-sites/Website-V17 && dotnet run --launch-profile single-site`):
  1. Request two missing URLs, wait for the flush; stop the site.
  2. With node's `node:sqlite` (CLAUDE.md shows the pattern) on `umbraco/Data/single-site.sqlite.db`: set one row's `LastSeen` to two years ago; set the clean-up job's last run in `umbracoDistributedJob` back more than one day.
  3. Start the site, wait past Umbraco's one-minute job delay plus its check period; confirm the aged row is gone, the other stays, and the log (`umbraco/Logs/`) has `Removed 1 tracked 404 URLs last seen before ...`.
  4. Stop the site and restore: `git checkout -- test-sites/Website-V17/appsettings.json test-sites/Website-V17/umbraco/Data/*.sqlite.db`.

- [ ] **Step 3: Record** what was checked and how in `CLAUDE.md` (Retention section, "Manual checks"), commit:

```bash
git add CLAUDE.md
git commit -m "docs: record the clean-up manual check"
```
