# Delete tracked URLs Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Let dashboard users delete one tracked URL, the selected rows of a page, or everything matching the current search and filters.

**Architecture:** `ITrackedUrlRemover` (Core, replaceable) gains two methods. The default `TrackedUrlRemover` (Infrastructure) deletes in batches of 1000, each batch inside `ITrackingFlusher.RunExclusiveAsync`: select the batch's identities, `TrackingQueue.Discard` them, `ExecuteDelete` by id. The list's search/domain/culture filter is extracted into one shared helper so "matching" is defined once. Three Management API operations (BackOffice) sit on top; the client adds a row action, a bulk action (selection) and a collection action.

**Tech Stack:** C# / .NET 10, EF Core (SQLite and SQL Server), NUnit + Moq, Umbraco 17.8 Management API; TypeScript, Lit, Vitest, Playwright; `@hey-api/openapi-ts` generated client.

**Spec:** `docs/superpowers/specs/2026-11-10-delete-tracked-urls-design.md`. Read it and `CLAUDE.md` (Tracking, Dashboard, Redirects, Blocked URLs, Documentation sections) first.

## Global Constraints

- Branch `feature/8-delete-tracked-urls` (already created, off `develop`). Commit per task. Commit messages end with `Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>`.
- Zero analyzer warnings (StyleCop; the single-line copyright header on every C# file; XML docs on public and internal members; usings outside the namespace, file-scoped namespaces, braces always).
- No schema change, no migration; `pwsh scripts/check-pending-model-changes.ps1` must stay green.
- Access: policy `PageNotFoundTracker.DashboardAccess` on every new endpoint (`Constants.DashboardAccessPolicy`); no new setting.
- Batch size 1000 (SQL Server's 2100 parameter limit); `ids` per API call 1 to 100.
- New `ITrackedUrlRemover` methods are plain interface members (not default members): a compile-time break for custom replacers, to be mentioned in the docs.
- `delete-matching` with no filter is refused unless `all: true`; `all: true` together with any filter is 400.
- Operation IDs are the action method names: `DeleteTrackedUrl`, `DeleteTrackedUrls`, `DeleteMatchingTrackedUrls`.
- Generated client: `npm run generate-client` from the running single-site (`NODE_TLS_REJECT_UNAUTHORIZED=0`); commit only real changes (`index.ts`, `sdk.gen.ts`, `types.gen.ts`), revert line-ending-only diffs.
- Docs, quoted samples and the three documentation checks stay green; docs ship in the same change. Public pages never link to `CLAUDE.md` or `docs/superpowers/`.
- Client: every interactive element carries a `data-mark`; the E2E tests keep a copy of the marks (change both together); no `localhost` in shipped client code.
- Git Bash cannot reach LocalDB: run SQL Server cases from PowerShell with `$env:PNFT_SQLSERVER_CONNECTION='Server=(localdb)\pnft-test;Integrated Security=True;TrustServerCertificate=True'`.

## Review Focus

Inputs the spec implies but its tasks' main tests would not hit; each has a pinned test in the owning task.

1. A search term containing `%`, `_`, `[` or `\` in `delete-matching` deletes only literal matches (Task 1).
2. A queued hit for a deleted row is not written back by the next flush (Task 1).
3. More than 1000 matching rows are all deleted across several batches, and the returned count is the total (Task 1).
4. `POST delete-matching` with `{}` (a client that dropped its filter) is 400 and deletes nothing; `all` plus a filter is 400 (Task 2).
5. `POST delete` with duplicate ids, an id below 1, an empty list and 101 ids (Task 2).

## File Structure

Create:
- `src/code/...Core/Tracking/TrackedUrlCriteria.cs`: public record, the input of "delete matching".
- `src/code/...Infrastructure/Tracking/TrackedUrlFilters.cs`: internal static, the shared filter.
- `src/code/...BackOffice/Controllers/DeleteTrackedUrlController.cs`, `DeleteTrackedUrlsController.cs`, `DeleteMatchingTrackedUrlsController.cs`.
- `src/code/...BackOffice/Models/DeleteTrackedUrlsRequestModel.cs`, `DeleteMatchingTrackedUrlsRequestModel.cs`, `DeleteTrackedUrlsResponseModel.cs`.
- Client: `src/delete/` with `constants.ts`, `delete.service.ts` (+ test), `delete.action.ts`, `delete-selected.bulk-action.ts`, `delete-all.collection-action.ts`, `delete-all-modal.element.ts`, `delete-all-modal.token.ts`, `data-marks.ts`, `manifests.ts` (+ test).
- `src/test/...E2E/tests/delete.spec.ts`.

Modify: `ITrackedUrlRemover.cs`, `TrackedUrlRemover.cs`, `TrackedUrlQueryService.cs` (use the helper), `ITrackedUrlRemover` fakes in tests if any fail to compile, `src/manifests.ts` (client root, register `delete/manifests`), `localization/en.ts`, `paged-table.element.base.ts` and `tracked-url-table.element.ts` (selection), `paged-collection.context.base.ts` (clear selection on load), `docs/dashboards.md`, `docs/how-it-works.md`, `docs/extending/dashboard-actions.md` or the page that describes the remover, `CLAUDE.md`, the spec's status line.

---

### Task 1: Remover contract, shared filter and batched delete

**Files:**
- Create: `Core/Tracking/TrackedUrlCriteria.cs`, `Infrastructure/Tracking/TrackedUrlFilters.cs`
- Modify: `Core/Tracking/ITrackedUrlRemover.cs`, `Infrastructure/Tracking/TrackedUrlRemover.cs`, `Infrastructure/Tracking/TrackedUrlQueryService.cs`
- Test: `src/test/...Integration.Tests/Tracking/TrackedUrlRemoverTests.cs` (extend; runs on SQLite and, with `PNFT_SQLSERVER_CONNECTION`, SQL Server), `src/test/...Infrastructure.Tests/Tracking/TrackedUrlFiltersTests.cs`

**Interfaces:**
- Consumes: `TrackingQueue.Discard(string urlHash, string domain, string culture)`, `ITrackingFlusher.RunExclusiveAsync`, `LikePattern.Contains`, `TrackedUrlFilter`.
- Produces:
  - `public sealed record TrackedUrlCriteria(string? Search, TrackedUrlFilter Domain, TrackedUrlFilter Culture)` (Core; the spec called this a filter, but `TrackedUrlFilter` already names the per-column struct, so this is the name).
  - `ITrackedUrlRemover.DeleteAsync(IReadOnlyCollection<int> ids, CancellationToken ct)` returns `Task<int>` (rows deleted; ids already gone count 0; duplicates and non-positive ids are ignored by the service).
  - `ITrackedUrlRemover.DeleteMatchingAsync(TrackedUrlCriteria criteria, CancellationToken ct)` returns `Task<int>`.
  - `internal static IQueryable<TrackedUrl> TrackedUrlFilters.Apply(IQueryable<TrackedUrl> rows, string? search, TrackedUrlFilter domain, TrackedUrlFilter culture)`.
  - `TrackedUrlRemover.BatchSize` = 1000 (`internal const int`).

- [ ] **Step 1: Write the failing tests** in `TrackedUrlRemoverTests` (seed rows with the fixture's existing helper; queue a hit with `queue.TryEnqueue`/the existing test pattern):
  - `Delete_by_ids_deletes_those_rows_and_returns_the_count` (3 rows, delete 2 ids plus one unknown id: returns 2, the third row stays).
  - `Delete_by_ids_discards_queued_hits_so_the_flush_does_not_bring_the_row_back` (queue a hit for a row, delete it, force `flushService.FlushAsync`, row absent).
  - `Delete_matching_deletes_only_rows_matching_search_domain_and_culture` (search `old`, domain `a.com`, culture `nl-NL`: only the intersecting rows go).
  - `Delete_matching_treats_like_characters_literally` (rows `/a%b`, `/a_b`, `/axb`; search `a%b` deletes only `/a%b`; repeat for `_`, `[` and `\`).
  - `Delete_matching_deletes_more_than_one_batch_and_returns_the_total` (seed `TrackedUrlRemover.BatchSize + 5` rows; all deleted; count equals the seeded number).
  - `Delete_matching_discards_queued_hits_of_the_deleted_rows` (as the ids case).
  - `Delete_matching_with_a_cancelled_token_keeps_what_was_deleted` (cancel after the first batch via a wrapper; rows deleted so far stay deleted, the remaining stay, the call throws `OperationCanceledException`).
  - In `TrackedUrlFiltersTests` (unit, SQLite in-memory context if one exists in that project, else cover through the query service tests): `Apply_with_whitespace_search_does_not_filter`.
- [ ] **Step 2: Run to verify they fail**: `cd src && dotnet test test/Umbraco.Community.PageNotFoundTracker.Integration.Tests --filter TrackedUrlRemoverTests`. Expected: compile errors (members missing).
- [ ] **Step 3: Implement** the Core record and the two interface members with XML docs (replacer note: plain members).
- [ ] **Step 4: Extract `Filter`** from `TrackedUrlQueryService` into `TrackedUrlFilters.Apply` (same code, same behaviour) and call it from the query service; `TrackedUrlQueryServiceTests` must stay green untouched.
- [ ] **Step 5: Implement `TrackedUrlRemover`**: both methods loop "select a batch, then `RunExclusiveAsync` { discard + `ExecuteDelete` }". By ids: distinct ids above 0, chunked by `BatchSize`, selecting `Id, UrlHash, Domain, Culture` where id in chunk. Matching: select the first `BatchSize` rows (`Apply(...)` ordered by `Id`) projecting the same four columns; stop when a batch selects fewer than `BatchSize`; sum `ExecuteDelete` results. Select inside the lock so a flush cannot slip between select and delete. Resolve the context factory per call (as the existing code does). Existing single `DeleteAsync(TrackedUrl)` stays unchanged.
- [ ] **Step 6: Run** the filtered tests, then `dotnet build` (zero warnings) and `dotnet test test/Umbraco.Community.PageNotFoundTracker.Infrastructure.Tests`. Expected: pass. Run the SQL Server cases from PowerShell (see Global Constraints).
- [ ] **Step 7: Commit** `feat: delete tracked URLs by ids or by filter in batches`.

### Task 2: Management API operations

**Files:**
- Create: the three controllers and three models listed above.
- Test: `src/test/...BackOffice.Tests/DeleteTrackedUrlsControllersTests.cs` (unit, Moq `ITrackedUrlRemover` and `ITrackingPersistenceState`), `src/test/...Integration.Tests/ManagementApi/TrackedUrlsApiTests.cs` (extend)

**Interfaces:**
- Consumes: `ITrackedUrlRemover` (Task 1), `TrackedUrlsControllerBase.InvalidRequest(title, detail)` and `EnsurePersistenceAvailableAsync`, `TrackedUrlCriteria`, `TrackedUrlFilter`.
- Produces:
  - `DeleteTrackedUrlController.DeleteTrackedUrl(int id, CancellationToken)`: `[HttpDelete("tracked-urls/{id:int}")]`, 200 `DeleteTrackedUrlsResponseModel { Deleted = 1 }`, 404 problem "Tracked URL not found" (when the remover returns 0), 503.
  - `DeleteTrackedUrlsController.DeleteTrackedUrls([FromBody] DeleteTrackedUrlsRequestModel, CancellationToken)`: `[HttpPost("tracked-urls/delete")]`; request `{ Ids: int[] }`; 200 `{ Deleted }`; 400 for null/empty ids, more than 100 distinct ids after de-duplication, or any id below 1; 503.
  - `DeleteMatchingTrackedUrlsController.DeleteMatchingTrackedUrls([FromBody] DeleteMatchingTrackedUrlsRequestModel, CancellationToken)`: `[HttpPost("tracked-urls/delete-matching")]`; request `{ Search?, Domain?, NoDomain, Culture?, NoCulture, All }`; 200 `{ Deleted }`; 400 for domain with noDomain, culture with noCulture, no filter (search blank, no domain/culture constraint) without `All`, `All` with any filter; 503.
  - All three: `[ApiVersion("1.0")]`, `[MapToApiVersion("1.0")]`, `[Authorize(Policy = Constants.DashboardAccessPolicy)]`, derive from `TrackedUrlsControllerBase`, `ProducesResponseType` for each answer.
  - `DeleteTrackedUrlsResponseModel { int Deleted }`.

- [ ] **Step 1: Write failing unit tests** (names): `DeleteTrackedUrl_returns_200_with_one`, `DeleteTrackedUrl_returns_404_when_the_row_is_gone`, `DeleteTrackedUrls_deduplicates_ids_before_calling_the_remover`, `DeleteTrackedUrls_rejects_empty_ids`, `DeleteTrackedUrls_rejects_an_id_below_one`, `DeleteTrackedUrls_rejects_more_than_100_ids`, `DeleteMatching_without_a_filter_and_without_all_is_400_and_does_not_call_the_remover`, `DeleteMatching_with_all_and_a_filter_is_400`, `DeleteMatching_with_all_calls_the_remover_with_an_empty_criteria`, `DeleteMatching_maps_search_domain_and_culture_to_the_criteria`, `DeleteMatching_rejects_domain_with_noDomain`, and 503 for each controller when persistence is unavailable.
- [ ] **Step 2: Run** `dotnet test test/Umbraco.Community.PageNotFoundTracker.BackOffice.Tests --filter DeleteTrackedUrlsControllersTests`. Expected: FAIL (types missing).
- [ ] **Step 3: Implement** models and controllers per the Interfaces block (validation before the persistence check, as `GetTrackedUrls` does; the 404 problem details shaped like `DeleteBlockRuleController`'s).
- [ ] **Step 4: Write failing integration tests** in `TrackedUrlsApiTests` (real backoffice tokens, as its existing tests): each operation 200 with real rows removed, 401 anonymous, 403 for a user outside the groups, `DeleteTrackedUrl` 404 for an unknown id, `DeleteMatching` with `{}` is 400 and the rows remain, the OpenAPI document lists `DeleteTrackedUrl`, `DeleteTrackedUrls` and `DeleteMatchingTrackedUrls`.
- [ ] **Step 5: Run** `dotnet test test/Umbraco.Community.PageNotFoundTracker.Integration.Tests --filter TrackedUrlsApiTests`. Expected: PASS. Then `dotnet build`: zero warnings.
- [ ] **Step 6: Commit** `feat: add the delete tracked URL endpoints`.

### Task 3: Regenerate the client and the delete service

**Files:**
- Modify: `src/code/...BackOffice.Client/src/api/index.ts`, `sdk.gen.ts`, `types.gen.ts` (generated)
- Create: `src/delete/delete.service.ts`, `src/delete/delete.service.test.ts`

**Interfaces:**
- Consumes: the running API (Tasks 1 and 2); the shape of `block-rule.service.ts` (`mapError`, `readable`, results as a discriminated union, no backoffice runtime imports so Vitest runs it).
- Produces:
  - `type PnftDeleteResult = { kind: 'deleted'; deleted: number } | { kind: 'notFound' } | { kind: 'invalid'; message: string | undefined } | { kind: 'unavailable' } | { kind: 'error' }`
  - `deleteTrackedUrl(send: PnftDeleteOneSend, id: number): Promise<PnftDeleteResult>`, `deleteTrackedUrls(send, ids: number[])`, `deleteMatchingTrackedUrls(send, criteria: PnftDeleteMatchingCriteria)` where `PnftDeleteMatchingCriteria` is `{ search?: string; domain?: string; noDomain?: boolean; culture?: string; noCulture?: boolean; all?: boolean }` and the `send` types return `{ data?: DeleteTrackedUrlsResponseModel; error?: unknown }`.
  - `criteriaFromFilter(filter: PnftPagedCollectionFilterModel): PnftDeleteMatchingCriteria`: maps the collection filter (`PNFT_EMPTY` to `noDomain`/`noCulture`, `''`/undefined to no constraint, blank search to none) and sets `all: true` when nothing is constrained. Reuse the mapping in `collection/query.ts` instead of duplicating it.

- [ ] **Step 1: Start single-site with the new endpoints** (`cd test-sites/Website-V17 && dotnet run --launch-profile single-site`, after `dotnet build`), then `cd` to the client project and run `NODE_TLS_REJECT_UNAUTHORIZED=0 npm run generate-client`. Revert line-ending-only diffs and the `client-ready.ts` handling per `CLAUDE.md`. Expected: `sdk.gen.ts` exports `deleteTrackedUrl`, `deleteTrackedUrls`, `deleteMatchingTrackedUrls`. Restore `appsettings.json` and the sqlite db with the `git checkout` from `CLAUDE.md`.
- [ ] **Step 2: Write failing Vitest tests**: `maps 200 to deleted with the count`, `maps 404 to notFound`, `maps 400 to invalid with the detail`, `maps 503 to unavailable`, `maps a rejected send to error`, `criteriaFromFilter sets all when nothing is constrained`, `criteriaFromFilter maps the empty markers to noDomain and noCulture`, `criteriaFromFilter ignores a whitespace-only search`, `criteriaFromFilter does not set all when a search is present`.
- [ ] **Step 3: Run** `npm test -- delete.service`. Expected: FAIL.
- [ ] **Step 4: Implement** `delete.service.ts` per the Interfaces block.
- [ ] **Step 5: Run** `npm test` and `npm run build`. Expected: pass.
- [ ] **Step 6: Commit** `feat: add the delete tracked URL client and service`.

### Task 4: Delete row action

**Files:**
- Create: `src/delete/constants.ts`, `src/delete/delete.action.ts`, `src/delete/data-marks.ts`, `src/delete/manifests.ts`, `src/delete/manifests.test.ts`
- Modify: `src/manifests.ts` (client root: register the delete manifests next to blocking and redirect), `src/localization/en.ts`
- Test: `src/delete/manifests.test.ts`

**Interfaces:**
- Consumes: `deleteTrackedUrl` (Task 3), `PNFT_TRACKED_URL_COLLECTION_CONTEXT`, `PNFT_TRACKED_URL_ENTITY_TYPE`; follow `blocking/unblock.action.ts` exactly for structure (capture notification context and localization first, `umbConfirmModal` with `color: 'danger'`, `tryExecute(..., { disableNotifications: true })`, `collection.loadCollection()`, toasts per result kind).
- Produces: alias `PNFT_DELETE_ACTION_ALIAS = 'Pnft.EntityAction.TrackedUrl.Delete'`; manifest kind `default`, weight **-20**, `forEntityTypes: [PNFT_TRACKED_URL_ENTITY_TYPE]`, icon `icon-trash`, label `#pageNotFoundTracker_delete`, `additionalOptions: true`, no condition. Localization keys (en): `pageNotFoundTracker_delete`, `_deleteHeadline`, `_deleteConfirmText` (takes the URL), `_deleted` (takes the URL), `_deletedHeadline`, `_deleteNotFound`, `_deleteFailedHeadline`, `_deleteError`, `_deleteUnavailable`.

- [ ] **Step 1: Write failing Vitest tests** in `manifests.test.ts`: `the delete action is an entity action for tracked URL rows`, `the delete action has weight -20, below block (-10)`, `the delete action has no condition so it shows on every row`.
- [ ] **Step 2: Run** `npm test -- delete/manifests`. Expected: FAIL.
- [ ] **Step 3: Implement** the constants, manifest, action class (default export `api`) and texts; register in `src/manifests.ts`.
- [ ] **Step 4: Run** `npm test`, `npm run build`, `npm run check-samples`. Expected: pass.
- [ ] **Step 5: Manual check** in the browser pane against single-site (`FlushInterval=00:00:01`): send a 404 (`curl -k https://localhost:44401/pnft-del-1`), reload the dashboard, row dropdown, Delete, confirm: row gone and the toast shows; the URL is tracked again from 1 after another 404. Restore db and `appsettings.json`.
- [ ] **Step 6: Commit** `feat: add the delete row action`.

### Task 5: Select rows and "Delete selected"

**Files:**
- Create: `src/delete/delete-selected.bulk-action.ts`
- Modify: `src/delete/manifests.ts`, `src/localization/en.ts`, `src/collection/views/table/paged-table.element.base.ts` (selection), `src/collection/views/table/tracked-url-table.element.ts`, `src/collection/paged-collection.context.base.ts` (clear selection on load), `src/delete/data-marks.ts`

**Interfaces:**
- Consumes: Umbraco 17.8's own mechanism: `UmbDefaultCollectionContext` makes `selection` selectable when `entityBulkAction` manifests exist for the collection's entity types and `umb-collection-selection-actions` renders them in the footer; a bulk action extends `UmbEntityBulkActionBase` and reads `this.selection` (unique strings). Verify against the installed typings in `node_modules/@umbraco-cms/backoffice` (`entity-bulk-action`, `collection/default/collection-default.context`, `components/table`) before coding, as `CLAUDE.md` asks for on every Umbraco upgrade.
- Produces: manifest `{ type: 'entityBulkAction', kind: 'default', alias: 'Pnft.EntityBulkAction.TrackedUrl.Delete', forEntityTypes: [PNFT_TRACKED_URL_ENTITY_TYPE], weight: 0, meta: { icon: 'icon-trash', label: '#pageNotFoundTracker_deleteSelected' } }` and `PnftDeleteSelectedBulkAction` (confirm with the count, `deleteTrackedUrls(send, ids.map(Number))`, reload, toast with the returned count). `PnftPagedTableElementBase` takes an `allowSelection` flag (default false, so the block rule table is unchanged); `tracked-url-table.element.ts` sets it true. The collection context base calls `selection.clear()` whenever it loads (any reload, including after Create redirect and Block).

- [ ] **Step 1: Write failing Vitest tests**: manifests: `the bulk action is registered for tracked URL rows only`; `the block rule table does not allow selection` is a runtime concern, so cover it by E2E (Task 7) not Vitest.
- [ ] **Step 2: Run** `npm test -- delete/manifests`. Expected: FAIL.
- [ ] **Step 3: Implement** the selection flag in the table base (`#tableConfig` becomes `{ allowSelection, allowSelectAll: false, hideIcon: true }` per subclass; wire `umb-table`'s `selected`/`deselected` events and its `selection` property to `context.selection` if the 17.8 table view does not do it itself: check how `UmbCollectionViewElementBase` and Umbraco's own table view do it), the bulk action, its manifest, texts (`pageNotFoundTracker_deleteSelected`, `_deleteSelectedHeadline`, `_deleteSelectedConfirmText` taking the count, `_deletedMany` taking the count) and the selection clear.
- [ ] **Step 4: Run** `npm test`, `npm run build`. Expected: pass.
- [ ] **Step 5: Manual check** in the browser pane: select two rows, the footer shows Delete selected, confirm: both rows go; change the sort or page: selection is empty. Restore db.
- [ ] **Step 6: Commit** `feat: add row selection and delete selected`.

### Task 6: "Delete all matching" collection action

**Files:**
- Create: `src/delete/delete-all.collection-action.ts`, `src/delete/delete-all-modal.element.ts`, `src/delete/delete-all-modal.token.ts`
- Modify: `src/delete/manifests.ts`, `src/localization/en.ts`, `src/delete/data-marks.ts`

**Interfaces:**
- Consumes: `criteriaFromFilter`, `deleteMatchingTrackedUrls` (Task 3), `PNFT_TRACKED_URL_COLLECTION_CONTEXT` (`totalItems`, `filter`, `loadCollection`), `isFilterActive` from `collection/state.ts`; structure after `blocking/add-rule.collection-action.ts` and `blocking/rule-modal.token.ts`/`rule-modal.element.ts`.
- Produces: collection action `{ type: 'collectionAction', kind: 'button', alias: 'Pnft.CollectionAction.TrackedUrl.DeleteAll', meta: { label: '#pageNotFoundTracker_deleteAll', additionalOptions: false }, conditions: forThisCollection }`; modal `Pnft.Modal.DeleteAll` with data `{ total: number; filtered: boolean }`, value `{ confirmed: boolean }`. The modal states the count ("Delete {total} tracked URLs?"), says "matching the current filter" when `filtered`, and when not `filtered` says all tracked URLs will be deleted and keeps Confirm disabled until the input (`data-mark` `pnft-delete-all-confirm-input`) equals `delete` (trimmed, case-insensitive). The action is disabled/does nothing when `totalItems` is 0. After Confirm: `deleteMatchingTrackedUrls(send, criteriaFromFilter(filter))`, reload, success toast with the returned count, inline-free (toast) errors per result kind.

- [ ] **Step 1: Write failing Vitest tests** (pure parts only): `isDeleteAllConfirmed accepts "delete" in any case with surrounding spaces`, `isDeleteAllConfirmed rejects anything else`, and manifest tests `the delete all action is a collection action for the tracked URL collection only`. Put `isDeleteAllConfirmed(input: string): boolean` in `delete.service.ts` or a small `delete-all.ts` without backoffice imports.
- [ ] **Step 2: Run** `npm test -- delete`. Expected: FAIL.
- [ ] **Step 3: Implement** modal, token, action, manifests, texts, `data-mark`s (`pnft-delete-all-modal`, `pnft-delete-all-confirm`, `pnft-delete-all-cancel`, `pnft-delete-all-confirm-input`, `pnft-delete-all-count`).
- [ ] **Step 4: Run** `npm test`, `npm run build`. Expected: pass.
- [ ] **Step 5: Manual check** in the browser pane: search a term, Delete all matching: dialog names the count and the filter, only those rows go; clear the search, Delete all: Confirm disabled until `delete` is typed. Restore db.
- [ ] **Step 6: Commit** `feat: add delete all matching`.

### Task 7: E2E tests, documentation and wrap-up

**Files:**
- Create: `src/test/...E2E/tests/delete.spec.ts`
- Modify: `docs/dashboards.md`, `docs/how-it-works.md`, the `docs/extending` page that describes `ITrackedUrlRemover` (search `docs/` for it; if none documents it, add a short section to `docs/extending/dashboard-actions.md`), `CLAUDE.md`, the spec's `Status:` line, the E2E `blocked.spec.ts` and `redirect.spec.ts` selectors if the row dropdown changed
- Test: `delete.spec.ts`

**Interfaces:**
- Consumes: the marks from Task 4 to 6 (copy them into the spec as a `MARKS` constant like `blocked.spec.ts`), Umbraco's `entity-action:Pnft.EntityAction.TrackedUrl.Delete`, `umb-entity-actions-dropdown uui-button` in the row, run-scoped prefix `/pnft-e2e-<Date.now()>-...`, `FlushInterval=00:00:01`.
- Produces: tests `Delete_removes_a_single_row` (the URL is tracked again from 1 after another 404), `Delete_selected_removes_the_selected_rows_only`, `Delete_all_matching_a_search_removes_only_those_rows`, `Delete_all_without_a_filter_needs_the_typed_word`.

- [ ] **Step 1: Write the E2E tests** per the Produces block; clean up every row with the run prefix at the end like the other specs.
- [ ] **Step 2: Run** against single-site: `cd src/test/Umbraco.Community.PageNotFoundTracker.E2E && npm run test:e2e`. Expected: all specs pass, including the existing redirect, blocked and dashboard specs. Fix selectors that the third row action broke (row dropdown contents).
- [ ] **Step 3: Docs.** `dashboards.md`: Delete (row dropdown), selecting rows and Delete selected, Delete all matching (filter, count, typed word without a filter), that a deleted URL is tracked again from one and that deleting cannot be undone, same access as the dashboard. `how-it-works.md`: how deletes interact with the queue (hits queued for a deleted URL are dropped; hits that arrive afterwards start a new row; with several servers another server's queued hits can bring the row back), batched delete. The remover page: the two new interface methods and that custom replacers must implement them (compile-time break, mention in the release notes). If a quoted sample covers the remover, update the sample under `src/samples` too.
- [ ] **Step 4: Run the checks**: `pwsh .github/scripts/check-doc-samples.ps1`, `pwsh .github/scripts/check-doc-links.ps1`, `dotnet test src/test/Umbraco.Community.PageNotFoundTracker.Infrastructure.Tests --filter ConfigurationDocsTests`, `npm run check-samples`. Expected: pass.
- [ ] **Step 5: Full verification**: `cd src && dotnet build` (zero warnings), `dotnet test` (SQL Server cases from PowerShell), client `npm test` and `npm run build`, `pwsh scripts/check-pending-model-changes.ps1`, pack plus `check-package.ps1` per `CLAUDE.md` (the generated client must not contain `localhost`).
- [ ] **Step 6: Update `CLAUDE.md`** (a short "Delete tracked URLs (spec 10)" section: remover methods, batch size, `all` rule, endpoints table, client pieces and aliases, data marks, tests, the row dropdown now holding Block and Delete; list the spec and plan under Specs and Plans; update the overview line) and set the spec's `Status:` to `implemented`.
- [ ] **Step 7: Commit** `docs: describe deleting tracked URLs` (E2E and CLAUDE.md in the same or a separate commit).
