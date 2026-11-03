# Spec 3: Tracking of 404 URLs

Status: implemented (issue #3, branch `feature/3-tracking`)
Depends on: spec 1 (solution setup), spec 2 (404 handling)
Series: see the table in `2026-11-01-solution-setup-design.md`

## Intent

Site owners want to know which URLs visitors request that do not exist, and how often, so
they can fix links or create redirects (spec 5) or block noise (spec 6). This spec records
every 404 detected by spec 2 in the database, cheaply and without slowing down requests.

Requests are not written to the database one by one. They are queued in memory, merged, and
written in batches by a background service. The result is one record per URL, with a hit
count.

## Goals

1. Every 404 reaching the call points from spec 2 is recorded, unless tracking is disabled.
2. Writing never blocks or slows the visitor's request.
3. One record per distinct URL (per domain and culture), with hit count and first and last
   seen times.
4. Tracking can be switched off in app settings, and the flush interval is configurable.
5. The database layer works on SQLite and SQL Server with migrations for both, and an
   implementer can add support for another database without changing this package.
6. A `sql-server` launch profile and CI integration tests prove the SQL Server migrations.

## Non-goals

- No dashboard (spec 4), redirects (spec 5) or blocking (spec 6).
- No retention or clean-up of old records. Not discussed; a later spec can add it.
- No storing of referrer, user agent, client IP or individual hits.
- No API for reading the data. Spec 4 adds what the dashboard needs.
- No support for databases other than SQLite and SQL Server in the box. Only the extension
  point.

## Anti-goals

- A database write inside the request pipeline.
- Unbounded memory growth when the database is slow or a scanner floods the site.
- A model that creates several records for what an editor sees as one URL.
- A broken site because the database provider is unknown or a write fails. Tracking
  degrades, the site keeps serving pages.

## Constraints

- Umbraco 17.x, .NET 10, solution layout from spec 1. Contracts and entity in `.Core`,
  `DbContext`, channel and background service in `.Infrastructure`.
- Settings under `Umbraco:Community:PageNotFoundTracker`, bound to the settings class from
  spec 2.
- Same database as Umbraco (the connection string `umbracoDbDSN`). The table name carries a
  package prefix so it cannot clash with other tables.
- StyleCop and the other conventions from spec 1 apply.
- Packaging: `Umbraco.Cms.Persistence.EFCore` (`[17.8.0,18.0.0)`, central package management)
  is a new package reference. It brings EF Core and both EF Core providers at Umbraco's own
  version. Because sibling projects are `PrivateAssets=all`, it must be mirrored in the entry
  csproj, and the two persistence projects must be added to the entry project's packed dlls
  and to `check-package.ps1`. `Microsoft.EntityFrameworkCore.Design` (for the migrations
  script) is a development-only reference and must not flow to consumers.

## Approaches considered

Database layer (decided with the user), grounded in the `YodasMyDad/Merchello` repository
and in `umbraco-pagespeed-optimization`, which use the same pattern:

- **A. EF Core with a provider project per database. Chosen.** A shared `DbContext` and a
  provider contract, with SQLite and SQL Server each in their own migrations project.
  Other databases plug in through the same contract.
- B. Umbraco's own NPoco migrations. Rejected: implementers could not add databases.
- C. EF Core with one shared migration set. Rejected: EF Core migrations are provider
  specific, so one set breaks on one of the two databases.

Plumbing inside approach A (decided with the user on 2026-11-03, issue #3):

- A1. Own plumbing, as Merchello: the package reads `umbracoDbDSN`, resolves `|DataDirectory|`
  itself, and the provider contract matches, configures, migrates and upserts. Rejected: it
  duplicates Umbraco's connection handling, and a mismatch would point SQLite at a different
  file than Umbraco.
- **A2. Build on Umbraco's EF Core support. Chosen.** The `DbContext` is registered with
  `AddUmbracoDbContext<T>` from `Umbraco.Cms.Persistence.EFCore` (the overload that passes
  the resolved connection string and provider name). The provider contract shrinks to the
  provider name, configuring the options (`UseSqlite`/`UseSqlServer` with its own
  migrations assembly and history table) and an optional upsert override. The default
  upsert is shared EF Core code that works on every provider (see Background flush). EF Core
  comes in through `Umbraco.Cms.Persistence.EFCore`, so its version always matches
  Umbraco's (10.0.x for 17.8).
- A3. A2 with native upsert SQL per provider (`INSERT … ON CONFLICT` on SQLite,
  `MERGE WITH (HOLDLOCK)` on SQL Server). Rejected: raw SQL in two places and every custom
  provider would have to write its own. The upsert override keeps this possible later.

Delivery (decided with the user): everything in this spec lands in one pull request,
including the SQL Server profile and the CI changes.

Packaging (decided with the user): both providers ship inside the single NuGet package
from spec 1, and the matching one is chosen at runtime from the connection string. The
alternative of separate provider packages, as Merchello does, was not chosen.

Background processing (decided with the user): an in-memory `Channel` plus a hosted
`BackgroundService` that flushes on an interval. A hosted service per server is used rather
than Umbraco's recurring jobs, because each server has its own in-memory queue to flush.

Storage model (decided with the user): one record per URL, not one record per hit.

## Behaviour

### Recording

- The two call points from spec 2 hand a small record (original URL, domain, culture,
  page or file) to a tracker service. If tracking is disabled, the tracker does nothing
  and the call returns immediately.
- The tracker writes the record to a bounded `Channel` without waiting. If the channel is
  full, the new record is dropped and a warning is logged, rate limited so a flood does
  not flood the log.
- The call never throws into the request pipeline.

### URL identity

Two requests count as the same URL when domain, culture and the normalised URL match.

- Default: path only. The query string is ignored.
- Setting `IncludeQueryString` (default `false`): when on, the query string is part of the
  identity. Parameters are compared as they arrive, so reordered parameters count as
  different URLs. (The builder may normalise parameter order if it is cheap. Left to
  the builder.)
- Case is ignored and a trailing slash is ignored.
- The first-seen spelling of the URL is kept for display.
- Domain and culture come from spec 2's call point. A request with no matched domain is
  stored with an empty domain.
- Whether the request was for a page or a file is stored on the record but is not part of
  the identity.

### Background flush

- A hosted service reads the channel and, every `FlushInterval` (default 1 minute,
  configurable), merges everything queued since the last flush into one entry per identity,
  with a count, and writes them in one batch.
- The write is an additive upsert: a new URL becomes a new record, an existing one gets its
  hit count increased and its last-seen time moved forward. The increase must be atomic in
  the database, so several servers writing to the same table do not lose counts.
- Default upsert (approach A2): for the merged batch, first an EF Core `ExecuteUpdate`
  that sets `HitCount = HitCount + n` and `LastSeen = max(LastSeen, batch last seen)` for
  identities that exist, then an insert of the identities that matched no row. If an insert
  hits the unique index because another server inserted the same identity in between, the
  update is retried once for those identities. A provider can replace this code path.
- A failed write is logged and the batch is retried at the next interval, up to a bounded
  amount of memory. Beyond that, the oldest entries are dropped.
- When the application stops, the service flushes what is pending before it exits, within
  the host's shutdown time.

### Data model

One table with one row per URL identity:

| Field | Meaning |
|-------|---------|
| Id | Primary key |
| Url | Normalised identity form of the URL (full path as requested) |
| RelativePath | Normalised path relative to the matched domain, without query string, indexed (see calls) |
| DisplayUrl | First-seen spelling |
| Domain | Matched domain, or empty |
| Culture | Culture, or empty |
| IsFile | Whether the request was for a file |
| HitCount | Number of 404s |
| FirstSeen / LastSeen | UTC `DateTime` values (not `DateTimeOffset`, which SQLite cannot order reliably) |

A unique index covers the identity (Url, Domain, Culture). SQL Server limits index key size,
so URL length handling (truncation, or an additional hash column) is left to the builder
and must behave the same on both databases.

### Settings

Under `Umbraco:Community:PageNotFoundTracker`:

- `TrackingEnabled` (default `true`).
- `FlushInterval` (default one minute).
- `IncludeQueryString` (default `false`).
- `ChannelCapacity` (default chosen by the builder).

When tracking is disabled, nothing is queued and the background service does no work.
Migrations still run, so enabling tracking later works immediately. (Writer's call.)

## Database providers

### Contract

`.Core` defines a provider contract with, at least:

- the Umbraco database provider name it supports (for example `Microsoft.Data.Sqlite`),
- how to configure the `DbContext` options from the connection string Umbraco resolved,
  including the migrations assembly and the package-prefixed history table,
- optionally, its own additive upsert (otherwise the shared default is used).

Connection string and provider name come from `AddUmbracoDbContext<T>`, so the contract does
not read configuration. Migrations run through the shared `DbContext` (`Database.MigrateAsync`)
once the provider has configured it, so the contract needs no migrate member unless a
database needs one. Exact members are left to the builder, with Merchello's pair of
provider interfaces as a reference.

If the contract needs EF Core types (`DbContextOptionsBuilder`), `.Core` takes a dependency on
EF Core abstractions; that is acceptable for this contract (writer's call).

### Built-in providers

Two projects hold the provider implementations and their own EF Core migrations:
`Umbraco.Community.PageNotFoundTracker.Persistence.Sqlite` and
`Umbraco.Community.PageNotFoundTracker.Persistence.SqlServer`. Each registers itself in a
composer. Their output ships inside the single package from spec 1. The SQLite provider also
enables WAL journaling, as Merchello does.

### Selection and failure

At startup the provider whose name matches Umbraco's configured database provider is used
to run migrations and to configure the `DbContext`. If none matches, the package logs a
clear error naming the provider, and tracking is disabled for that run. The site keeps
working.

### Extending for other databases

An implementer references the package, writes a provider for their database with its own
migrations, and registers it from their own composer. The package then selects it through
the same lookup. `CLAUDE.md` documents the contract and shows a minimal example. A test
registers a fake provider and proves it is picked.

### Migrations workflow

A script in `scripts/` (PowerShell, as Merchello's `add-migration.ps1`) creates the same
named migration for both providers in one go. CI also checks that both providers have no
pending model changes, so the two migration sets cannot drift apart.

## Test site and CI

- New launch profile `sql-server` on the test site, with its connection string for a
  SQL Server running in a Docker container. The README and `CLAUDE.md` document the
  `docker run` command and the connection string. The site installs itself on first run
  through the unattended install settings, so no database file is committed. This relaxes
  the "no database server other than SQLite" non-goal from spec 1 for this purpose only.
  The profile has the single-site shape. It does not configure MCP and has no 404 page content, and
  tracking needs neither.
- Integration tests apply the migrations on SQLite and on SQL Server. In CI, SQL Server
  runs as a service container on the GitHub runner. Locally, the same tests use the Docker
  container and are skipped with a clear message if no connection is configured.
- PR validation, the beta workflow and the release workflow all get the SQL Server service
  container and the pending-model-changes check, so the integration tests run on SQL Server
  everywhere and the three workflows stay in step.

## Testing

- **Unit tests:** URL normalisation (case, trailing slash, query on and off), merging of
  queued records, channel overflow, flush timing and the interval setting, shutdown flush,
  disabled tracking, provider selection including the unknown provider case, and a custom
  provider being picked up.
- **Integration tests:** on SQLite and SQL Server, against the package's `DbContext` only
  (no Umbraco boot on SQL Server, decided with the user on 2026-11-03): migrations apply to an
  empty database, the upsert creates and then increments, and concurrent flushes do not lose
  counts. On SQLite only, with Umbraco booted as in spec 2's tests: an end-to-end 404 request
  ends up as a record after a forced flush. The full site on SQL Server is proven manually
  through the `sql-server` launch profile.
- **E2E:** none needed beyond spec 2's. A 404 request followed by a forced flush is covered
  in integration tests.

## Calls made in this spec

These are the writer's calls, not decisions the user made:

- Dropping on a full channel, with rate limited warnings, rather than blocking.
- Flush on shutdown, and a bounded retry of failed batches.
- Whether a 404 page was resolved is not stored.
- Referrer, user agent and IP are not stored.
- Migrations run even when tracking is disabled.
- Unknown database provider disables tracking instead of failing startup.
- Both migration sets must be created by one script, and CI checks they do not drift.
- Table name has a package prefix.
- No retention or clean-up in this spec.
- Startup order: migrations run only once Umbraco's runtime level is `Run`, so after
  Umbraco's own install has finished. If the runtime level is lower (for example during
  install), the package waits for the level to become `Run` within the same run and then
  migrates. Migrations having completed is exposed as an awaitable startup gate that later specs
  wait on (spec 6's first rule load). If Umbraco restarts after installing, the gate is rebuilt
  on that start. It never creates
  the database itself.
- Connection: the connection string and provider name are read from Umbraco's own
  connection string configuration, and the `|DataDirectory|` token is resolved the way
  Umbraco resolves it, so SQLite points at the same file as Umbraco.
- EF Core history table: a package-prefixed table name (and schema where the database has
  schemas), so it cannot clash with Umbraco's own EF Core migrations in the same database.
- Once per request: each request is tracked at most once, guarded by a flag on the request,
  even if the finder or the re-rendered page runs more than once. Any method that reaches
  the call points counts, so HEAD requests count.
- Stored URL: the full path as the visitor requested it, including any alias prefix such as
  `/siteA`. `Domain` stores the matched Umbraco domain name exactly as Umbraco returns it
  (stored lowercase with the port in the test site, for example `localhost:44402/sitea` or
  `www.sitea.local:44404`), or an empty string. Specs 4 and 5 can
  derive the path relative to the domain from these two.
- Normalisation: the percent-encoded form is decoded before comparing, then compared
  ignoring case (invariant culture) with a trailing slash and repeated slashes ignored.
  (Final review, 2026-11-03: the decoding happens at the call point, by Kestrel, which decodes
  everything but `%2F`; the one call point without an `HttpContext` decodes the URI path the
  same way. The normaliser itself no longer decodes, because decoding twice merged `/a%2541`
  with `/aA`.)
- Culture is stored as the code Umbraco returns for the domain, compared ignoring case. No
  culture and no domain are stored as an empty string, never null, so the unique index
  behaves the same on SQLite and SQL Server.
- When an identity is merged or upserted with different `IsFile` values, the value from the
  first-seen record is kept. `DisplayUrl` likewise keeps the first-seen spelling.
- The additive upsert is part of the provider contract only if EF Core's bulk operations
  cannot express it on a database. Built-in providers should use the same code path unless
  a database needs its own, and the contract documents how an implementer overrides it.
- Tracking does not depend on a resolved 404 page. A 404 where spec 2 resolved nothing is
  tracked the same way.
- The `sql-server` profile installs the Clean starter kit through the normal unattended
  install. It has no MCP user and no 404 page content, because both live in the committed
  databases of the four SQLite profiles. Missing-page tracking works without a 404 page.
- Settings are read once at startup. Changing them needs a restart.
- Relative path (added after the cross-spec review): each record also stores `RelativePath`,
  the request path with the matched domain's own path removed (case-insensitive), normalised
  like `Url`, without the query string, and indexed. With no matched domain it equals the
  normalised path. It is computed when the 404 is recorded. Records keep the value computed
  then, even if domains are edited later. Specs 4 to 6 use it for display, redirect mapping,
  rule matching and rule deletes, so none of them has to repeat prefix stripping or fight the
  query string. It is not part of the unique identity.
- "File" in the `IsFile` column means the last segment of the request path has a file
  extension, in both code paths, as defined in spec 2.
- Discarding queued hits (needed by specs 5 and 6): this spec only prepares for it (decided
  with the user on 2026-11-03). The flush drains the channel into an in-memory merge buffer,
  and the merge buffer, the retry buffer and the batch in flight are guarded by one lock that
  a later discard can take, so a discard and a flush can be serialised. No discard operation
  is added here: spec 5 adds it for a URL identity and spec 6 extends it to path patterns. A
  channel itself cannot remove items, which is why the buffer exists.
- Persistence availability: the package has one state, "persistence available", true only
  when a provider matched and migrations completed. When it is false, tracking is off, the
  dashboards and endpoints of specs 4 to 6 report a clear "database unsupported or
  unavailable" state, and blocking passes requests through with a logged reason.
- Later specs add tables (spec 6 adds the block rule table). A custom provider must include
  migrations for all package tables. The provider documentation says so, and adding a table
  in a later version is a change implementers have to follow.
- Committed SQLite databases hold no tracked rows. Running the site dirties them (migrations,
  WAL), and contributors reset them with `git checkout`, as spec 1 documents for other
  incidental changes. E2E runs that create tracked data must never commit it.

- After the builder check (2026-11-03), writer's calls: the `DbContext` and entity are public so
  custom providers can build migrations against them; when two providers register the same
  name, the last registration wins (so an implementer can replace a built-in one); hits that
  arrive before migrations finish are queued as usual and flushed once the startup gate opens;
  with `IncludeQueryString` off, `DisplayUrl` holds the path without the query string.

- During the build (writer's calls):
  - SQLite WAL: the SQLite provider does not set the journal mode. Umbraco 17 already runs SQLite in WAL mode
    (unlike Merchello's setup), so the "enables WAL journaling" line under Built-in providers is not needed.
  - URL length handling: the unique index is on `UrlHash` (SHA-256 hex, 64 characters, of the full normalised URL
    before truncation), `Domain` and `Culture`, not on `Url`, so it fits SQL Server's index key limit and behaves the
    same on SQLite. Stored values are truncated: `Url` and `DisplayUrl` 2000 characters, `RelativePath` 800 (it is
    indexed), `Domain` 255; `Culture` is a 50 character column. Two long URLs that only differ after the cut stay
    two records.
  - Memory bound: one bound for queue and retry. `ChannelCapacity` (default 10000) caps the channel (full: the new hit
    is dropped, warned at most once a minute with the count) and the pending (merge + retry) buffer in identities
    (over it after a failed write: the entries with the oldest `LastSeen` are dropped, with a warning).
  - `ITrackedUrlUpsert.UpsertAsync` returns the hits it did not write (the default writes per identity, so one bad
    row does not fail the batch); only those go back to the pending buffer, so written hits are never counted twice.
    An implementation that throws counts as having written nothing, so a batching override must be all-or-nothing.
  - Final review fixes (2026-11-03):
    - The default upsert logs per-hit failures at debug and stops after 3 consecutive failures, returning the failed
      and the unattempted hits; the flush logs one summary warning per failed flush with the first exception. A dead
      or slow database cannot stall a flush hit by hit.
    - On cancellation the default upsert returns the current and the unattempted hits instead of throwing, so a
      flush cancelled mid-batch (shutdown) restores only unwritten hits and nothing is counted twice. Implementations
      should do the same. Hits still queued after the final flush at shutdown are lost; that is logged as a warning
      with the count.
    - `HitCount` (and the per-identity count in memory) is a 64-bit value.
    - `Culture` is truncated to its 50 character column.
    - An unknown provider is logged as a warning instead of an error when `TrackingEnabled` is off (nothing is lost).
    - When the persistence becomes unavailable, the queued hits (channel, pending buffer, in-flight batch) are
      discarded, so hits queued while pending do not stay in memory.
    - Dropping a hit on a full channel is lock free (interlocked counter, compare-and-swap on the warning time).
    - Deferred: migration retry with backoff, chunked transactions.
  - With `IncludeQueryString` on, the query string is lower-cased but not percent-decoded (the path arrives decoded
    from the call point; the normaliser does not decode).
    Parameter order is not normalised.
  - The provider composers (`SqlitePersistenceComposer`, `SqlServerPersistenceComposer`) are public so an
    implementer can `[ComposeAfter]` them to replace a built-in provider for the same name.
  - SQL Server is tested at the database layer only (migrations, upsert, concurrency) through
    `PNFT_SQLSERVER_CONNECTION`, a server-level connection string; each run creates and drops a `pnft_test_<guid>`
    database. CI uses the service container; local runs may use Docker or a LocalDB instance.
  - `FlushInterval` below one second is raised to one second; zero or negative, and a `ChannelCapacity` below one,
    fall back to the defaults.
  - The test site project-references both provider projects: the entry project references them with
    `PrivateAssets=all` and never uses them in code, so a project-referencing site would not get their dlls
    (NuGet consumers get them from `lib/`).

## Acceptance

- A 404 request on `single-site` becomes one record after the next flush; a second request
  for the same URL increases its hit count and does not add a record.
- The same URL with a different case or trailing slash is the same record. With
  `IncludeQueryString` on, a different query string is a different record.
- The same path on two sites or two cultures gives two records.
- With `TrackingEnabled` false, no records appear and no queue builds up.
- Changing `FlushInterval` changes how often records appear.
- Stopping the site flushes pending records.
- Migrations apply on SQLite and on SQL Server, tested locally and in CI. The
  `sql-server` profile starts against a Docker SQL Server and tracks a 404.
- An unknown database provider logs an error and the site still serves pages.
- A fake provider registered by an implementer is selected, proven by a test.
- `CLAUDE.md` documents the settings, the provider contract and the migrations script.

## Left to the builder

- The exact provider contract members and the `DbContext` details.
- URL length handling for the unique index.
- Whether query parameter order is normalised.
- Default channel capacity and the retry memory bound.
- How the additive upsert is written for each provider (EF Core bulk operations or raw
  SQL), as long as it is atomic.
- The Docker image tag and the exact CI service container setup.
