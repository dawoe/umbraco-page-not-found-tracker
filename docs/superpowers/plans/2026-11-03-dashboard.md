# Backoffice Dashboard Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** A dashboard in the Content section that lists the tracked 404 URLs (spec 3) with sorting, URL search, domain and
culture filters and paging, served by a Management API, visible only to the configured user groups, and built on
Umbraco's collection framework so specs 5 and 6 can add row actions.

**Architecture:** `ITrackedUrlQueryService` (Core contract, Infrastructure implementation over `PageNotFoundTrackerDbContext`)
does all sorting, filtering, searching and paging in the database. Three Management API endpoints in `.BackOffice`
(list, filter options, access) sit in their own OpenAPI document; the list and filter options are guarded by an
authorization policy backed by `IDashboardAccess`. The client project gets a committed `@hey-api/openapi-ts` client, a
collection (repository, table view, search, filters, page size) hosted by a Content-section dashboard, and a condition that
asks the access endpoint, so the UI and API share one rule.

**Tech Stack:** .NET 10, Umbraco 17.8.x Management API (`Umbraco.Cms.Api.Management`), EF Core, NUnit + Moq, Umbraco
integration test base, TypeScript + Lit + Vite, Umbraco backoffice 17.8 collection framework, `@hey-api/openapi-ts`,
Vitest (pure client logic only), Playwright.

**Spec:** `docs/superpowers/specs/2026-11-01-dashboard-design.md` (issue #4, approved). Read it first; "Calls made in this
spec" is binding. Load these skills before client work: `umbraco-cms-backoffice-skills:umbraco-version-guard`, `-umbraco-dashboard`,
`-umbraco-collection`, `-umbraco-collection-view`, `-umbraco-conditions`, `-umbraco-openapi-client`, `-umbraco-localization`,
`-umbraco-entity-actions`, `-umbraco-chrome-navigation` (manual checks), and `-umbraco-e2e-testing`.

## Global Constraints

- Branch `feature/4-dashboard` (already created off `develop`), one PR to `develop`. Commits end with the session's `Co-Authored-By` line.
- Umbraco 17 conventions only (never 18): reference repositories' `v17/develop` branches, not `develop`.
- Layering (`CLAUDE.md`): contracts and models in `.Core`; query service and access evaluator in `.Infrastructure`; controllers,
  models, policy and OpenAPI setup in `.BackOffice`; client in `.BackOffice.Client`; entry project only wires. Every package
  composer keeps `[ComposeBefore(typeof(PageNotFoundTrackerComposer))]`; replaceable services use `AddUnique`.
- New `PackageReference` in a sibling project must be mirrored in the entry csproj (none expected here).
- Setting under `Umbraco:Community:PageNotFoundTracker`: `DashboardUserGroups` (string list of user group aliases, default
  `admin` and `editor`), read once at startup. `admin` always passes; empty or invalid falls back to the defaults with a warning.
- Page size: 20 (default), 50 or 100 in the UI; server clamps `take` to 1 to 100. Default order: hits descending, ties broken by `Id`.
- Search: case-insensitive "contains" on the normalised `Url` column, term lower-cased and percent-decoded, `%`, `_`, `[`
  and the escape character treated literally. Domain/culture filters have an explicit "empty" marker distinct from "all".
- API returns `FirstSeen`/`LastSeen` as UTC ISO 8601; list items also carry `RelativePath` (not a column).
- Generated client committed in the client project; the OpenAPI file is not committed; no server base URL in the shipped bundle.
- Texts go through localization (English in the box), keys prefixed `pageNotFoundTracker_`.
- StyleCop zero warnings, copyright header, XML docs on public and internal members, `internal sealed` NUnit fixtures. Interactive
  client elements carry `data-mark`. Node 24.x.
- Never commit `.sqlite.db` / `appsettings.json` changes from profile runs (restore with `git checkout`).

## Review Focus

1. Search text of `%`, `_`, `[`, `\`, `%2541`, upper-case letters, only whitespace, or 3,000 characters: literal and case-insensitive,
   whitespace-only means no search, nothing throws (Task 2).
2. `skip` negative or huge, `take` of 0, -1 or 1,000,000, and a page past the end: clamped or rejected (400), past-the-end gives
   empty items with the true total (Tasks 2, 3).
3. Both `domain` and `noDomain` (or `culture` and `noCulture`) in one request, and a real domain literally named like the empty
   marker: 400 for the first, correct rows for the second (Tasks 2, 3).
4. `DashboardUserGroups` that is null, empty, whitespace entries, mixed case (`Editor`), or only unknown aliases: defaults plus one
   warning; user with no groups; `admin` omitted from the setting (Task 1).
5. Persistence `Pending` at request time, `Unavailable`, and zero tracked rows (no domains, no cultures): 503 problem details,
   unavailable state in the UI, empty filter options and the empty-state text (Tasks 3, 5).

---

### Task 1: Access rule

**Files:**
- Modify: `src/code/Umbraco.Community.PageNotFoundTracker.Core/Configuration/PageNotFoundTrackerSettings.cs`
- Create: `src/code/Umbraco.Community.PageNotFoundTracker.Core/Dashboard/IDashboardAccess.cs`
- Create: `src/code/Umbraco.Community.PageNotFoundTracker.Infrastructure/Dashboard/DashboardAccess.cs`
- Modify: `src/code/Umbraco.Community.PageNotFoundTracker.Infrastructure/Extensions/UmbracoBuilderExtensions.cs`
- Test: `src/test/Umbraco.Community.PageNotFoundTracker.Infrastructure.Tests/DashboardAccessTests.cs`

**Interfaces:**
- Produces: `PageNotFoundTrackerSettings.DashboardUserGroups { get; set; }` (`IList<string>`, default `["admin", "editor"]`);
  `public interface IDashboardAccess { bool IsAllowed(IEnumerable<string> userGroupAliases); }`; `DashboardAccess` is
  `internal sealed`, singleton, registered with `AddUnique<IDashboardAccess, DashboardAccess>()`.

- [ ] **Step 1: Write failing tests** in `DashboardAccessTests` (build `DashboardAccess` from `Options.Create(settings)` and a `Mock<ILogger<DashboardAccess>>`):
  `IsAllowed_DefaultSettings_AdminAndEditorPass_WriterFails`, `IsAllowed_AdminAlwaysPasses_WhenSettingOmitsIt`,
  `IsAllowed_IsCaseInsensitiveOnAlias` (`Editor` setting, `editor` user), `IsAllowed_NoGroups_Fails`,
  `Constructor_NullEmptyOrWhitespaceOnlySetting_FallsBackToDefaultsAndLogsOneWarning`,
  `Constructor_UnknownAliasesOnly_AreKeptAndAdminStillPasses` (an unknown alias is valid input, not "invalid": no warning),
  `Constructor_ValidSetting_LogsNoWarning`.
- [ ] **Step 2: Run** `dotnet test src --filter FullyQualifiedName~DashboardAccessTests`. Expected: FAIL (types missing).
- [ ] **Step 3: Implement** the setting, interface and `DashboardAccess`. Entries are trimmed, blanks dropped, matching is
  `StringComparer.OrdinalIgnoreCase`; `admin` is added to the effective set. "Invalid" means null, empty, or only blank entries.
  The effective set is computed once in the constructor (settings are read at startup). Register in `AddPageNotFoundTracker`.
- [ ] **Step 4: Run** the filter again plus `dotnet build src`. Expected: PASS, zero warnings.
- [ ] **Step 5: Commit** `feat(dashboard): add the dashboard access rule and DashboardUserGroups setting`.

### Task 2: Query service

**Files:**
- Create in `.Core/Tracking/`: `ITrackedUrlQueryService.cs`, `TrackedUrlQuery.cs`, `TrackedUrlFilter.cs`, `TrackedUrlSortColumn.cs`,
  `TrackedUrlPage.cs`, `TrackedUrlFilterOptions.cs`
- Create: `.Infrastructure/Tracking/TrackedUrlQueryService.cs`, `.Infrastructure/Tracking/LikePattern.cs`
- Modify: `.Infrastructure/Extensions/UmbracoBuilderExtensions.cs` (register `AddUnique<ITrackedUrlQueryService, TrackedUrlQueryService>()`)
- Test: `src/test/Umbraco.Community.PageNotFoundTracker.Infrastructure.Tests/LikePatternTests.cs`;
  `src/test/Umbraco.Community.PageNotFoundTracker.Integration.Tests/Tracking/TrackedUrlQueryServiceTests.cs`

**Interfaces:**
- Produces:
  ```csharp
  public enum TrackedUrlSortColumn { Hits, Url, Domain, Culture, IsFile, FirstSeen, LastSeen }
  public readonly record struct TrackedUrlFilter  // Any (default), Empty, Equals(string value)
  public sealed record TrackedUrlQuery(int Skip, int Take, TrackedUrlSortColumn SortColumn, bool Descending,
      string? Search, TrackedUrlFilter Domain, TrackedUrlFilter Culture);
  public sealed record TrackedUrlPage(IReadOnlyList<TrackedUrl> Items, long Total);
  public sealed record TrackedUrlFilterOptions(IReadOnlyList<string> Domains, IReadOnlyList<string> Cultures,
      bool HasEmptyDomain, bool HasEmptyCulture);   // Domains/Cultures exclude the empty string, sorted ordinal
  public interface ITrackedUrlQueryService {
      Task<TrackedUrlPage> GetPageAsync(TrackedUrlQuery query, CancellationToken cancellationToken);
      Task<TrackedUrlFilterOptions> GetFilterOptionsAsync(CancellationToken cancellationToken); }
  ```
  `Skip` below 0 becomes 0, `Take` is clamped to 1 to 100 inside the service (defence in depth, the controller validates too).
  `internal static class LikePattern { string Contains(string search); }` returns the lower-cased, percent-decoded (malformed
  escapes left as typed, never throws) term wrapped in `%`, with `\`, `%`, `_`, `[` escaped by `\`; used with
  `EF.Functions.Like(x.Url, pattern, "\\")`.

- [ ] **Step 1: Write failing tests.** `LikePatternTests`: `%`, `_`, `[`, `\` each escaped; `%2541` decoded once to `%41` then escaped
  (never decoded twice); `ABC` lower-cased; malformed `%E0%A4%A` does not throw; 3,000 characters accepted.
  `TrackedUrlQueryServiceTests` use `TrackingDatabaseFixture` (runs on SQLite, and SQL Server when `PNFT_SQLSERVER_CONNECTION` is set;
  read how `TrackedUrlUpsertTests` parameterises both providers and follow it). Seed 25 rows with known counts, domains, cultures.
  Tests: `GetPageAsync_Default_OrdersByHitsDescendingThenId`, `GetPageAsync_EverySortColumn_BothDirections_OrderedWithStableTies`
  (TestCase over all seven columns, asserts a second page does not repeat or skip rows), `GetPageAsync_SearchIsCaseInsensitiveContains`,
  `GetPageAsync_SearchTreatsPercentUnderscoreBracketLiterally` (rows `/a%b`, `/a_b`, `/a[b`, `/axb`; searching `%`, `_`, `[` matches one each),
  `GetPageAsync_DomainFilter_Value_Empty_Any`, `GetPageAsync_CultureFilter_Value_Empty_Any`,
  `GetPageAsync_DomainNamedLikeTheEmptyMarker_FiltersByValueNotEmpty`, `GetPageAsync_FiltersAndSearchCombine`,
  `GetPageAsync_PagePastTheEnd_ReturnsNoItemsAndTrueTotal`, `GetPageAsync_TakeAndSkipOutOfRange_AreClamped`,
  `GetFilterOptionsAsync_ReturnsDistinctValuesAndEmptyFlags`, `GetFilterOptionsAsync_NoRows_ReturnsEmptyLists`.
- [ ] **Step 2: Run** `dotnet test src --filter "FullyQualifiedName~LikePatternTests|FullyQualifiedName~TrackedUrlQueryServiceTests"`. Expected: FAIL.
- [ ] **Step 3: Implement** the contracts, `LikePattern` and `TrackedUrlQueryService`. Obtain the context the same way
  `DefaultTrackedUrlUpsert` / `TrackingFlushService` does. Everything (filter, order, skip, take, count) is one EF query shape, no
  client-side evaluation. Sort `Url` column sorts on `Url` (normalised); every ordering ends with an ascending `ThenBy(Id)` as the tie-break, whatever the primary direction. Use `AsNoTracking`.
- [ ] **Step 4: Run** the filter. If SQL Server is available, run with `PNFT_SQLSERVER_CONNECTION` set (see `CLAUDE.md`). Expected: PASS on SQLite (and SQL Server).
- [ ] **Step 5: Commit** `feat(dashboard): add the tracked URL query service`.

### Task 3: Management API

**Files:**
- Modify: `.BackOffice/Constants.cs`, `.BackOffice/BackOfficeComposer.cs`, `.BackOffice/Umbraco.Community.PageNotFoundTracker.BackOffice.csproj` (only if a framework reference is needed)
- Create in `.BackOffice/`: `Controllers/TrackedUrlsControllerBase.cs`, `Controllers/GetTrackedUrlsController.cs`,
  `Controllers/GetTrackedUrlFilterOptionsController.cs`, `Controllers/GetDashboardAccessController.cs`,
  `Models/TrackedUrlResponseModel.cs`, `Models/TrackedUrlFilterOptionsResponseModel.cs`, `Models/DashboardAccessResponseModel.cs`,
  `Security/DashboardAccessRequirement.cs`, `Security/DashboardAccessHandler.cs`, `Configuration/ConfigureSwaggerOptions.cs`,
  `Extensions/UmbracoBuilderExtensions.cs`
- Test: `src/test/Umbraco.Community.PageNotFoundTracker.BackOffice.Tests/` (`TrackedUrlsControllerTests.cs`, `DashboardAccessHandlerTests.cs`);
  `src/test/Umbraco.Community.PageNotFoundTracker.Integration.Tests/ManagementApi/TrackedUrlsApiTests.cs`

**Interfaces:**
- Consumes: `ITrackedUrlQueryService`, `IDashboardAccess`, `ITrackingPersistenceState` (Tasks 1, 2).
- Produces (routes under `/umbraco/pagenotfoundtracker/api/v1`, OpenAPI document name `Constants.ApiName`, group tag `Tracked URL`):
  - `GET tracked-urls` operation `GetTrackedUrls`: query `skip=0`, `take=20`, `orderBy=Hits` (`TrackedUrlSortColumn` as string),
    `direction=Descending` (`Ascending|Descending`), `search`, `domain`, `noDomain=false`, `culture`, `noCulture=false`.
    200 `PagedViewModel<TrackedUrlResponseModel>`; 400 problem details when `take` outside 1 to 100, `skip` negative, or a value and its
    `no*` marker are both set; 401 anonymous; 403 outside the groups; 503 problem details when persistence is `Unavailable`
    (when `Pending`, await `WaitUntilReadyAsync` with the request token first).
  - `GET tracked-urls/filter-options` operation `GetTrackedUrlFilterOptions`: 200 `{ domains, cultures, hasEmptyDomain, hasEmptyCulture }`, same errors.
  - `GET access` operation `GetDashboardAccess`: any signed-in backoffice user, 200 `{ allowed: bool }` from `IDashboardAccess` and the user's
    group aliases (writer's call: a third endpoint so the UI condition and the API policy share one rule).
  - `TrackedUrlResponseModel { int Id, string Url (DisplayUrl), string RelativePath, string Domain, string Culture, bool IsFile, long HitCount, DateTime FirstSeen, DateTime LastSeen }` (UTC, serialised ISO 8601).
  - Policy name constant `Constants.DashboardAccessPolicy`; list and filter-options controllers carry `[Authorize(Policy = Constants.DashboardAccessPolicy)]`.

- [ ] **Step 1: Write failing tests.** Unit (`TrackedUrlsControllerTests`, mocked service and state): `Get_Defaults_QueriesHitsDescendingSkip0Take20`,
  `Get_TakeZeroNegativeOrAbove100_ReturnsBadRequest`, `Get_NegativeSkip_ReturnsBadRequest`, `Get_DomainAndNoDomainTogether_ReturnsBadRequest`,
  `Get_CultureAndNoCultureTogether_ReturnsBadRequest`, `Get_NoDomainTrue_MapsToEmptyFilter`, `Get_PersistenceUnavailable_Returns503`,
  `Get_PersistencePending_WaitsThenQueries`, `Get_MapsRelativePathAndUtcTimestamps`. `DashboardAccessHandlerTests`: succeeds for a user with an allowed
  group role claim, fails for a user without, fails for anonymous. Integration (`TrackedUrlsApiTests`, Umbraco's management API test base on SQLite; check
  `Umbraco.Cms.Tests.Integration` for the base that provides an authenticated client with a chosen user group): `List_AllowedGroup_Returns200WithRows`,
  `List_GroupOutsideSetting_Returns403`, `List_Anonymous_Returns401`, `FilterOptions_GroupOutsideSetting_Returns403`,
  `Access_ReturnsAllowedTrueAndFalseByGroup`, `OpenApiDocument_ContainsTheThreeOperationIds` (fetch the swagger document and assert the operation IDs above).
- [ ] **Step 2: Run** `dotnet test src --filter "FullyQualifiedName~TrackedUrls|FullyQualifiedName~DashboardAccessHandler"`. Expected: FAIL.
- [ ] **Step 3: Implement** following the Umbraco 17.8 Management API pattern (a `ManagementApiControllerBase`-derived base with the versioned backoffice route and
  `[MapToApi(Constants.ApiName)]`; the swagger doc registered through `IConfigureOptions<SwaggerGenOptions>` with Umbraco's backoffice security
  scheme; the policy added through `AddAuthorization`; stable operation IDs). Verify each attribute and type name against the 17.8 source or a `v17/develop`
  reference repo rather than memory. Group role claims are the user group aliases on the backoffice identity: confirm, and say what you found in the commit body.
  Register everything from `BackOfficeComposer` (keep `[ComposeBefore]`).
- [ ] **Step 4: Run** the filter, then `dotnet build src` (zero warnings). Expected: PASS.
- [ ] **Step 5: Commit** `feat(dashboard): add the Management API for tracked URLs`.

### Task 4: Client foundation (generated client, entity type, repository, localization)

**Files:**
- Modify: `src/code/Umbraco.Community.PageNotFoundTracker.BackOffice.Client/package.json` (devDependencies `@hey-api/openapi-ts`, `vitest`; scripts `generate-client`, `test`),
  `src/entry.ts`, `src/manifests.ts`, `.github/workflows/pr-validation.yml`, `.github/workflows/beta-release.yml`, `.github/workflows/release.yml` (run `npm test` after `npm ci`), `.github/scripts/check-package.ps1`
- Create in the client `src/`: `api/` (generated, committed), `openapi-ts.config.ts`, `entity.ts`, `localization/manifests.ts`, `localization/en.ts`,
  `collection/repository/tracked-url.repository.ts`, `collection/repository/tracked-url.data-source.ts`, `collection/query.ts`, `collection/query.test.ts`, `collection/types.ts`

**Interfaces:**
- Consumes: the three endpoints of Task 3.
- Produces: `PNFT_TRACKED_URL_ENTITY_TYPE = 'page-not-found-tracker-url'` (`entity.ts`); `PnftTrackedUrlCollectionModel` (id, url, relativePath, domain,
  culture, isFile, hitCount, firstSeen, lastSeen, `entityType`, `unique` = `String(id)`); `PnftCollectionFilter { skip, take, orderBy, direction, search?, domain?: string | typeof PNFT_EMPTY, culture?: ... }`;
  `buildListQuery(filter): GetTrackedUrlsData['query']` in `query.ts` (pure, maps the `PNFT_EMPTY` sentinel to `noDomain=true` / `noCulture=true`);
  `PnftTrackedUrlRepository` with `requestCollection(filter)` and `requestFilterOptions()`.

- [ ] **Step 1: Write failing tests** in `query.test.ts` (Vitest): `buildListQuery_DefaultsToHitsDescendingSkip0Take20`, `buildListQuery_EmptySentinelBecomesNoDomainFlag`,
  `buildListQuery_EmptySentinelBecomesNoCultureFlag`, `buildListQuery_OmitsBlankSearch`, `buildListQuery_ADomainTextEqualToTheMarkerWordIsSentAsAValue`
  (the sentinel is a `Symbol`/unique object, never a string).
- [ ] **Step 2: Run** `npm test` in the client folder. Expected: FAIL.
- [ ] **Step 3: Implement.** Start the test site (`dotnet run --launch-profile single-site`), generate the client with
  `npm run generate-client` (`OPENAPI_URL` env var, default `https://localhost:44401/umbraco/swagger/page-not-found-tracker-management-api/swagger.json`, `NODE_TLS_REJECT_UNAUTHORIZED=0` for local dev only; confirm the real swagger path) into `src/api/`
  and commit it. In `entry.ts` configure the generated client from Umbraco's auth context (`UMB_AUTH_CONTEXT`: server URL, token, credentials) as the `umbraco-openapi-client` skill
  describes, so no base URL is baked in. Repository and data source wrap the client with `tryExecute`; entity type registered as an `entityType` manifest. English strings in
  `localization/en.ts` registered as a `localization` manifest; all keys prefixed `pageNotFoundTracker_`. Add to `check-package.ps1` an assertion that the packed `entry-point.js` and the
  `App_Plugins` bundle contain no `localhost`.
- [ ] **Step 4: Run** `npm test`, `npm run build`, and grep the build output for `localhost` (expect no match). Expected: PASS, build clean.
- [ ] **Step 5: Commit** `feat(dashboard): add the generated API client, entity type and repository`.

### Task 5: Collection, table view, search, filters and paging

**Files:**
- Create in the client `src/collection/`: `manifests.ts`, `collection.context.ts` (only if the framework's context cannot carry the extra state), `views/table/tracked-url-table.element.ts`,
  `views/table/tracked-url-table.columns.ts`, `filters/domain-culture-filter.element.ts`, `filters/search.element.ts`, `pagination/page-size.element.ts`, `states/*.element.ts` (loading, empty, empty-after-filter, error, unavailable)
- Modify: client `src/manifests.ts`, `src/localization/en.ts`

**Interfaces:**
- Consumes: `PnftTrackedUrlRepository`, `PnftCollectionFilter`, `PNFT_TRACKED_URL_ENTITY_TYPE` (Task 4).
- Produces: collection manifest alias `Pnft.Collection.TrackedUrl`, repository alias `Pnft.Repository.TrackedUrl`, a `collectionView` of the table kind, and one extension
  slot for row actions (entity actions targeting `PNFT_TRACKED_URL_ENTITY_TYPE`; none registered here).

- [ ] **Step 1: Define the `data-mark` hooks** the Task 7 E2E tests rely on, before building the elements: `pnft-collection`, `pnft-search`, `pnft-filter-domain`,
  `pnft-filter-culture`, `pnft-page-size`, `pnft-row`, `pnft-state-empty`, `pnft-state-empty-filtered`, `pnft-state-error`, `pnft-state-unavailable`, `pnft-state-loading`, `pnft-total`, and
  `pnft-sort-<column>` per header. Add them as constants in `collection/data-marks.ts` (one place, used by elements and the E2E tests' own copy).
- [ ] **Step 2: Implement with the framework first.** Use the collection framework's repository, table view, sorting and pagination as far as they go; custom pieces only for the page size choice
  (20, 50, 100, default 20), the domain and culture filters (options from `requestFilterOptions()`, choices "All", values, and localized "No domain"/"No culture" via the sentinel), the URL search box, the type
  (page/file) cell, local time rendering of `firstSeen`/`lastSeen` (`toLocaleString`), and the five states. Default sort Hits descending; a header click sorts that column, the second click reverses it; search, filter, sort and page-size changes reset to page 1; the total
  is shown. **If a requirement cannot be met inside the framework, stop and ask the user before building a custom list (spec).**
- [ ] **Step 3: Verify in the browser.** Run `single-site`, send a few 404s (`curl -k https://localhost:44401/pnft-manual-a`), wait one flush (default one minute, or start with
  `Umbraco__Community__PageNotFoundTracker__FlushInterval=00:00:01`), and use the `umbraco-chrome-navigation` skill to check each column sorts both ways, search narrows, filters narrow, page size changes the page, and each state renders (stop the site for the error state).
- [ ] **Step 4: Run** `npm test` and `npm run build`. Expected: PASS.
- [ ] **Step 5: Commit** `feat(dashboard): add the tracked URL collection, table view, filters and paging`.

### Task 6: Dashboard, access condition, placeholder removal

**Files:**
- Create in the client `src/dashboards/tracked-urls/`: `manifests.ts`, `tracked-urls-dashboard.element.ts`; `src/conditions/dashboard-access.condition.ts`, `src/conditions/manifests.ts`
- Delete: `src/dashboards/placeholder/` (manifest and element)
- Modify: client `src/manifests.ts`, `src/localization/en.ts`

**Interfaces:**
- Consumes: collection alias `Pnft.Collection.TrackedUrl` (Task 5), `GetDashboardAccess` (Task 3).
- Produces: dashboard alias `Umbraco.Community.PageNotFoundTracker.Dashboard.TrackedUrls`, pathname `page-not-found-tracker`, condition `Pnft.Condition.DashboardAccess`; dashboard conditions
  `Umb.Condition.SectionAlias` = `Umb.Section.Content` and `Pnft.Condition.DashboardAccess`.

- [ ] **Step 1: Implement** the condition as a `UmbConditionBase` that calls `GetDashboardAccess` once per session (cache the promise) and reports `permitted` from `allowed`; on request failure it is not permitted (fail closed).
  Register the dashboard in the Content section with label and icon (writer's call: `icon-alert`, label key `pageNotFoundTracker_dashboardLabel`, English "Page not found"). Delete the placeholder files and manifest import.
- [ ] **Step 2: Verify in the browser** (single-site): admin sees the dashboard in Content at `/umbraco/section/content/dashboard/page-not-found-tracker`; set `Umbraco__Community__PageNotFoundTracker__DashboardUserGroups__0=writer`
  and restart: an editor user no longer sees it and the API returns 403; `admin` still sees it. Revert the setting afterwards.
- [ ] **Step 3: Run** `npm run build`. Expected: PASS, no placeholder in the output.
- [ ] **Step 4: Commit** `feat(dashboard): register the Content dashboard with the access condition and remove the placeholder`.

### Task 7: E2E and CI

**Files:**
- Delete: `src/test/Umbraco.Community.PageNotFoundTracker.E2E/tests/smoke.spec.ts`
- Create: `src/test/Umbraco.Community.PageNotFoundTracker.E2E/tests/dashboard.spec.ts`
- Modify: `.github/workflows/pr-validation.yml` (Start test site step)

**Interfaces:**
- Consumes: the `data-mark` constants of Task 5.

- [ ] **Step 1: Write the failing test** `dashboard.spec.ts`. In `beforeAll`, send real 404s with Playwright's `request` fixture to unique, run-scoped paths
  (`const run = Date.now()`): `/pnft-e2e-${run}-a` 3 times, `-b` 2 times, `-c` once, and `/pnft-e2e-${run}-file.jpg` once. Tests: `lists_tracked_urls_ordered_by_hits_descending` (search
  `pnft-e2e-${run}`, `expect.poll` until the rows appear to absorb the flush, assert order a, b, c / file), `search_narrows_by_url`, `sorting_by_url_toggles_direction`,
  `page_filter_by_type_shows_file_row` (type column text), `page_size_can_be_changed_to_50_and_100`, `domain_filter_offers_no_domain_choice`, `shows_empty_after_filter_state`. Unauthenticated and non-allowed-group cases are covered by the integration tests.
- [ ] **Step 2: Run** against a locally running `single-site` started with `Umbraco__Community__PageNotFoundTracker__FlushInterval=00:00:01`: `npm run test:e2e`. Expected before Task 5/6 work: FAIL; now: PASS.
- [ ] **Step 3: Update** `pr-validation.yml` "Start test site" to export `Umbraco__Community__PageNotFoundTracker__FlushInterval=00:00:01` for the job (spec 1's workflow change is in scope). Remove `smoke.spec.ts`.
- [ ] **Step 4: Commit** `test(dashboard): add dashboard E2E tests and a short flush interval in CI`.

### Task 8: Documentation, manual checks, pull request

**Files:**
- Modify: `CLAUDE.md`, `docs/superpowers/specs/2026-11-01-dashboard-design.md` (Status: done)

- [ ] **Step 1: Document in `CLAUDE.md`** (replace "Nothing reads the tracked data yet", mark spec 4 done, update the project overview, solution layout, Build and test, E2E notes that mention the placeholder and the Settings URL):
  the `DashboardUserGroups` setting and its fallback rule; the three endpoints, query parameters and the policy; the OpenAPI document name and how to regenerate the client (`npm run generate-client`, needs a running site, never commit the swagger file, never a base URL);
  the collection setup (aliases, entity type, the empty-filter sentinel); **how to add a row action** (an `entityAction` manifest with `forEntityTypes: ['page-not-found-tracker-url']`, which model fields the action receives incl. `relativePath`); search covers the stored (truncated) `Url`; the E2E env var; the SQL Server scope below.
- [ ] **Step 2: Manual checks** with the `umbraco-chrome-navigation` skill on `multi-site-aliases`, `multi-site-hostnames` and `multi-language`: send 404s per site/language (`/siteA/nope`, `/siteB/nope`, `/nl/nope`; hostnames via `curl -k --resolve`), confirm the domain and culture filters list the right values and narrow correctly, then restore `appsettings.json` and the `.sqlite.db` files (`git checkout -- ...`).
  Run the `sql-server` profile once and call the list endpoint to confirm it works there.
- [ ] **Step 3: Run the full gate**: `npm ci && npm test && npm run build` (client), `dotnet build src` (zero warnings), `dotnet test src`, `pwsh scripts/check-pending-model-changes.ps1`, then pack and `check-package.ps1` per `CLAUDE.md`. Expected: all green.
- [ ] **Step 4: Commit** `docs: document the dashboard`, push, and open a PR to `develop` linking issue #4. Then follow the repository's PR workflow (bind the PR, check CI).

---

## Self-review notes

- **Spec coverage:** list, columns, default order, sorting, search, filters (with empty choices), paging, states, DB-side querying, API, OpenAPI document, generated client, server page-size limit, groups setting and fallback, UI + API enforcement, entity type and docs, localization, unit/integration/E2E tests, manual profile checks, 503 on unavailable persistence, placeholder replacement: all mapped above.
- **Deliberate deviations to confirm:** (1) a third endpoint `GET access` backs the client condition; (2) the endpoint's integration tests run on SQLite (Umbraco's test host only boots on SQLite); SQL Server is covered by the query-service tests on both providers plus a manual call against the `sql-server` profile; (3) Vitest is added for pure client logic.
- **Open verification points the implementer must settle against 17.8 source:** the versioned route and swagger attributes, the role-claim = group-alias assumption, the swagger URL, whether the dashboard can host a collection by manifest `kind` (if not, host `<umb-collection>` in a small dashboard element), and the framework's support for the page-size choice (see the stop-and-ask rule in Task 5).
