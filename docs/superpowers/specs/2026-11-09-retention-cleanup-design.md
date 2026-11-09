# Spec 8: Retention and clean-up of tracked URLs

Status: approved (issue #7, branch `feature/7-retention-cleanup`)
Depends on: spec 3 (tracking), spec 7 (documentation)
Series: see the table in `2026-11-01-solution-setup-design.md`

## Intent

The tracked URL table (`pageNotFoundTrackerUrl`) only grows. Sites that are scanned all the time collect many one-off
URLs that are never requested again. They fill the dashboard with stale rows, and the dashboard queries the table on
every load. This spec adds an automatic clean-up: a daily background job removes tracked URLs whose last hit is older
than a configured age, without anyone doing anything. A URL that is still being requested never ages out, because every
hit moves its `LastSeen` forward.

## Goals

1. A clean-up that is **on by default** and removes tracked URLs whose `LastSeen` is more than **365 days** ago.
2. Two settings under `Umbraco:Community:PageNotFoundTracker`: one to switch the clean-up off, one for the age.
3. A background job that runs once a day, deletes in batches and behaves the same on SQLite and SQL Server.
4. In a load-balanced setup, at most one server runs the clean-up at a time.
5. One log line per run stating how many rows were removed.
6. The public documentation (`docs/`, both readmes) describes the clean-up and the settings, in the same change.

## Non-goals

- No protection by hit count: rows are removed by age only (decided with the maintainer: a URL with many hits that
  nobody requested for a year is no longer a problem).
- No cap on the number of rows.
- No manual deletion from the dashboard (issue #8).
- No clean-up of block rules (`pageNotFoundTrackerBlockRule`): a block rule is an editor's decision, never stale data.
- No dashboard UI for the clean-up (no "last run", no button to run it now).
- No configurable run period or batch size: both are internal constants.
- No clean-up of hits still waiting in a server's in-memory queue (see Behaviour, Rows that come back).

## Anti-goals

- Removing a row whose `LastSeen` is inside the configured age.
- Long table locks that stall the tracking flush or the dashboard (a long *run* is accepted, see the scheduler decision;
  long *locks* are not: every batch is a short statement).
- The job running on two servers at the same time.
- A clean-up failure that stops the site, the tracking flush or Umbraco's other background jobs.
- A clean-up that silently does nothing (switched on but never running, with nothing in the log).

## Constraints

- Umbraco 17.8 (`[17.8.0,18.0.0)`), .NET 10, the project layout and conventions of spec 1 (settings in Core, behaviour in
  Infrastructure, StyleCop, zero analyzer warnings, XML docs, copyright header, `[ComposeBefore(typeof(PageNotFoundTrackerComposer))]`
  on package composers).
- Tracking data lives in the package's own EF Core `PageNotFoundTrackerDbContext` (spec 3); schema changes are EF Core
  migrations added to **both** provider projects with `scripts/add-migration.ps1`, and
  `scripts/check-pending-model-changes.ps1` must pass.
- The documentation rules of spec 7 apply: one page owns each fact, quoted samples are checked, `ConfigurationDocsTests`
  checks that every public setting appears in `docs/configuration.md` and that its first `json` block binds.

## Approaches considered

**A. Umbraco's distributed background job (chosen).** The clean-up is an `IDistributedBackgroundJob`
(`Umbraco.Cms.Infrastructure.BackgroundJobs`, Umbraco 17). Umbraco's `DistributedBackgroundJobHostedService` checks for
runnable jobs on a timer and `IDistributedJobService.TryTakeRunnableAsync` claims a job through its row in Umbraco's
`umbracoDistributedJob` table, so only one server runs it per period. That is the multi-server guarantee the issue asks
for, provided by Umbraco itself, and the mechanism Umbraco 17 moved its own scheduled jobs to. Trade-off: the job only
runs once Umbraco has reached `RuntimeLevel.Run` (after install or upgrade), which is no loss because the tracking table
only exists after that too.

**B. Umbraco's recurring background job limited to a server role.** An `IRecurringBackgroundJob` with `ServerRoles` =
`SchedulingPublisher` and `Single`. The older pattern. Rejected: it depends on the server role election being correct;
a misconfigured or flapping cluster can run it on two servers at once, the case the issue wants to avoid.

**C. A package hosted service with its own lock row.** Rejected: it rebuilds what approach A gets from Umbraco.

## Behaviour

### Settings

Added to `PageNotFoundTrackerSettings` (read once at startup, like the other settings; a change needs a restart):

| Setting | Type | Default | Effect |
|---|---|---|---|
| `CleanupEnabled` | boolean | `true` | When `false` the job runs but removes nothing. |
| `CleanupAfterDays` | integer | `365` | A tracked URL is removed when its last hit is more than this many days ago. A value below 1 means the default (an internal `EffectiveCleanupAfterDays`, as `EffectiveChannelCapacity`). |

The name `CleanupAfterDays` and the integer type are a builder call (made in this spec): an integer number of days
reads better in `appsettings.json` than a time span (`365.00:00:00`). In the approaches discussion the working name was
`CleanupMaxAge`.

The clean-up is independent of `TrackingEnabled`: with tracking off, existing rows still age out (they still clutter the
dashboard).

### The job

`TrackedUrlCleanupJob` (Infrastructure, internal, `IDistributedBackgroundJob`):

- `Name`: a stable, package-prefixed name, for example `PageNotFoundTracker.TrackedUrlCleanup`. It is the key of the row in
  `umbracoDistributedJob` and must never change after release.
- `Period`: one day.
- `ExecuteAsync(CancellationToken)` (the overload with the shutdown token):
  1. When `CleanupEnabled` is `false`: log at debug and return.
  2. Wait for the tracking persistence gate (`ITrackingPersistenceState.WaitUntilReadyAsync`). When it reports
     `Unavailable` (no provider, failed migration), return without logging: the migration service already logged why.
  3. Compute the cut-off once: `TimeProvider` UTC now minus `EffectiveCleanupAfterDays` days. A row is removed when
     `LastSeen < cutoff` (strictly older; a row exactly at the cut-off stays).
  4. Delete in batches (an internal constant, about 1000 rows per statement) until a batch removes fewer rows than the
     batch size. Each batch is its own statement and its own short transaction, so locks stay short and a flush or
     dashboard query can run between batches. A fresh `PageNotFoundTrackerDbContext` per run, from
     `IDbContextFactory<PageNotFoundTrackerDbContext>` resolved at run time (as `TrackingFlushService` and
     `TrackedUrlQueryService` do: the pooled factory must not be resolved before the gate opened).
  5. Log the total.
- The job does not take the tracking flush lock (`ITrackingFlusher.RunExclusiveAsync`) and does not touch the in-memory
  queue. It is not needed: each batch's `WHERE LastSeen < @cutoff` is evaluated inside the delete statement, so a row a
  flush has just updated is no longer older than the cut-off and is not deleted; and an upsert whose update finds a row
  already deleted inserts it (spec 3's update-then-insert). Holding the flush lock across a large clean-up would stall
  tracking on that server for no gain.
- Cancellation (host shutdown) stops between batches; rows already removed stay removed and are included in the log line.
- Any other exception is caught inside the job, logged as an error with the number removed so far, and the job returns
  normally (the next run is a day later). The job never throws into Umbraco's scheduler.

Batch delete shape (builder's choice, see Left to the builder): it must select the rows by `LastSeen < cutoff`, limit the
number per statement, and produce the same result on SQLite and SQL Server, proven by integration tests on both.

### Registration

`builder.Services.AddSingleton<IDistributedBackgroundJob, TrackedUrlCleanupJob>()` from the Infrastructure registration
(`InfrastructureComposer` path). The job is always registered, also when `CleanupEnabled` is `false`, so switching the
setting does not add and remove rows in `umbracoDistributedJob`. Umbraco's `IDistributedJobService.EnsureJobsAsync`
registers the job's row on startup, also on an existing site that installs or upgrades the package (to be confirmed by a
test, see Testing). The job must be singleton-compatible: it resolves scoped and pooled services per run.

### Umbraco 17.8 scheduler facts (checked by decompiling `DistributedJobService` and `DistributedBackgroundJobHostedService`)

- `IDistributedBackgroundJob` requires the parameterless `ExecuteAsync()`; the token overload calls it by default.
  Implement both (the parameterless one delegates with `CancellationToken.None`).
- `EnsureJobsAsync` adds the row of a new job with `LastRun = now`, so the **first run is one period after the package is
  installed or upgraded**, not at startup; restarts do not reset it. Umbraco also waits `DistributedJobSettings.Delay`
  (one minute by default) before it registers jobs. The docs and the manual check must reflect this.
- `EnsureJobsAsync` deletes rows of jobs the starting server does not register. During a rolling deployment where some
  servers lack the package, the row can be removed and added again, which pushes the next run back by one period.
  Accepted; mentioned in the docs.
- A claimed job is only taken over by another server after `LastAttemptedRun + Period + MaximumExecutionTime`
  (`MaximumExecutionTime` five minutes by default), so a run cannot start on two servers at once in practice.
- The scheduler loop catches job exceptions, passes the shutdown token and always calls `FinishAsync`. The job still
  catches its own exceptions (for the log line with the count).
- Each server runs its distributed jobs **one after another** in one loop: while the clean-up runs, that server runs none
  of Umbraco's other distributed jobs (webhook firing, scheduled publishing, log and content version clean-up).

**Decision (maintainer): one daily run, no cap.** A run deletes everything due, however long it takes. A normal daily run
is short. The long case is a large existing table after `CleanupAfterDays` is lowered (for example 365 to 7): that first
run can hold up webhooks and scheduled publishing on the claiming server for a while. Considered and rejected: an hourly
period with a time cap per run. `docs/how-it-works.md` and the `CleanupAfterDays` row in `docs/configuration.md` warn about
the long first run after lowering the age (suggest lowering it in steps on a large table).

### Multi-server behaviour

Umbraco's distributed job table makes one server claim each run. Settings are read per server, so when servers have
different clean-up settings, the server that claims the run uses its own. The docs say: keep the settings the same on all
servers.

### Rows that come back

Hits still waiting in any server's queue (not yet flushed) are not touched. If the clean-up removes a row and a pending
hit for that URL is flushed afterwards, the row is created again with only the pending hits and a new `FirstSeen`. That
is correct: the URL was requested recently, so it is not stale. Documented in `how-it-works.md`.

### Logging

| Situation | Level | Message (wording is the builder's) |
|---|---|---|
| Run removed rows | Information | "Removed {Count} tracked 404 URLs last seen before {Cutoff}." |
| Run removed nothing | Debug | same, count 0 |
| `CleanupEnabled` false | Debug | "Clean-up of tracked 404 URLs is switched off." |
| Cancelled by shutdown | Information when count > 0, else Debug | count removed so far |
| Failure | Error, with the exception | count removed so far |

## Data model

A new index on `TrackedUrl.LastSeen` (not unique), so the batch delete does not scan the whole table. One migration,
named `AddLastSeenIndex`, generated in both provider projects with `pwsh scripts/add-migration.ps1 -Name AddLastSeenIndex`.
No new table, no new column. A custom database provider (spec 3, "Extending for other databases") must add this migration
too: `docs/extending/` already says custom providers ship migrations for all package tables; the docs change mentions the
new migration where that rule is stated.

## Documentation

Public pages (spec 7 rules, one page owns each fact):

- `docs/configuration.md`: a new "Clean-up" section with the two settings (owner of the settings), the two settings in
  the full example `json` block, and `ConfigurationDocsTests` updated for the new values asserted from that block.
- `docs/how-it-works.md`: a "Clean-up" section (owner of the behaviour): daily, by last seen, batches, one server at a
  time, the first run about a day after install or upgrade, rows that come back, keep settings equal on all servers,
  independent of `TrackingEnabled`, the long first run after lowering `CleanupAfterDays` (it delays Umbraco's other
  scheduled jobs on that server), and a rolling deployment can push the next run back a day.
- `docs/dashboards.md`: one line that rows not requested for the configured age disappear on their own, linking to
  how-it-works.
- `docs/troubleshooting.md`: "A tracked URL disappeared" (clean-up by age, a redirect or a block removed it).
- `docs/extending/` where custom providers are described: the new `AddLastSeenIndex` migration.
- `README.md` features list and `src/assets/readme.md` (features and the settings table): the clean-up and its default.
- `CLAUDE.md`: a "Retention (spec 8)" section and the spec/plan lists. Not public documentation.

## Testing

- Unit (Infrastructure.Tests): `EffectiveCleanupAfterDays` (default, below 1, positive); the job with `CleanupEnabled`
  false does nothing; with persistence `Unavailable` it does nothing; cut-off computation from a fake `TimeProvider`.
- Integration (Integration.Tests, the database fixture pattern of spec 3: SQLite always, SQL Server when
  `PNFT_SQLSERVER_CONNECTION` is set):
  - rows older than the cut-off are removed, a row exactly at the cut-off and newer rows stay;
  - more rows than one batch (for example 2500) are all removed in one run;
  - block rules are untouched;
  - the migration creates the `LastSeen` index on both providers.
- Integration (Umbraco booted on SQLite): the job is in the container's `IEnumerable<IDistributedBackgroundJob>`, and its
  row exists in `umbracoDistributedJob` after boot.
- `ConfigurationDocsTests`, `check-doc-samples.ps1`, `check-doc-links.ps1` pass.
- No E2E test: the job runs once a day; the behaviour is covered by integration tests.
- Manual check on the `single-site` profile: insert an old row (or age one with SQL), make the job due (set its
  `lastRun` in `umbracoDistributedJob` back more than a day while the site is stopped; the first run is otherwise a day
  after the job's row was added), start the site, wait past Umbraco's one-minute job delay, confirm the row is gone and
  the log line appears. Restore the committed database afterwards. The builder documents how it was checked.

## Acceptance

1. A fresh install and an upgraded site both remove tracked URLs last seen more than 365 days ago, once a day (the first
   run about a day after the job is registered), without configuration.
2. `CleanupEnabled: false` removes nothing; `CleanupAfterDays: 30` removes rows older than 30 days.
3. The same results on SQLite and SQL Server (integration tests).
4. One log line with the count per run that removed rows.
5. Build with zero analyzer warnings; all unit, integration and doc checks pass; no pending model changes.
6. The documentation listed above is updated in the same branch.

## Left to the builder

- The exact batch delete query (for example select a page of ids ordered by `Id` then `ExecuteDeleteAsync` on those ids,
  or `Take` inside the delete if EF Core translates it on both providers), as long as the integration tests prove both
  providers.
- The batch size constant (around 1000) and the exact job name (package-prefixed, never changed after release).
- Whether `AlignToClock` is set (default is fine).
- Log message wording.
