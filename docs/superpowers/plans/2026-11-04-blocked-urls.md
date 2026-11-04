# Blocked URLs Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Block rules (wildcard URL pattern, status code, domain and culture scope) that answer matching requests at once with an empty body, are never tracked, delete matching tracked records, and are managed from a "Block" row action and a second dashboard tab.

**Architecture:** Rules live in a new table of the spec 3 `DbContext`. An immutable `BlockSnapshot` (rules pre-sorted by specificity, plus a copy of Umbraco's domains) is held by a singleton provider that loads once after `RuntimeLevel.Run`, swaps atomically on refresh and keeps the old one when a rebuild fails. A middleware registered by `UsePageNotFoundTracker()` (before `UseUmbraco()`) matches the request path against the snapshot with no database read. A `PayloadCacheRefresherBase` refreshes every server and makes it discard queued hits that match a new rule. The client adds a second collection dashboard, a "Block" and an "Unblock" entity action, and a rule modal.

**Tech Stack:** .NET 10, Umbraco 17.8 (`PayloadCacheRefresherBase`, `DistributedCache`, `IDomainCache`), EF Core (SQLite, SQL Server), NUnit + Moq, Umbraco integration tests, TypeScript + Lit, `@hey-api/openapi-ts`, Vitest, Playwright.

**Spec:** `docs/superpowers/specs/2026-11-01-blocked-urls-design.md` (issue #6). Read it first, including "Build decisions" and "Builder-check calls", which are binding. Load before client work: `umbraco-cms-backoffice-skills:umbraco-version-guard`, `-umbraco-entity-actions`, `-umbraco-modals`, `-umbraco-collection`, `-umbraco-collection-action`, `-umbraco-openapi-client`, `-umbraco-localization`, `-umbraco-chrome-navigation`, `umbraco-cms-backoffice-testing-skills:umbraco-e2e-testing`.

## Global Constraints

- Branch `feature/6-blocked-urls` (off `develop`), one PR to `develop`. Commits end with the session's `Co-Authored-By` line.
- Layering (`CLAUDE.md`): rule model and contracts in `.Core` (`...Core.Blocking`); matching, snapshot, middleware, service, refresher, persistence in `.Infrastructure` (`...Infrastructure.Blocking`); endpoints in `.BackOffice`; UI in `.BackOffice.Client/src/blocking/`. Entry project only wires. Package composers keep `[ComposeBefore(typeof(PageNotFoundTrackerComposer))]`; replaceable services use `AddUnique`.
- Status codes 400 to 499 accepted (`BlockingConstants.MinStatusCode/MaxStatusCode`); dialog offers 403, 404, 410, default 410. Blocked response: that status, empty body, any method, never tracked, independent of `TrackingEnabled`.
- Pattern: starts with `/`, `*` matches any run including slashes; normalised = percent-decoded once, lower-cased, repeated slashes collapsed, trailing slash dropped (lone `/` kept); path only, query string never matched; at most 800 characters (the `RelativePath` column). Rule identity = normalised pattern + domain + culture (empty string = all); re-adding updates the status code.
- Matching text = the request path as Kestrel hands it (no second decode), normalised, with the matched domain's own path removed (`TrackedUrlNormalizer` rules). Domain = lower-cased matched domain name as tracking stores it; culture = the matched domain's culture. No matched domain: only all-domain, all-culture rules apply.
- Precedence: domain beats all domains, then culture beats all cultures, then longer literal prefix, then lowest status code.
- Safeguards: Umbraco-owned paths (`UmbracoOwnedPaths`) are never blocked at request time; a pattern is rejected when it matches an owned path itself or its literal prefix lies inside an owned subtree; patterns without a literal character besides `/` and `*` are rejected.
- Snapshot: always one current; the first load runs once `RuntimeLevel.Run` and persistence is `Available`; a request waits for the shared initial load at most 5 s, then passes through with a warning; the load retries every 30 s; persistence `Unavailable` passes through at once. Rebuilds also run on Umbraco domain and language changes.
- Access: same policy as the dashboard (`Constants.DashboardAccessPolicy`), same OpenAPI document, operation ID = action method name.
- StyleCop zero warnings, copyright header, XML docs on public and internal members. Client: `data-mark` on interactive elements (copy into the E2E project), texts in `localization/en.ts` as `pageNotFoundTracker_*`, no `localhost` in the bundle.
- Never commit `.sqlite.db` / `appsettings.json` changes from profile runs.

## Review Focus

1. Request `/wp-login.php?x=1` or a `POST`/`HEAD` to a blocked path, `%` and `_` in a pattern or stored path: blocked regardless of query and method; the SQL delete never over-deletes (Tasks 1, 4, 5).
2. Alias sites: an all-domains `/wp-admin/*` rule blocks `/siteA/wp-admin/x` and `/siteB/wp-admin/x`; a rule scoped to one domain does not touch the other; a request with no matched domain ignores scoped rules (Tasks 3, 4).
3. `/umbraco/...`, `/UMBRACO`, `/App_Plugins/...` are never blocked even when an accepted rule such as `/*.php` matches them (`/umbraco/x.php`); `/um*`, `*umbraco*`, `*`, `/*`, `/**` are rejected (Tasks 1, 4).
4. Startup and failure: a request during the initial load waits and is then blocked; a failing database passes requests through and retries; a failed rebuild keeps the old snapshot (Task 3).
5. Same rule re-added in another case updates the status instead of duplicating; a 801-character pattern is rejected; unblocking makes the URL tracked again from count 1; deleting records and discarding queued hits agree between the SQL and in-memory matcher (Tasks 1, 2, 5).

---

### Task 1: Pattern normalisation, matching, validation and LIKE translation (pure)

**Files:**
- Create: `Core/Blocking/BlockingConstants.cs`; `Infrastructure/Blocking/BlockPatternNormalizer.cs`, `CompiledBlockPattern.cs`, `BlockPatternValidator.cs`, `BlockLikePattern.cs`
- Modify: `Infrastructure/Tracking/TrackedUrlNormalizer.cs` (make `NormalizePath(string)` and `RelativeTo(string path, string domainName)` `internal`)
- Test: `Infrastructure.Tests/Blocking/BlockPatternNormalizerTests.cs`, `CompiledBlockPatternTests.cs`, `BlockPatternValidatorTests.cs`, `BlockLikePatternTests.cs`

**Interfaces:**
- Produces:
  - `public static class BlockingConstants { const string TableName = "pageNotFoundTrackerBlockRule"; const int MinStatusCode = 400, MaxStatusCode = 499, DefaultStatusCode = 410, MaxPatternLength = 800; }`
  - `BlockPatternNormalizer.Normalize(string pattern): string` (also ensures a leading `/`).
  - `CompiledBlockPattern.Compile(string normalisedPattern): CompiledBlockPattern` with `bool IsMatch(string normalisedPath)` and `int LiteralPrefixLength` (characters before the first `*`).
  - `BlockPatternValidator.Validate(string normalisedPattern, int statusCode): string?` (null when valid, else the message; the owned-path message names the path).
  - `BlockLikePattern.ToLike(string normalisedPattern): string` (`*` to `%`, `\`, `%`, `_`, `[` escaped with `\`, use with escape `\`).

- [ ] **Step 1: Write failing tests.** Normalizer: `/WP-Admin/` and `/wp-admin` equal; `//a///b/` becomes `/a/b`; `%2E`-style escapes decoded once; empty or `/` gives `/`. Matcher (`[TestCase]` table): `/wp-admin/*` matches `/wp-admin/`, `/wp-admin/a/b/c`, not `/wp-admin` (document it) and not `/x/wp-admin/a`; `/*.php` matches `/a/b/x.php`; `/a*c*e` matches `/abcde` and `/ace`; no wildcard means exact match; `LiteralPrefixLength("/wp-*")` is 4. Validator: `*`, `/*`, `/**`, `//*/` rejected as having no literal; `/um*`, `/*umbraco*`, `/umbraco`, `/umbraco/x*`, `/App_Plugins/*` rejected naming the path; `/*.php`, `/wp-admin/*` accepted; 801 characters rejected; status 399 and 500 rejected, 410 accepted. LIKE: `/a_b%c[d]\e*` gives `/a\_b\%c\[d]\\e%`; plus a parity test `Like_and_matcher_agree` that, on an in-memory SQLite database, inserts a set of paths into a scratch table and asserts `LIKE ... ESCAPE '\'` selects exactly the paths `IsMatch` accepts for a pattern table that includes `%`, `_`, `[` and `\` in both patterns and paths.
- [ ] **Step 2: Run** `dotnet test src --filter FullyQualifiedName~Blocking` and confirm FAIL (types missing).
- [ ] **Step 3: Implement** the members above. `IsMatch`: split on `*`, anchored start, greedy leftmost search of the middle literals, anchored end, ordinal-ignore-case. `Validate` owned-path rule: compare the pattern's literal prefix and the pattern against each `UmbracoOwnedPaths.Prefixes` entry (lower-cased): reject when `IsMatch(prefix)` or the literal prefix equals the prefix or starts with `prefix + "/"`.
- [ ] **Step 4: Run** the same filter; expected PASS.
- [ ] **Step 5: Commit** `feat(blocking): add pattern normalisation, matching, validation and LIKE translation`.

### Task 2: Rule entity, DbContext mapping and migrations

**Files:**
- Create: `Core/Blocking/BlockRule.cs`; migrations in both `Persistence.Sqlite/Migrations/` and `Persistence.SqlServer/Migrations/` (through the script)
- Modify: `Infrastructure/Tracking/PageNotFoundTrackerDbContext.cs`
- Test: `Integration.Tests/Blocking/BlockRuleMigrationTests.cs` (uses `TrackingDatabaseFixture`, SQLite and SQL Server)

**Interfaces:**
- Produces: `public class BlockRule { int Id; string Pattern (normalised); string DisplayPattern; int StatusCode; string Domain; string Culture; DateTime Created (UTC) }`; `DbSet<BlockRule> BlockRules`. Columns: `Pattern` 800, `DisplayPattern` 800, `Domain` 255 default `''`, `Culture` 50 default `''`, unique index on (`Pattern`, `Domain`, `Culture`). Index size must stay under SQL Server's 1700-byte key limit: 800 + 255 + 50 characters at 2 bytes is 2210, so hash the key like tracking does: add `PatternHash` (SHA-256 hex, 64) to the entity and make the unique index (`PatternHash`, `Domain`, `Culture`), as `UrlHash` does for tracked URLs.

- [ ] **Step 1: Write failing tests** `Migration_creates_the_block_rule_table`, `Unique_index_rejects_the_same_pattern_domain_and_culture`, `Same_pattern_with_another_domain_is_allowed`, `Existing_tracking_table_data_survives_the_migration`.
- [ ] **Step 2: Run** `dotnet test src --filter FullyQualifiedName~BlockRuleMigration`; FAIL.
- [ ] **Step 3: Implement** the entity and mapping (`BlockingConstants.TableName`), then `pwsh scripts/add-migration.ps1 -Name AddBlockRules`.
- [ ] **Step 4: Run** the tests and `pwsh scripts/check-pending-model-changes.ps1`; expected PASS, no pending changes, zero build warnings.
- [ ] **Step 5: Commit** `feat(blocking): add the block rule table and migrations`.

### Task 3: Domain selection, snapshot, snapshot provider and startup service

**Files:**
- Create: `Infrastructure/NotFound/DomainEntry.cs`, `DomainSelector.cs`; `Infrastructure/Blocking/BlockSnapshot.cs`, `IBlockSnapshotProvider.cs`, `BlockSnapshotProvider.cs`, `BlockSnapshotService.cs` (hosted), `BlockSnapshotInvalidationHandler.cs`
- Modify: `Infrastructure/NotFound/RequestDomainMatcher.cs` (delegate to `DomainSelector`, behaviour unchanged), `Infrastructure/Extensions/UmbracoBuilderExtensions.cs`
- Test: `Infrastructure.Tests/NotFound/DomainSelectorTests.cs`, `Infrastructure.Tests/Blocking/BlockSnapshotTests.cs`, `BlockSnapshotProviderTests.cs`; existing `RequestDomainMatcher` tests stay green

**Interfaces:**
- Consumes: Task 1 (`CompiledBlockPattern`, `BlockPatternNormalizer`), Task 2 (`BlockRule`, `DbContext`), `ITrackingPersistenceState`, `TrackedUrlNormalizer.NormalizePath/RelativeTo`.
- Produces:
  - `internal sealed record DomainEntry(string Name, string Host, int? Port, string Path, string Culture)`; `DomainSelector.Select(IReadOnlyList<DomainEntry> domains, string host, int? port, string path): DomainEntry?` (same longest-path rule as today); `DomainSelector.FromCache(IEnumerable<Domain>)`.
  - `internal sealed class BlockSnapshot`: `BlockSnapshot.Empty`; `BlockSnapshot.Build(IEnumerable<BlockRule> rules, IReadOnlyList<DomainEntry> domains)`; `bool NeedsDomainMatch` (any scoped rule or any domain with a path); `int? FindStatus(string path, string host, int? port)` (path normalised by the caller's rules; applies alias stripping, scope and precedence; returns the status code or null; owned paths return null).
  - `internal interface IBlockSnapshotProvider { ValueTask<BlockSnapshot> GetAsync(CancellationToken ct); Task RefreshAsync(CancellationToken ct); }`. `GetAsync` returns the current snapshot at once after the first load, otherwise waits for the shared initial load (max 5 s, constructor-injectable `TimeSpan` for tests), then returns `Empty` and logs a warning. `RefreshAsync` builds a complete snapshot and swaps it with `Volatile.Write`; on exception it logs and keeps the old one (never throws to the caller except cancellation). `BlockSnapshotService` waits for `RuntimeLevel.Run` and a non-`Pending` persistence state, loads once, on failure retries every 30 s (injectable) until success; with `Unavailable` it stays on `Empty`. `BlockSnapshotInvalidationHandler` handles `DomainCacheRefresherNotification` and `LanguageCacheRefresherNotification` by calling `RefreshAsync`.

- [ ] **Step 1: Write failing tests.** Selector: host, port and longest path win; trailing-slash request; no match gives null (copy cases from the existing matcher tests). Snapshot: all-domain rule matches on every domain and with no domain; domain-scoped rule matches only its domain and never with no domain; culture-scoped rule needs the domain's culture; `/wp-admin/*` all-domains matches `/sitea/wp-admin/x` under domain `localhost:44402/sitea` (alias stripped); precedence order (domain over all, culture over all, longer literal prefix, lowest status) as a `[TestCase]` table; owned path returns null even with a matching `/*` style rule built directly; `NeedsDomainMatch` false for only all-domain rules and no path domains. Provider: `Request_during_initial_load_waits_and_gets_the_loaded_snapshot` (gate the loader with a `TaskCompletionSource`); `Initial_load_taking_longer_than_the_wait_passes_through_with_empty`; `Failed_initial_load_is_retried_in_the_background`; `Refresh_swaps_while_a_reader_still_sees_the_old_snapshot` (reader holds the old reference, new `GetAsync` returns the new); `Failed_rebuild_keeps_the_old_snapshot`; `Unavailable_persistence_returns_empty_without_waiting`.
- [ ] **Step 2: Run** the filters; FAIL.
- [ ] **Step 3: Implement.** Sort rules once in `Build`: domain-scoped first, then culture-scoped, then `LiteralPrefixLength` descending, then `StatusCode` ascending; `FindStatus` returns the first match. Load rules with `IDbContextFactory<PageNotFoundTrackerDbContext>` resolved per call from `IServiceProvider` (as `TrackedUrlQueryService`) and domains from `IDomainCache.GetAll(false)`. Register provider and handlers in `AddPageNotFoundTracker()` with `AddUnique`, the hosted service with `AddHostedService`.
- [ ] **Step 4: Run** `dotnet test src --filter "FullyQualifiedName~Blocking|FullyQualifiedName~DomainSelector|FullyQualifiedName~RequestDomainMatcher|FullyQualifiedName~NotFound"`; expected PASS.
- [ ] **Step 5: Commit** `feat(blocking): add the snapshot, its provider and startup load`.

### Task 4: Block middleware and registration

**Files:**
- Create: `Infrastructure/Blocking/BlockMiddleware.cs`
- Modify: `Infrastructure/Extensions/ApplicationBuilderExtensions.cs` (add internal `UseBlocking()` guarded by a `Properties` key like `UseFileNotFoundHandling`), `Umbraco.Community.PageNotFoundTracker/Extensions/WebApplicationExtensions.cs` (call `UseBlocking()` before `UseFileNotFoundHandling()`), the integration test `PageNotFoundTrackerStartupFilter` (add `UseBlocking()` first)
- Test: `Infrastructure.Tests/Blocking/BlockMiddlewareTests.cs`; `Integration.Tests/Blocking/BlockMiddlewareIntegrationTests.cs`

**Interfaces:**
- Consumes: `IBlockSnapshotProvider.GetAsync`, `BlockSnapshot.FindStatus`, `BlockSnapshot.NeedsDomainMatch`, `DomainSelector`, `UmbracoOwnedPaths`.
- Produces: `internal sealed class BlockMiddleware(RequestDelegate next)` with `InvokeAsync(HttpContext)`: owned path gives `next`; otherwise take the snapshot, normalise `Request.Path.Value`, match the domain only when `NeedsDomainMatch` (host and port from `Request.Host`), `FindStatus`; a hit sets `StatusCode`, `ContentLength = 0`, does not call `next`; a miss calls `next`. Never touches `INotFoundRecorder`.

- [ ] **Step 1: Write failing tests.** Unit (fake provider): blocked path answers the rule status with empty body for `GET`, `HEAD`, `POST`; query string ignored; owned path passes through; unmatched passes through; next not called on a block; the provider's wait is honoured. Integration (`PageNotFoundTestBase`, rules inserted through the DbContext and `IBlockSnapshotProvider.RefreshAsync`): blocked missing path gets 410 with empty body and the tracking table stays empty after a forced flush; blocked path that is an existing published page is blocked too; a physical file under `wwwroot` is blocked (proves ordering ahead of static files; add a throwaway file in test setup); a block beats a redirect registered for the same URL (`IRedirectUrlService`); `/umbraco/management/api/v1/server/status` is never blocked; blocking works with `TrackingEnabled=false`.
- [ ] **Step 2: Run** `dotnet test src --filter FullyQualifiedName~BlockMiddleware`; FAIL.
- [ ] **Step 3: Implement** the middleware and registration; update the XML remarks on `UsePageNotFoundTracker()` to say it also enables blocking.
- [ ] **Step 4: Run** the filter; expected PASS. If the static-file case does not pass with `UseBlocking()` ahead of `UseUmbraco()` in the test host, stop and report: the spec treats ordering as a requirement.
- [ ] **Step 5: Commit** `feat(blocking): answer blocked requests before routing and static files`.

### Task 5: Rule service (create, update, delete, query), queue discard, scope options

**Files:**
- Create: `Core/Blocking/IBlockRuleService.cs`, `BlockRuleQuery.cs` (same shape as `TrackedUrlQuery`: skip, take, order column enum `Pattern|StatusCode|Domain|Culture|Created` default `Created` descending, `Search`, domain and culture filter as `TrackedUrlFilter`), `BlockRulePage.cs`, `CreateBlockRuleOutcome.cs`, `BlockScopeOptions.cs`; `Infrastructure/Blocking/BlockRuleService.cs`
- Modify: `Infrastructure/Tracking/TrackingQueue.cs` (add `DiscardMatching`), `Infrastructure/Extensions/UmbracoBuilderExtensions.cs`
- Test: `Infrastructure.Tests/Tracking/TrackingQueueDiscardMatchingTests.cs`, `Infrastructure.Tests/Blocking/BlockRuleServiceTests.cs`; `Integration.Tests/Blocking/BlockRuleServiceTests.cs` (SQLite and SQL Server via the fixture)

**Interfaces:**
- Consumes: Tasks 1 to 3, `ITrackingFlusher.RunExclusiveAsync`, `IBlockSnapshotProvider.RefreshAsync`.
- Produces:
  - `TrackingQueue.DiscardMatching(Func<TrackedUrlHit, bool> predicate): int` (channel, pending, in-flight; takes the queue lock; same removal logic as `Discard`).
  - `public enum CreateBlockRuleStatus { Saved, Invalid }`; `public sealed record CreateBlockRuleOutcome(CreateBlockRuleStatus Status, string? Message, bool RecordsRemoved)`.
  - `public interface IBlockRuleService { Task<BlockRulePage> QueryAsync(BlockRuleQuery query, CancellationToken ct); Task<CreateBlockRuleOutcome> CreateAsync(string pattern, int statusCode, string domain, string culture, CancellationToken ct); Task<bool> DeleteAsync(int id, CancellationToken ct); Task<BlockScopeOptions> GetScopeOptionsAsync(CancellationToken ct); }`; `BlockScopeOptions(IReadOnlyList<string> Domains, IReadOnlyList<string> Cultures)` from `IDomainService`/`ILanguageService` (domain names lower-cased, language ISO codes), `AddUnique`.
  - `CreateAsync` order: normalise and validate (`Invalid` with the message); upsert by (`PatternHash`, domain, culture) updating `StatusCode` when it exists (domain lower-cased, culture trimmed); `snapshotProvider.RefreshAsync`; send the refresher (Task 6 adds the call; here depend on an `IBlockRuleChangeNotifier` seam: `Task NotifyAsync(BlockRuleCachePayload payload, CancellationToken ct)`, default no-op until Task 6); then inside `RunExclusiveAsync`: `DiscardMatching` on hits whose `RelativePath` matches the compiled pattern and whose domain and culture are in scope, and `ExecuteDeleteAsync` on `TrackedUrls` where `RelativePath LIKE pattern ESCAPE '\'` (`EF.Functions.Like(path, like, "\\")`) and domain and culture equal the rule's (skipped when empty); a thrown delete is logged, `RecordsRemoved=false`, the rule stays. `DeleteAsync` removes the rule, refreshes, notifies; false when the id is unknown.

- [ ] **Step 1: Write failing tests.** Queue: `DiscardMatching_removes_matching_hits_from_channel_pending_and_in_flight_only`. Service unit (mocks): invalid pattern returns `Invalid` and saves nothing; existing identity updates the status (one row); delete failure keeps the rule and returns `RecordsRemoved=false`; delete of unknown id is false. Integration: `Create_deletes_matching_rows_in_scope_and_keeps_others` (rows with `/wp-admin/a`, `/wp-admin/b?x=1` style `IncludeQueryString` rows, `/other`, another domain and another culture, a path containing `_` and `%`); `Create_wildcard_with_underscore_does_not_over_delete`; `Like_delete_and_matcher_select_the_same_rows` (parity over the stored rows); `Create_discards_a_queued_hit_so_the_row_does_not_return`; `Same_pattern_other_case_updates_status_not_duplicate`; `Delete_then_hit_is_tracked_again_from_one`; `Query_filters_sorts_and_pages` (mirror `TrackedUrlQueryServiceTests` coverage for the five columns); `Scope_options_list_configured_domains_and_languages`.
- [ ] **Step 2: Run** `dotnet test src --filter FullyQualifiedName~Blocking`; FAIL.
- [ ] **Step 3: Implement** the contracts, service and queue method.
- [ ] **Step 4: Run** the filter; expected PASS (SQL Server cases skipped without `PNFT_SQLSERVER_CONNECTION`; run them once locally with it set).
- [ ] **Step 5: Commit** `feat(blocking): add the rule service with delete and queue discard`.

### Task 6: Distributed cache refresher

**Files:**
- Create: `Core/Blocking/BlockRuleCachePayload.cs`; `Infrastructure/Blocking/BlockRuleCacheRefresher.cs`, `BlockRuleCacheRefresherNotification.cs`, `DistributedBlockRuleChangeNotifier.cs`
- Modify: `Infrastructure/Extensions/UmbracoBuilderExtensions.cs` (`builder.CacheRefreshers().Add<BlockRuleCacheRefresher>()`, replace the no-op notifier)
- Test: `Infrastructure.Tests/Blocking/BlockRuleCacheRefresherTests.cs`; `Integration.Tests/Blocking/BlockRuleCacheRefresherIntegrationTests.cs`

**Interfaces:**
- Produces: `public enum BlockRuleChange { Saved, Removed }`; `public sealed record BlockRuleCachePayload(string Pattern, string Domain, string Culture, BlockRuleChange Change)`; `BlockRuleCacheRefresher : PayloadCacheRefresherBase<BlockRuleCacheRefresherNotification, BlockRuleCachePayload>` with a fixed `UniqueId` GUID (new, constant, documented) and name `Page Not Found Tracker Block Rules`; `RefreshInternal(payloads)` calls `IBlockSnapshotProvider.RefreshAsync` and, for `Saved` payloads, `ITrackingFlusher.RunExclusiveAsync(() => queue.DiscardMatching(...))` for the payload's pattern and scope. `DistributedBlockRuleChangeNotifier.NotifyAsync` calls `DistributedCache.RefreshByPayload(BlockRuleCacheRefresher.UniqueId, [payload])` (Umbraco's `DistributedCache` extension).

- [ ] **Step 1: Write failing tests.** Unit: `Saved_payload_rebuilds_snapshot_and_discards_matching_queued_hits_only`; `Removed_payload_rebuilds_without_discarding`; `Refresher_failure_in_rebuild_does_not_throw` (old snapshot kept). Integration (a second server cannot be started, so assert through the real composition): `Payload_refresh_on_the_registered_refresher_makes_a_rule_inserted_directly_in_the_database_apply` (insert a rule row without the service, invoke `ICacheRefresherNotificationFactory`/`CacheRefresherCollection` refresher with a `Saved` payload, assert the middleware blocks); `Refresher_is_registered_in_the_collection`.
- [ ] **Step 2: Run** the filter; FAIL.
- [ ] **Step 3: Implement.** Follow `umbraco-pagespeed-optimization`'s refresher pattern (`PayloadCacheRefresherBase` serialises the payload through `IJsonSerializer`).
- [ ] **Step 4: Run** `dotnet test src --filter FullyQualifiedName~BlockRuleCacheRefresher`; expected PASS.
- [ ] **Step 5: Commit** `feat(blocking): refresh rule snapshots on every server through the distributed cache`.

### Task 7: Endpoints and generated client

**Files:**
- Create: `BackOffice/Controllers/GetBlockRulesController.cs`, `CreateBlockRuleController.cs`, `DeleteBlockRuleController.cs`, `GetBlockScopeOptionsController.cs`; `BackOffice/Models/BlockRuleResponseModel.cs`, `CreateBlockRuleRequestModel.cs`, `CreateBlockRuleResponseModel.cs`, `BlockScopeOptionsResponseModel.cs`
- Modify: `Client/src/api/*` (regenerated)
- Test: `BackOffice.Tests/Controllers/BlockRuleControllersTests.cs`; `Integration.Tests/Api/BlockRulesApiTests.cs` (real tokens)

**Interfaces:**
- Produces (base `/umbraco/pagenotfoundtracker/api/v1`, policy `PageNotFoundTracker.DashboardAccess`, derive `TrackedUrlsControllerBase`):
  - `GET block-rules`, `GetBlockRules`: query `skip`, `take` (as `GetTrackedUrls`), `orderBy` (`Pattern|StatusCode|Domain|Culture|Created`, default `Created`), `direction` (default `Descending`), `search`, `domain`, `noDomain`, `culture`, `noCulture`; 200 `PagedViewModel<BlockRuleResponseModel>`; 400; 503. Item: `id`, `pattern` (the `DisplayPattern`), `statusCode`, `domain`, `culture`, `created` (UTC with `Z`).
  - `POST block-rules`, `CreateBlockRule`: body `{ pattern, statusCode, domain, culture }`; 200 `{ recordsRemoved, message }`; 400 problem details with the validation message in `detail`; 503; 401/403.
  - `DELETE block-rules/{id:int}`, `DeleteBlockRule`: 200 empty; 404; 503.
  - `GET block-rules/scope-options`, `GetBlockScopeOptions`: 200 `{ domains, cultures }`; 503.
  - Rule search works as tracked URL search on the normalised pattern (`LikePattern.Contains`).

- [ ] **Step 1: Write failing tests**: one controller unit test per status above with a mocked `IBlockRuleService`; integration: 401 anonymous, 403 outside groups, 400 for `*`, `/um*` and `/*` with the message in `detail`, 200 create then list shows it, create twice updates the status (one item), delete 200 then 404, scope options include a configured domain, swagger document contains the four operation IDs, `GET block-rules?orderBy=Bogus` is 400.
- [ ] **Step 2: Run** `dotnet test src --filter FullyQualifiedName~BlockRule`; FAIL.
- [ ] **Step 3: Implement** the controllers (`EnsurePersistenceAvailableAsync` as the others) and models.
- [ ] **Step 4: Run** `dotnet test src`; expected PASS, zero warnings.
- [ ] **Step 5: Regenerate the client** with a site running (`NODE_TLS_REJECT_UNAUTHORIZED=0 npm run generate-client` in the client project); commit only `index.ts`, `sdk.gen.ts`, `types.gen.ts` real changes and revert line-ending-only ones; no origin in the diff.
- [ ] **Step 6: Commit** `feat(blocking): add the block rule endpoints and regenerate the client`.

### Task 8: Client: blocked rules dashboard, Block and Unblock actions, rule modal

**Files (under `Client/src/blocking/` unless stated):**
- Create: `constants.ts` (`PNFT_BLOCK_RULE_ENTITY_TYPE = 'page-not-found-tracker-block-rule'`, collection, repository, view, filter aliases, dashboard alias `Umbraco.Community.PageNotFoundTracker.Dashboard.BlockRules`, pathname `blocked-urls`, weight 6), `data-marks.ts`, `manifests.ts`, `collection/` (mirrors `src/collection/`: `block-rule-collection.context.ts` and its token, `repository/block-rule.data-source.ts`, `.mapping.ts` (+ `.test.ts`), `.repository.ts`, `query.ts` (+ `.test.ts`), `views/table/block-rule-table.element.ts` and `block-rule-table.columns.ts` (+ `.test.ts`), `filters/` reusing `filter-choices.ts`, `search.element.ts` and the state elements from `src/collection/` wherever they are not tracked-URL specific, extracting shared pieces rather than copying), `dashboards/blocked-urls-dashboard.element.ts`, `add-rule.collection-action.ts`, `block.action.ts` (entity action on `PNFT_TRACKED_URL_ENTITY_TYPE`, alias `Pnft.EntityAction.TrackedUrl.Block`, icon `icon-block`), `unblock.action.ts` (entity action on `PNFT_BLOCK_RULE_ENTITY_TYPE`, confirmation through `UMB_CONFIRM_MODAL`, alias `Pnft.EntityAction.BlockRule.Unblock`), `rule-modal.token.ts`, `rule-modal.element.ts` (alias `Pnft.Modal.CreateBlockRule`), `block-rule.service.ts` (+ `.test.ts`)
- Modify: `src/manifests.ts`, `src/localization/en.ts`, `src/conditions/` only if the dashboard condition needs a new manifest reference (it is reused as is)
- Test: Vitest as listed

**Interfaces:**
- Produces: modal data `{ pattern, domain, culture, statusCode, title }`; modal value `{ saved: true, recordsRemoved, message }` or undefined. Fields: pattern text input (required, hint that `*` is a wildcard), domain select (`All domains` plus `GetBlockScopeOptions` domains, with the row's domain added when missing), culture select likewise, status select 403, 404, 410 (default 410), a static warning that rules apply to every matching request, not only missing pages; inline server `detail` on 400. "Block" preselects `relativePath`, `domain`, `culture` from the collection context item read by `unique` (same lookup as `create-redirect.action.ts`), is available on page and file rows, and after success calls the tracked collection context's `loadCollection()` and shows a success toast (warning toast when `recordsRemoved` is false). "Add rule" opens the modal with empty pattern and "all". "Unblock" confirms, deletes through the service, reloads the blocked collection. `block-rule.service` maps 200, 400 (message), 404 and network failures to a result union as `redirect.service.ts` does. Empty domain and culture cells show `#pageNotFoundTracker_allDomains` and `#pageNotFoundTracker_allCultures`. Default order: created descending. Dashboard conditions as the tracked dashboard (`Umb.Condition.SectionAlias` = content, `Pnft.Condition.DashboardAccess`).

- [ ] **Step 1: Write failing Vitest tests**: `block-rule.service` result mapping (200 with and without removal failure, 400 message, 404, network error); `block-rule.mapping` (API item to collection model, empty domain and culture); `query.ts` (orderBy and direction mapping, `PNFT_EMPTY` to `noDomain`/`noCulture`, `filter` to `search`); table columns (five columns, all sortable, default sort); modal pure helper `buildRuleDefaults(trackedUrl)` (relative path, domain, culture preselected; status 410); `manifests.test.ts` that the block action targets the tracked entity type and unblock the rule entity type.
- [ ] **Step 2: Run** `npm test`; FAIL.
- [ ] **Step 3: Implement** the files above and register them; add all `pageNotFoundTracker_*` texts.
- [ ] **Step 4: Run** `npm test && npm run build`; expected PASS and a clean build with no `localhost` in `wwwroot/App_Plugins`.
- [ ] **Step 5: Verify in the browser** (`umbraco-chrome-navigation`, `single-site`, `FlushInterval=00:00:01`): the second tab appears after Page not found; a page row and a `.css` row both show a dropdown with Create redirect (page row only) and Block; blocking a row removes it and the URL answers 410 with an empty body; Add rule with `/wp-admin/*` then a request to `/wp-admin/x/y` answers 410; filters, search, sort and paging work; Unblock confirms and the URL is tracked again. Check the 17.8 caveats in `CLAUDE.md` (dropdown with two actions, cold deep link).
- [ ] **Step 6: Commit** `feat(blocking): add the blocked URLs dashboard and the block and unblock actions`.

### Task 9: E2E and redirect test selectors

**Files:**
- Create: `E2E/tests/blocked.spec.ts`
- Modify: `E2E/tests/redirect.spec.ts` (the row action is now inside a dropdown), E2E data-mark copies (`blocking` marks)

- [ ] **Step 1: Write the E2E tests** on `single-site` with run-scoped prefixes: `Block_from_a_row_moves_the_url_to_the_blocked_list_and_answers_410` (send a 404, wait for the row, Block with defaults, row leaves the tracked list, appears on the blocked tab, request answers 410 with an empty body, a second request does not create a tracked row after a flush wait); `Add_rule_with_wildcard_blocks_a_sub_path`; `Unblock_makes_the_url_trackable_again`; `Umbraco_owned_pattern_is_rejected_with_a_message` (`/umbraco/x*`); update the redirect spec to open the dropdown first.
- [ ] **Step 2: Run** the whole suite as in `CLAUDE.md` (short flush interval). Expected: PASS.
- [ ] **Step 3: Commit** `test(blocking): add the blocked URLs E2E tests and adapt the redirect tests`.

### Task 10: Manual checks, documentation and full verification

**Files:**
- Modify: `CLAUDE.md`

- [ ] **Step 1: Manual checks** (restore databases with `git checkout` afterwards): `multi-site-aliases`: an all-domains `/wp-admin/*` rule gives the rule status on `/siteA/wp-admin/x` and `/siteB/wp-admin/x`, a rule scoped to `localhost:44402/sitea` affects only Site A; `multi-language`: a culture-scoped rule affects only that culture's domain (`/nl/...` vs `/en/...`) and a no-domain request ignores it; `sql-server` profile: create, block and unblock work; run the integration tests with `PNFT_SQLSERVER_CONNECTION` set.
- [ ] **Step 2: Update `CLAUDE.md`**: status and spec list (spec 6 done, plan path), a "Blocked URLs (spec 6)" section (rule model and identity, normalisation and matching, precedence, domain handling and alias stripping, safeguards and the overlap rule, snapshot lifecycle and timings, middleware placement and that `UsePageNotFoundTracker()` enables it, the refresher and payload, endpoints table, delete mechanism and its LIKE parity, client pieces, aliases and data marks, test list, manual check results, known limits), the solution layout entries, and the dropdown note in the row actions text.
- [ ] **Step 3: Full verification**: `npm test`, `npm run build`, `dotnet build` (zero warnings), `dotnet test`, `pwsh scripts/check-pending-model-changes.ps1`, pack plus `pwsh .github/scripts/check-package.ps1`.
- [ ] **Step 4: Commit** `docs(blocking): document blocked URLs and their extension points`.

---

## Self-review

- **Spec coverage:** model and uniqueness (2), matching and normalisation (1), precedence and alias stripping (3), middleware, ordering, all methods, no DB read (3, 4), safeguards (1, 4), status codes (1, 7, 8), snapshot lifecycle and failure behaviour (3), refresher and queue discard on all servers (5, 6), delete parity on both databases (5), unblock (5, 8), second dashboard with list capabilities and states (8), Block on page and file rows plus Add rule (8), endpoints and client (7), migrations and pending check (2), tests and E2E and manual (all, 9, 10), `CLAUDE.md` (10).
- **Types:** `BlockRule`, `CompiledBlockPattern`, `BlockSnapshot.FindStatus`, `IBlockSnapshotProvider`, `BlockRuleCachePayload`, `CreateBlockRuleOutcome` and the four operation IDs keep the same names throughout.
- **Known deviation from the spec text:** the unique index is on a pattern hash (not the pattern) because of SQL Server's index key limit, as tracking does for URLs.
