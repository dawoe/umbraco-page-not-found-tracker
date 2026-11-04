# Redirect Creation Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** A "Create redirect" action on dashboard rows: the editor picks a target page, a redirect is created through a replaceable backend provider (default: Umbraco's `IRedirectUrlService`), the tracked record is deleted and the list refreshes. The form is a replaceable, package-defined backoffice extension type.

**Architecture:** `IRedirectProvider` (Core) answers "can you handle this record" (a per-row flag on the list API) and creates the redirect from the record plus an opaque JSON payload. `ITrackedUrlRedirectService` (Core contract, Infrastructure implementation) owns the plumbing: load the record, ask the provider, on success delete the record under the flush lock and discard queued hits. One endpoint in `.BackOffice` exposes it. The client adds a `pnftRedirectForm` extension type, a modal that hosts the highest-weight form, a default content-picker form and an entity action on `PNFT_TRACKED_URL_ENTITY_TYPE`.

**Tech Stack:** .NET 10, Umbraco 17.8 (`IRedirectUrlService`, `IDomainService`), EF Core, NUnit + Moq, Umbraco integration test base, TypeScript + Lit, `@hey-api/openapi-ts`, Vitest, Playwright.

**Spec:** `docs/superpowers/specs/2026-11-01-redirect-creation-design.md` (issue #5). Read it first; "Calls made in this spec" is binding. Load before client work: `umbraco-cms-backoffice-skills:umbraco-version-guard`, `-umbraco-entity-actions`, `-umbraco-modals`, `-umbraco-extension-registry`, `-umbraco-openapi-client`, `-umbraco-localization`, `-umbraco-chrome-navigation`, `umbraco-cms-backoffice-testing-skills:umbraco-e2e-testing`.

## Global Constraints

- Branch `feature/5-redirect-creation` (created off `develop`), one PR to `develop`. Commits end with the session's `Co-Authored-By` line.
- Layering (`CLAUDE.md`): contracts and models in `.Core`; provider, service, queue/delete code in `.Infrastructure`; endpoint and models in `.BackOffice`; extension type, form, modal, action in `.BackOffice.Client`. Entry project only wires.
- Replaceable services use `AddUnique`, never `TryAdd`; package composers keep `[ComposeBefore(typeof(PageNotFoundTrackerComposer))]`. A new `PackageReference` in a sibling project is mirrored in the entry csproj.
- Access: same rule as the dashboard (`Constants.DashboardAccessPolicy`), no extra permission. Same OpenAPI document (`page-not-found-tracker-management-api`). Operation ID = action method name.
- The "supported" flag depends only on the record (page vs file, query string present), never on a per-row lookup.
- Default provider: pages only; payload is the target content key; replaces an existing redirect for the same route and culture; fails with a readable message (never silent success) on: unknown domain/root, unsupported query string, missing or unpublished target, Umbraco redirect handling switched off.
- Same-server race: flush and delete + queue discard serialised on one lock. Other servers: accepted (spec).
- StyleCop zero warnings, copyright header, XML docs on public and internal members. Client: `data-mark` on interactive elements (copy new marks into the E2E project), texts in `localization/en.ts` as `pageNotFoundTracker_*`, no `localhost` in the bundle.
- Never commit `.sqlite.db` / `appsettings.json` changes from profile runs.

## Review Focus

1. Provider succeeds but the delete throws: editor told both facts, record stays, logged (Task 3).
2. A hit for the deleted URL queued or mid-flush while the delete runs: record not written back on this server (Task 2).
3. Record with an empty domain on `single-site` (no domains) and a domain with an alias path (`/sitea`): route format is right and the redirect actually applies (Tasks 1, 4).
4. Two Umbraco domains with the same name, different cultures; culture-less record on a variant site (Task 4).
5. Same redirect created twice, target unpublished or deleted between pick and confirm, redirect handling disabled in config, payload null/garbage (Tasks 3, 4).

---

### Task 1: Verify Umbraco facts and the order against spec 2 (spike)

The spec leaves these to the builder; every later task depends on the answers. Findings go into a "Redirects (spec 5)" section of `CLAUDE.md` (Task 8) and into the code comments of Task 4.

**Files:**
- Test: `src/test/Umbraco.Community.PageNotFoundTracker.Integration.Tests/Redirects/RedirectOrderingTests.cs`

**Interfaces:**
- Produces: written answers to (a) route format with a domain (`"{rootId}/{relativePath}"`) and without (`"/{path}"`) for Umbraco 17.8 `IRedirectUrlService.Register(route, contentKey, culture)`; (b) whether lookup honours a query string; (c) the config key that switches redirect handling off (expected `Umbraco:CMS:WebRouting:DisableRedirectUrlTracking` affects *tracking*; check what disables *applying*); (d) whether a redirect applies before the spec 2 last-chance finder.

- [ ] **Step 1: Write the failing test** `Registered_redirect_applies_before_the_404_page` deriving from `PageNotFoundTestBase`: publish a target page and a `notFoundPage`, register a redirect through `IRedirectUrlService` for a missing path, `GET` it with redirects off; assert 301 with `Location` = the target's URL, and that a different missing path still gets 404 with the 404 page body. Add `Redirect_with_query_string_is_ignored_or_honoured` that requests `/missing?x=1` and asserts and documents the observed behaviour.
- [ ] **Step 2: Run** `dotnet test src --filter FullyQualifiedName~RedirectOrderingTests` and adjust route format until it passes. Expected: PASS. If the redirect does **not** apply before spec 2, stop and report: the spec treats that as a blocker.
- [ ] **Step 3: Read Umbraco 17.8 sources/docs** (`Umbraco-CMS` `v17/dev`) for the no-domain route form and the setting name; record answers as comments at the top of the test file.
- [ ] **Step 4: Commit** `test: prove redirects apply before the 404 handling`.

### Task 2: Delete a tracked record safely (queue discard, flush lock, store)

**Files:**
- Modify: `Infrastructure/Tracking/TrackingQueue.cs` (add `Discard`), `TrackingFlushService.cs` and `ITrackingFlusher.cs` (add `RunExclusiveAsync`), `Core/Tracking/ITrackedUrlQueryService.cs` (add `GetByIdAsync`), `Infrastructure/Tracking/TrackedUrlQueryService.cs`, `Infrastructure/Extensions/UmbracoBuilderExtensions.cs`
- Create: `Core/Tracking/ITrackedUrlRemover.cs`, `Infrastructure/Tracking/TrackedUrlRemover.cs`
- Test: `Infrastructure.Tests/Tracking/TrackingQueueDiscardTests.cs`; `Integration.Tests/Tracking/TrackedUrlRemoverTests.cs` (SQLite, SQL Server via `TrackingDatabaseFixture`)

**Interfaces:**
- Produces: `TrackingQueue.Discard(string urlHash, string domain, string culture): int` (removes from channel, pending and in-flight copy; same case-insensitive identity as the merge key; returns removed count); `ITrackingFlusher.RunExclusiveAsync(Func<Task> action, CancellationToken ct): Task` (runs while holding the flush lock, so no flush batch is in flight); `ITrackedUrlQueryService.GetByIdAsync(int id, CancellationToken ct): Task<TrackedUrl?>`; `ITrackedUrlRemover.DeleteAsync(TrackedUrl url, CancellationToken ct): Task<bool>` (true when a row was deleted; discards queued hits and deletes the row inside `RunExclusiveAsync`; `AddUnique`).

- [ ] **Step 1: Write failing tests.** Unit: `Discard_removes_channel_pending_and_in_flight_hits_for_the_identity_only` (three identities, differing case in domain; after `Discard` only the others come out of `TakeBatch`). Integration: `Delete_removes_row_and_a_queued_hit_does_not_bring_it_back` (insert row via upsert, enqueue a hit, `DeleteAsync`, flush, assert count 0); `Delete_waits_for_a_flush_in_progress` (hold the flush lock via `RunExclusiveAsync`, start delete, assert it has not completed, release, assert it completes); `GetByIdAsync_returns_null_for_unknown_id`.
- [ ] **Step 2: Run to verify they fail** (members missing).
- [ ] **Step 3: Implement** the members above. `Discard` takes the queue `gate` and, for the in-flight array, filters it (a lock-protected field already). Delete the row with `ExecuteDeleteAsync` on `Id`. Resolve `IDbContextFactory<PageNotFoundTrackerDbContext>` per call from `IServiceProvider` (same reason as `TrackedUrlQueryService`).
- [ ] **Step 4: Run** `dotnet test src --filter "FullyQualifiedName~Discard|FullyQualifiedName~TrackedUrlRemover"`. Expected: PASS (SQL Server cases skipped without `PNFT_SQLSERVER_CONNECTION`).
- [ ] **Step 5: Commit** `feat(tracking): delete a tracked URL without a queued hit bringing it back`.

### Task 3: Redirect contract and creation service

**Files:**
- Create: `Core/Redirects/IRedirectProvider.cs`, `Core/Redirects/RedirectResult.cs`, `Core/Redirects/ITrackedUrlRedirectService.cs`, `Core/Redirects/CreateRedirectOutcome.cs`; `Infrastructure/Redirects/TrackedUrlRedirectService.cs`; register in `Infrastructure/Extensions/UmbracoBuilderExtensions.cs`
- Test: `Infrastructure.Tests/Redirects/TrackedUrlRedirectServiceTests.cs`

**Interfaces:**
- Produces:
  - `public interface IRedirectProvider { bool CanHandle(TrackedUrl url); Task<RedirectResult> CreateAsync(TrackedUrl url, JsonElement? payload, CancellationToken cancellationToken); }`
  - `public sealed record RedirectResult(bool Succeeded, string? Message)` with `Success()` and `Failure(string message)` factories.
  - `public enum CreateRedirectStatus { Created, CreatedRecordNotRemoved, NotFound, NotSupported, Failed }`; `public sealed record CreateRedirectOutcome(CreateRedirectStatus Status, string? Message)`.
  - `public interface ITrackedUrlRedirectService { Task<CreateRedirectOutcome> CreateAsync(int trackedUrlId, JsonElement? payload, CancellationToken ct); }`
- Consumes: `ITrackedUrlQueryService.GetByIdAsync`, `ITrackedUrlRemover.DeleteAsync` (Task 2). Both services and `IRedirectProvider` are `AddUnique` so implementers replace them.

- [ ] **Step 1: Write failing tests** with a fake provider and mocks: `Unknown_id_is_NotFound_and_provider_not_called`; `Provider_cannot_handle_is_NotSupported_and_nothing_deleted`; `Provider_failure_returns_message_and_deletes_nothing`; `Provider_success_deletes_record_and_returns_Created`; `Provider_throws_is_Failed_with_generic_message_and_logged_and_nothing_deleted`; `Delete_throws_after_success_is_CreatedRecordNotRemoved_and_logged`; `Delete_returns_false_after_success_is_still_Created` (already gone).
- [ ] **Step 2: Run to verify they fail.**
- [ ] **Step 3: Implement** the service per the table above; the message for `CreatedRecordNotRemoved` is fixed text ("The redirect was created, but the tracked URL could not be removed from the list.").
- [ ] **Step 4: Run** `dotnet test src --filter FullyQualifiedName~TrackedUrlRedirectServiceTests`. Expected: PASS.
- [ ] **Step 5: Commit** `feat(redirects): add the redirect provider contract and creation service`.

### Task 4: Default provider on `IRedirectUrlService`

**Files:**
- Create: `Infrastructure/Redirects/UmbracoRedirectProvider.cs`, `Infrastructure/Redirects/RedirectRouteBuilder.cs` (pure mapping, unit-testable); register `AddUnique<IRedirectProvider, UmbracoRedirectProvider>()`
- Test: `Infrastructure.Tests/Redirects/RedirectRouteBuilderTests.cs`, `UmbracoRedirectProviderTests.cs`; `Integration.Tests/Redirects/UmbracoRedirectProviderTests.cs`

**Interfaces:**
- Consumes: Task 1 findings (route format, query handling, setting name), Task 3 contract.
- Produces: `RedirectRouteBuilder.Build(int? domainRootId, string relativePath): string`; `UmbracoRedirectProvider.CanHandle` = `!url.IsFile && !url.Url.Contains('?')` unless Task 1 shows Umbraco honours queries (then only `IsFile`). Payload shape: `{ "targetKey": "<guid>" }`.

- [ ] **Step 1: Write failing unit tests** for the builder (domain with and without path, no domain gives the bare route, trailing/leading slash cases, root path `/`), and for the provider with mocks: `File_rows_are_not_handled`, `Query_string_rows_are_not_handled`, `Unknown_domain_fails_with_message`, `Two_domains_same_name_culture_picks_one`, `Record_without_culture_uses_invariant`, `Missing_target_fails`, `Unpublished_target_fails`, `Redirect_handling_disabled_fails`, `Garbage_or_missing_payload_fails`, `Existing_redirect_for_route_and_culture_is_replaced`.
- [ ] **Step 2: Run to verify they fail.**
- [ ] **Step 3: Implement.** Find the domain by lower-cased name via `IDomainService`/`IDomainCache` (reuse the spec 2 name parsing in `RequestDomainMatcher`; move shared parsing to `.Core` only if needed), take its root content id; check the target via `IPublishedContentCache`; replace by removing the existing `IRedirectUrl` for the route+culture (`GetMostRecentRedirectUrl`) before `Register`. Read the disabled setting from `IOptionsMonitor<WebRoutingSettings>` (name from Task 1).
- [ ] **Step 4: Write integration tests** `Creates_redirect_for_domain_root_and_culture_then_request_redirects`, `Creating_twice_leaves_one_redirect_with_the_new_target`, `No_domain_record_uses_first_root`; run `dotnet test src --filter FullyQualifiedName~Redirect`. Expected: PASS.
- [ ] **Step 5: Commit** `feat(redirects): default provider on Umbraco's redirect management`.

### Task 5: Endpoint, row flag and generated client

**Files:**
- Create: `BackOffice/Controllers/CreateTrackedUrlRedirectController.cs`, `BackOffice/Models/CreateRedirectRequestModel.cs`, `BackOffice/Models/CreateRedirectResponseModel.cs`
- Modify: `BackOffice/Models/TrackedUrlResponseModel.cs` (add `bool CanCreateRedirect`), `GetTrackedUrlsController.cs` (inject `IRedirectProvider`, set the flag in `Map`), `Client/src/api/*` (regenerated)
- Test: `BackOffice.Tests/Controllers/CreateTrackedUrlRedirectControllerTests.cs`, extend `GetTrackedUrlsControllerTests`; `Integration.Tests/Api/TrackedUrlsApiTests.cs`

**Interfaces:**
- Produces: `POST tracked-urls/{id:int}/redirect`, action `CreateTrackedUrlRedirect(int id, CreateRedirectRequestModel request, CancellationToken ct)`, body `{ payload: JsonElement? }`. Answers: 200 `{ recordRemoved: bool, message: string? }` (Created = true; CreatedRecordNotRemoved = false with message), 404 problem details (record not found), 409 problem details (not supported, message), 422 problem details (provider failure, message), 503 as spec 4, 401/403 via the policy. List items gain `canCreateRedirect`.

- [ ] **Step 1: Write failing tests**: one controller unit test per status above with a mocked `ITrackedUrlRedirectService`; the list controller test that `CanCreateRedirect` mirrors `IRedirectProvider.CanHandle`; integration (real tokens) 401 anonymous, 403 outside groups, 404 unknown id, 200 with a fake provider, `canCreateRedirect` false for a file row, swagger document contains `CreateTrackedUrlRedirect`.
- [ ] **Step 2: Run to verify they fail.**
- [ ] **Step 3: Implement** the controller (derive `TrackedUrlsControllerBase`, `[Authorize(Policy = Constants.DashboardAccessPolicy)]`, `EnsurePersistenceAvailableAsync`) and the flag.
- [ ] **Step 4: Run** `dotnet test src`. Expected: PASS, zero warnings.
- [ ] **Step 5: Regenerate the client** with a site running (`NODE_TLS_REJECT_UNAUTHORIZED=0 npm run generate-client` in the client project); check `git diff src/api` has no origin/`localhost`.
- [ ] **Step 6: Commit** `feat(redirects): add the create redirect endpoint and the per-row flag`.

### Task 6: Client extension type, modal, default form and entity action

**Files:**
- Create under `Client/src/redirect/`: `redirect-form.extension.ts` (manifest type `pnftRedirectForm`, `declare global` augmentation of `UmbExtensionManifestMap`, element contract), `redirect-modal.token.ts`, `redirect-modal.element.ts`, `create-redirect.action.ts` (extends `UmbEntityActionBase`), `default-form/default-redirect-form.element.ts` (content picker, validation, summary), `redirect.service.ts` (calls `createTrackedUrlRedirect`, maps statuses to a result union), `manifests.ts`
- Modify: `src/manifests.ts`, `src/localization/en.ts`, `src/collection/types.ts` (`canCreateRedirect`), `src/collection/repository/tracked-url.data-source.ts` (map it), `src/collection/data-marks.ts`, `collection.context.ts` only if a refresh hook is missing
- Test: `redirect/redirect.service.test.ts`, `redirect/select-form.test.ts` (Vitest, pure logic)

**Interfaces:**
- Produces: `PnftRedirectFormElement` contract: property `trackedUrl: PnftTrackedUrlCollectionModel`, method `getPayload(): Promise<unknown | undefined>` (undefined means invalid; the form shows its own validation). Default form returns `{ targetKey }`. Modal `Pnft.Modal.CreateRedirect`: hosts the highest-weight `pnftRedirectForm` (use `umb-extension-slot` with `single`; verify it orders by weight, else sort in `select-form.ts`), Confirm calls the service, shows provider messages inline, closes with `{ removed }`. Action: manifest `Pnft.EntityAction.TrackedUrl.CreateRedirect`, `forEntityTypes: [PNFT_TRACKED_URL_ENTITY_TYPE]`, `kind: 'default'`, visible only when the row's `canCreateRedirect` is true (reads the item from `PNFT_TRACKED_URL_COLLECTION_CONTEXT` by `unique`; use a condition or hide in `getHref`/execute, whichever 17.8 supports, verified in the browser), after success `loadCollection()` and a success notification (warning notification when `recordRemoved` is false).

- [ ] **Step 1: Write failing Vitest tests**: `redirect.service` maps 200 (removed true/false), 404, 409, 422 and a network error to distinct results; `select-form` picks the highest weight and falls back to the default when none other is registered.
- [ ] **Step 2: Run** `npm test`; expected FAIL.
- [ ] **Step 3: Implement** the files above; `data-mark`s `pnft-create-redirect-modal`, `pnft-redirect-confirm`, `pnft-redirect-error`, `pnft-redirect-target-picker`.
- [ ] **Step 4: Run** `npm test && npm run build`; expected PASS and a clean build.
- [ ] **Step 5: Verify in the browser** (`umbraco-chrome-navigation`) on `single-site` with a short flush interval: a page row shows the action, a file row does not; picking a page creates the redirect and the row leaves; a provider error (unpublish the target) shows inline and keeps the row. Note the "verify the first real action" caveat from `CLAUDE.md`.
- [ ] **Step 6: Commit** `feat(redirects): add the redirect form extension type, modal and row action`.

### Task 7: Replacement proof (custom provider and custom form)

**Files:**
- Test: `Integration.Tests/Redirects/CustomProviderReplacementTests.cs`; `Client/src/redirect/select-form.test.ts` (extend)

- [ ] **Step 1: Write the tests**: a composer in the test assembly marked `[ComposeAfter(typeof(PageNotFoundTrackerComposer))]` registers a fake `IRedirectProvider` (`AddUnique`) that handles file rows; assert the list flag is true for a file row, `POST` redirect succeeds, the record is gone and the default provider was not called. Vitest: a second registered form with higher weight is chosen over the default.
- [ ] **Step 2: Run** `dotnet test src --filter FullyQualifiedName~CustomProviderReplacement` and `npm test`. Expected: PASS.
- [ ] **Step 3: Commit** `test(redirects): prove a custom provider and form replace the defaults`.

### Task 8: E2E, manual checks and documentation

**Files:**
- Create: `src/test/Umbraco.Community.PageNotFoundTracker.E2E/tests/redirect.spec.ts`
- Modify: `CLAUDE.md` (status: spec 5 done; list the new spec/plan; "Redirects (spec 5)" section: contract, endpoint table, payload, Task 1 findings, minimal custom provider and custom form examples, client regeneration note; replace "Adding a row action" text with what is now registered), E2E `data-marks` copy

- [ ] **Step 1: Write the E2E test** on `single-site` (run-scoped prefix like `dashboard.spec.ts`): send a 404 for a page URL, wait for the row, create a redirect to a published page through the default form, assert the row disappears and the URL answers a redirect to that page; a `.css` row has no action.
- [ ] **Step 2: Run** the E2E suite as in `CLAUDE.md` (short flush interval). Expected: PASS.
- [ ] **Step 3: Manual check** on `multi-site-aliases` (`/siteA/...` vs `/siteB/...` land on the right site), `multi-language` (right culture) and, if available, `sql-server`. Restore databases with `git checkout` afterwards.
- [ ] **Step 4: Update `CLAUDE.md`** as listed, and add the plan to its plan list.
- [ ] **Step 5: Full verification**: `npm test`, `npm run build`, `dotnet build` (zero warnings), `dotnet test`, `pwsh scripts/check-pending-model-changes.ps1` (no model change expected), pack plus `check-package.ps1`.
- [ ] **Step 6: Commit** `docs(redirects): document redirect creation and its extension points`.

---

## Self-review

- **Spec coverage:** action and flag (5, 6), contract and default provider (3, 4), form extension type (6), endpoint statuses (5), delete + discard + serialisation (2, 3), both failure messages (3), ordering blocker and disabled setting (1, 4), replacement proofs (7), tests on SQLite/SQL Server (2, 4), E2E and manual (8), client regeneration and docs (5, 8).
- **Open facts** are isolated in Task 1 and consumed by Tasks 4 and 6 only.
- **Types:** `RedirectResult`, `CreateRedirectOutcome`, `ITrackedUrlRemover.DeleteAsync(TrackedUrl, ct)` and `canCreateRedirect` are used with the same names throughout.
