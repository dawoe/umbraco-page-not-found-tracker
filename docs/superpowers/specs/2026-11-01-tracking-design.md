# Spec 3: Tracking of 404 URLs

Status: draft, awaiting builder check and approval
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

## Approaches considered

Database layer (decided with the user), grounded in the `YodasMyDad/Merchello` repository
and in `umbraco-pagespeed-optimization`, which use the same pattern:

- **A. EF Core with a provider project per database. Chosen.** A shared `DbContext` and a
  provider contract, with SQLite and SQL Server each in their own migrations project.
  Other databases plug in through the same contract.
- B. Umbraco's own NPoco migrations. Rejected: implementers could not add databases.
- C. EF Core with one shared migration set. Rejected: EF Core migrations are provider
  specific, so one set breaks on one of the two databases.

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
- A failed write is logged and the batch is retried at the next interval, up to a bounded
  amount of memory. Beyond that, the oldest entries are dropped.
- When the application stops, the service flushes what is pending before it exits, within
  the host's shutdown time.

### Data model

One table with one row per URL identity:

| Field | Meaning |
|-------|---------|
| Id | Primary key |
| Url | Normalised identity form of the URL |
| DisplayUrl | First-seen spelling |
| Domain | Matched domain, or empty |
| Culture | Culture, or empty |
| IsFile | Whether the request was for a file |
| HitCount | Number of 404s |
| FirstSeen / LastSeen | UTC timestamps |

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
- how to configure the `DbContext` options, including the migrations assembly,
- how to run migrations.

Exact members are left to the builder, following Merchello's pair of provider interfaces as
a reference.

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
  The profile has the single-site shape and also has Umbraco MCP configured, as the others
  do.
- Integration tests apply the migrations on SQLite and on SQL Server. In CI, SQL Server
  runs as a service container on the GitHub runner. Locally, the same tests use the Docker
  container and are skipped with a clear message if no connection is configured.
- PR validation gets the SQL Server service container and the pending-model-changes check.

## Testing

- **Unit tests:** URL normalisation (case, trailing slash, query on and off), merging of
  queued records, channel overflow, flush timing and the interval setting, shutdown flush,
  disabled tracking, provider selection including the unknown provider case, and a custom
  provider being picked up.
- **Integration tests:** on SQLite and SQL Server: migrations apply to an empty database,
  the upsert creates and then increments, concurrent flushes do not lose counts, and an
  end-to-end 404 request ends up as a record after a flush.
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
  install), the package logs it and does nothing until the next start. It never creates
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
  `/siteA`. `Domain` stores the matched Umbraco domain name exactly as configured (for
  example `localhost/siteA` or `www.siteA.local`), or an empty string. Specs 4 and 5 can
  derive the path relative to the domain from these two.
- Normalisation: the percent-encoded form is decoded before comparing, then compared
  ignoring case (invariant culture) with a trailing slash and repeated slashes ignored.
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
  install, like the other profiles, so a 404 page exists once spec 2's test content is
  added. Missing-page tracking works without it.
- Settings are read once at startup. Changing them needs a restart.

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
