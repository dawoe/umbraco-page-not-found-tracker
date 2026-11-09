# Edit Block Rules Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** An editor changes a block rule's pattern, status code, domain and culture in place from its row on the Blocked URLs dashboard; a change that collides with another rule is refused with an inline message; the public docs describe it.

**Architecture:** `IBlockRuleService.UpdateAsync(id, ...)` validates like create, refuses a collision with another rule, updates the row in place with one `ExecuteUpdate`, then runs the tail create already has (snapshot refresh, one `Saved` cache payload, queued-hit discard, tracked URL delete) through a shared private method. `PUT block-rules/{id}` exposes it with the create body and answer. The client gets an Edit entity action (the row button; Unblock moves to the "..." menu) that opens the existing rule modal in an edit mode keyed on a new optional `ruleId` in the modal data.

**Tech Stack:** .NET 10, Umbraco 17.8, EF Core 10 (SQLite, SQL Server), NUnit + Moq (unit), Umbraco integration tests; TypeScript, Lit, Vite, Vitest, `@hey-api/openapi-ts` (client); Playwright (E2E).

**Spec:** `docs/superpowers/specs/2026-11-09-edit-block-rules-design.md`

## Global Constraints

- Branch `feature/11-edit-block-rules` off `develop` (Task 1 creates it and commits the spec and this plan).
- Every C# file starts with `// Copyright (c) Dave Woestenborghs and contributors. Licensed under the MIT License. See LICENSE in the project root for license information.`; usings outside the namespace, file-scoped namespaces, braces always, XML docs on all public and internal members. `dotnet build` (from `src/`) has **zero analyzer warnings**; fix, never suppress.
- **No schema change**: no migration, `pwsh scripts/check-pending-model-changes.ps1` must still pass.
- Identity of a rule = (`PatternHash`, `Domain`, `Culture`), unique index. Collision message `BlockingConstants.RuleExistsMessage` = `"A rule with this pattern, domain and culture already exists."`. `Id` and `Created` never change on an edit; `DisplayPattern` becomes the new trimmed input (truncated to 800).
- After the row is written, refresh, notify and the record removal use `CancellationToken.None` (as in `CreateAsync`). Exactly **one** `BlockRuleCachePayload(newPattern, newDomain, newCulture, BlockRuleChange.Saved)` is sent per update.
- `IDbContextFactory<PageNotFoundTrackerDbContext>` is resolved from `IServiceProvider` per call, never in a constructor.
- Endpoint: `PUT /umbraco/pagenotfoundtracker/api/v1/block-rules/{id:int}`, operation id `UpdateBlockRule`, policy `Constants.DashboardAccessPolicy`, body `CreateBlockRuleRequestModel`, 200 `CreateBlockRuleResponseModel`, 400 / 404 / 409 / 503 problem details (`Type = "Error"`).
- Client: Edit action alias `Pnft.EntityAction.BlockRule.Edit`, weight **0**, icon `icon-edit`; Unblock weight **-10**. Localization keys `pageNotFoundTracker_*` in `src/localization/en.ts`. Data marks and the E2E copy (`tests/blocked.spec.ts` `MARKS`) change together. No `localhost` in shipped client code.
- Client regeneration: `NODE_TLS_REJECT_UNAUTHORIZED=0 npm run generate-client` against the running single-site; commit only `src/api/index.ts`, `src/api/sdk.gen.ts`, `src/api/types.gen.ts`; `git checkout` the line-ending-only diffs.
- After running the test site: `git checkout -- test-sites/Website-V17/appsettings.json test-sites/Website-V17/umbraco/Data/*.sqlite.db`; never commit them.
- Docs rules (spec 7): one page owns each fact; `check-doc-links.ps1`, `check-doc-samples.ps1`, `ConfigurationDocsTests` and `npm run check-samples` pass; public pages never link to `CLAUDE.md` or `docs/superpowers/`.
- SQL Server integration cases: run from PowerShell with `$env:PNFT_SQLSERVER_CONNECTION='Server=(localdb)\pnft-test;Integrated Security=True;TrustServerCertificate=True'` (Git Bash cannot reach LocalDB).
- Commits end with `Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>`.

## Review Focus

1. Editing a rule without changing its identity (only the status code, or only the spelling `/WP-Admin/*` of `/wp-admin/*`) must be `Saved`, not `Conflict`: the collision check excludes the rule itself. Pinned in Task 2 (`Update_of_only_the_status_or_spelling_of_the_same_rule_is_Saved`).
2. Two editors (or servers) edit different rules into the same identity at once: the second write hits the unique index and must answer `Conflict`, never a 500, and never report a connection failure as a conflict. Pinned in Task 2 (`Update_unique_violation_on_the_write_is_Conflict`).
3. Narrowing a pattern must free the paths it no longer covers at once: the old path is answered normally and tracked again, the new path gets the new status with an empty body. Pinned in Task 4 (`Edited_rule_frees_the_old_path_and_blocks_the_new_one`).
4. The rule was unblocked by someone else while the dialog was open: the save answers 404, the dialog shows the client's text inline, and closing it reloads the list so the gone row disappears. Pinned in Task 3 (`Update_UnknownId_Returns404`), Task 5 (404 mapping) and Task 6 (`openRuleModalAndReload` reloads on cancel in edit mode; reviewed by reading, the opener has no unit test harness).
5. A rule whose status code was set to another 4xx through the API (say 451) is edited without touching the status: the dialog must preselect that code and keep it. Pinned in Task 6 (`buildStatusChoices_AddsAnUnknownCurrentCode`).

---

### Task 1: Branch, spec and plan

**Files:**
- Commit: `docs/superpowers/specs/2026-11-09-edit-block-rules-design.md`, `docs/superpowers/plans/2026-11-09-edit-block-rules.md`

- [ ] **Step 1: Create the branch**

```bash
git checkout -b feature/11-edit-block-rules develop
```

- [ ] **Step 2: Commit the spec and the plan**

```bash
git add docs/superpowers/specs/2026-11-09-edit-block-rules-design.md docs/superpowers/plans/2026-11-09-edit-block-rules.md
git commit -m "docs: add the edit block rules spec and plan"
```

Expected: `git status` clean, `git branch --show-current` prints `feature/11-edit-block-rules`.

---

### Task 2: Contract and `UpdateAsync` in the service

**Files:**
- Modify: `src/code/Umbraco.Community.PageNotFoundTracker.Core/Blocking/IBlockRuleService.cs`
- Modify: `src/code/Umbraco.Community.PageNotFoundTracker.Core/Blocking/CreateBlockRuleStatus.cs`
- Modify: `src/code/Umbraco.Community.PageNotFoundTracker.Core/Blocking/CreateBlockRuleOutcome.cs` (XML doc: outcome of `CreateAsync` and `UpdateAsync`)
- Modify: `src/code/Umbraco.Community.PageNotFoundTracker.Core/Blocking/BlockingConstants.cs`
- Modify: `src/code/Umbraco.Community.PageNotFoundTracker.Infrastructure/Blocking/BlockRuleService.cs`
- Test: `src/test/Umbraco.Community.PageNotFoundTracker.Infrastructure.Tests/Blocking/BlockRuleServiceTests.cs`

**Interfaces:**
- Produces (Core):
  - `Task<CreateBlockRuleOutcome> UpdateAsync(int id, string pattern, int statusCode, string domain, string culture, CancellationToken cancellationToken);` on `IBlockRuleService`
  - `CreateBlockRuleStatus.NotFound` ("no rule with that id; nothing changed") and `CreateBlockRuleStatus.Conflict` ("another rule has the new pattern, domain and culture; nothing changed"), appended after `Invalid`. The XML doc of `CreateAsync` says it returns `Saved` or `Invalid` only.
  - `public const string RuleExistsMessage = "A rule with this pattern, domain and culture already exists.";` in `BlockingConstants`
- Produces (Infrastructure, private, names for the plan only): `CompleteSaveAsync(string pattern, string domain, string culture)` = the tail (refresh with `CancellationToken.None`, `NotifyAsync` one `Saved` payload, `RemoveMatchingRecordsAsync`, build the `Saved` outcome), used by `CreateAsync` and `UpdateAsync`.

- [ ] **Step 1: Write the failing tests** in `BlockRuleServiceTests` (unit, in-memory SQLite, the fixture's `calls`, `RulesAsync`, `AddTrackedAsync`, `TrackedAsync`, `QueuedHit` helpers). One `Update*` test per bullet:

```csharp
[Test]
public async Task Update_changes_every_field_keeps_id_and_created_then_refreshes_notifies_and_deletes_in_order()
{
    await this.service.CreateAsync("/old", 410, string.Empty, string.Empty, CancellationToken.None);
    BlockRule before = (await this.RulesAsync()).Single();
    await this.AddTrackedAsync("/wp-admin/a", "www.a.local", "en-US");
    await this.AddTrackedAsync("/other", "www.a.local", "en-US");
    this.calls.Clear();

    CreateBlockRuleOutcome outcome = await this.service.UpdateAsync(before.Id, " /WP-Admin/* ", 403, " WWW.A.local ", " en-us ", CancellationToken.None);

    BlockRule rule = (await this.RulesAsync()).Single();
    Assert.Multiple(async () =>
    {
        Assert.That(outcome, Is.EqualTo(new CreateBlockRuleOutcome(CreateBlockRuleStatus.Saved, null, true)));
        Assert.That(rule.Id, Is.EqualTo(before.Id));
        Assert.That(rule.Created, Is.EqualTo(before.Created));
        Assert.That(rule.Pattern, Is.EqualTo("/wp-admin/*"));
        Assert.That(rule.PatternHash, Is.EqualTo(TrackedUrlNormalizer.Hash("/wp-admin/*")));
        Assert.That(rule.DisplayPattern, Is.EqualTo("/WP-Admin/*"));
        Assert.That(rule.StatusCode, Is.EqualTo(403));
        Assert.That(rule.Domain, Is.EqualTo("www.a.local"));
        Assert.That(rule.Culture, Is.EqualTo("en-US"));
        Assert.That(this.calls, Is.EqualTo(new[] { "refresh", "notify:Saved", "exclusive" }));
        Assert.That((await this.TrackedAsync()).Select(r => r.RelativePath), Is.EqualTo(new[] { "/other" }));
    });
    this.notifier.Verify(
        n => n.NotifyAsync(new BlockRuleCachePayload("/wp-admin/*", "www.a.local", "en-US", BlockRuleChange.Saved), It.IsAny<CancellationToken>()),
        Times.Once);
}
```

- `Update_of_unknown_id_is_NotFound_and_does_nothing`: `UpdateAsync(999, "/x", 410, "", "")` → `Status == NotFound`, `RecordsRemoved == false`, `RulesAsync()` empty, `calls` empty.
- `Update_with_invalid_pattern_or_status_is_Invalid_and_changes_nothing` with `[TestCase("/*", 410)]`, `[TestCase("/umbraco/x*", 410)]`, `[TestCase("/old", 302)]`: create `/old` 410 first, then update with the case → `Status == Invalid`, `Message` not empty, the stored rule still `/old` with 410, `calls` has only create's three entries.
- `Update_into_another_rules_identity_is_Conflict_and_changes_nothing`: create `/a` 410 and `/b` 404; update b's id to `" /A/ "` 403 → `Status == Conflict`, `Message == BlockingConstants.RuleExistsMessage`, both rules unchanged (patterns `/a`, `/b`, statuses 410, 404), no new `calls` after the two creates.
- `Update_of_only_the_status_or_spelling_of_the_same_rule_is_Saved`: create `/wp-admin/*` 410; update its id to `"/WP-Admin/*/"` 403 → `Saved`, one rule, `Pattern` `/wp-admin/*`, `DisplayPattern` `/WP-Admin/*/`, `StatusCode` 403, same `PatternHash`.
- `Update_canonicalises_the_culture` with `[TestCase("en-us", "en-US")]`, `[TestCase("Not-A-Culture-X", "not-a-culture-x")]`: create `/old` with no culture, update with the case → stored `Culture` equals `stored`.
- `Update_delete_failure_keeps_the_change_and_returns_RecordsRemoved_false`: create `/old`, drop the tracked table (as `Delete_failure_keeps_the_rule_and_returns_RecordsRemoved_false` does), update to `/new` → `Saved`, `RecordsRemoved == false`, `Message == BlockingConstants.RecordsNotRemovedMessage`, the rule reads `/new`.
- `Update_discards_matching_queued_hits_in_scope_only`: create `/old`; enqueue `QueuedHit("/new/a", "", "")`, `QueuedHit("/new/a", "www.b.local", "")`, `QueuedHit("/other", "", "")`; update to `/new/*` with domain `www.b.local` → the queue (`this.queue.Drain()` or the accessor the existing `Create_discards_matching_queued_hits_in_scope_only` test uses) holds `/new/a` for the empty domain and `/other` only.
- `Update_cancelled_after_the_write_still_refreshes_notifies_and_removes_records`: like `Create_cancelled_after_the_save_still_refreshes_notifies_and_removes_records`, with a `DbCommandInterceptor` that cancels the `CancellationTokenSource` in `NonQueryExecutedAsync` when `command.CommandText` contains `UPDATE` and `BlockingConstants.TableName` (add it next to `CancelAfterRuleDeleteInterceptor`; extend `BuildServiceCancellingAfterWrite` with a third mode or add a sibling builder). Assert `Saved`, `calls` ends with `"refresh", "notify:Saved", "exclusive"`, the tracked row the new pattern covers is gone.
- `Update_unique_violation_on_the_write_is_Conflict`: create `/a` 410 and `/b` 404; build the service with a `DbCommandInterceptor` whose `NonQueryExecutingAsync` fires once when the text contains `UPDATE` and `BlockingConstants.TableName`: it runs, on `command.Connection`, an `INSERT` of a rule with `PatternHash = TrackedUrlNormalizer.Hash("/c")`, `Pattern = "/c"`, `DisplayPattern = "/c"`, `StatusCode = 410`, `Domain = ''`, `Culture = ''`, `Created` now, then lets the update run. Update b's id to `/c` → `Status == Conflict`, `Message == RuleExistsMessage`, rule b still `/b` 404, three rules in the table, no refresh/notify/exclusive calls after the creates.

- [ ] **Step 2: Run to verify they fail**

Run (from `src/`): `dotnet test test/Umbraco.Community.PageNotFoundTracker.Infrastructure.Tests --filter "FullyQualifiedName~BlockRuleServiceTests"`
Expected: build error (`UpdateAsync`, `NotFound`, `Conflict`, `RuleExistsMessage` not defined).

- [ ] **Step 3: Add the contract** (`UpdateAsync`, the two enum values, the constant, the XML docs as described under Interfaces; the `IBlockRuleService` summary becomes "Lists, creates, updates and removes block rules").

- [ ] **Step 4: Implement `UpdateAsync` in `BlockRuleService`**, in the spec's order:

1. Normalise and canonicalise as `CreateAsync` does (pull the shared part into a private helper that returns the prepared `BlockRule` values plus the validation message, so the two methods cannot drift).
2. `Invalid` on a validation message.
3. In one context: `FirstOrDefaultAsync(r => r.Id == id)` → `NotFound` when null; `AnyAsync(r => r.Id != id && r.PatternHash == hash && r.Domain == domain && r.Culture == culture)` → `Conflict`; then `Where(r => r.Id == id).ExecuteUpdateAsync(setters => ...)` setting `Pattern`, `PatternHash`, `DisplayPattern`, `StatusCode`, `Domain`, `Culture`. Zero rows → `NotFound`. Wrap the `ExecuteUpdateAsync` in `try { } catch (DbException)`: re-run the `AnyAsync` collision query; when it is true return `Conflict`, else `throw`.
4. `return await this.CompleteSaveAsync(hash's pattern, domain, culture);` where `CompleteSaveAsync` is the tail extracted from `CreateAsync` (refresh, notify `Saved`, `RemoveMatchingRecordsAsync`, build the outcome). `CreateAsync` calls the same method; its behaviour and its tests do not change.

- [ ] **Step 5: Run to verify they pass** (same filter; the whole fixture, so the create tests still pass). Expected: PASS, zero warnings in the build output.

- [ ] **Step 6: Commit**

```bash
git add src/code/Umbraco.Community.PageNotFoundTracker.Core/Blocking src/code/Umbraco.Community.PageNotFoundTracker.Infrastructure/Blocking/BlockRuleService.cs src/test/Umbraco.Community.PageNotFoundTracker.Infrastructure.Tests/Blocking/BlockRuleServiceTests.cs
git commit -m "feat: update a block rule in place through the rule service"
```

---

### Task 3: `PUT block-rules/{id}`

**Files:**
- Create: `src/code/Umbraco.Community.PageNotFoundTracker.BackOffice/Controllers/UpdateBlockRuleController.cs`
- Test: `src/test/Umbraco.Community.PageNotFoundTracker.BackOffice.Tests/BlockRuleControllersTests.cs`
- Test: `src/test/Umbraco.Community.PageNotFoundTracker.Integration.Tests/ManagementApi/BlockRulesApiTests.cs`

**Interfaces:**
- Consumes: `IBlockRuleService.UpdateAsync` (Task 2), `TrackedUrlsControllerBase.EnsurePersistenceAvailableAsync` / `InvalidRequest`, `CreateBlockRuleRequestModel`, `CreateBlockRuleResponseModel`.
- Produces: `public sealed class UpdateBlockRuleController(IBlockRuleService ruleService, ITrackingPersistenceState persistenceState) : TrackedUrlsControllerBase` with `[HttpPut("block-rules/{id:int}")] [MapToApiVersion("1.0")] public async Task<IActionResult> UpdateBlockRule(int id, CreateBlockRuleRequestModel? request, CancellationToken cancellationToken)`; `ProducesResponseType` for 200 (`CreateBlockRuleResponseModel`), 400, 404, 409, 503 (`ProblemDetails`). The operation id `UpdateBlockRule` is the method name (see `TrackedUrlsOperationIdHandler`), so the generated client function is `updateBlockRule`.

- [ ] **Step 1: Write the failing unit tests** in `BlockRuleControllersTests` (a `UpdateController()` factory next to `CreateController()`):

- `Update_Saved_Returns200`: service `UpdateAsync(5, "/wp-*", 410, "d", "en-us", ...)` returns `Saved, null, true` → `OkObjectResult` with `RecordsRemoved == true`, `Message == null`.
- `Update_NullScope_PassesEmptyStrings`: body without domain/culture → `UpdateAsync(5, "/a*", 404, string.Empty, string.Empty, ...)` verified once.
- `Update_Invalid_Returns400WithDetail`: `Invalid, "too broad"` → `BadRequestObjectResult`, `ProblemDetails.Detail == "too broad"`.
- `Update_NotFound_Returns404`: `NotFound` → `ObjectResult.StatusCode == 404`, `ProblemDetails.Title == "Block rule not found"`.
- `Update_Conflict_Returns409WithDetail`: `Conflict, BlockingConstants.RuleExistsMessage` → `ObjectResult.StatusCode == 409`, `Detail == BlockingConstants.RuleExistsMessage`.
- `Update_BlankPattern_Returns400` with `[TestCase(null)] [TestCase("")] [TestCase("   ")]` plus a null body → `BadRequestObjectResult`, `UpdateAsync` never called.
- Extend `Unavailable_Returns503ForAllEndpoints` with `UpdateController().UpdateBlockRule(1, new CreateBlockRuleRequestModel { Pattern = "/a", StatusCode = 410 }, ...)` → 503.

- [ ] **Step 2: Write the failing integration tests** in `BlockRulesApiTests` (helper `PutAsync(int id, string pattern, int statusCode)` mirroring `PostAsync`, body `{ pattern, statusCode, domain: "", culture: "" }`):

- Add a PUT to `Anonymous_DeleteAndScopeOptions_Return401` (rename to `Anonymous_DeleteUpdateAndScopeOptions_Return401`) → 401, and to `GroupOutsideSetting_Delete_Returns403` → 403.
- `Update_ChangesTheRule_ThenList_ShowsNewValues`: sign in, `PostAsync("/to-edit*", 410)`, read the id from `?search=to-edit`, `PutAsync(id, "/Edited-Rule/*", 403)` → 200 with `recordsRemoved` present; list `?search=edited-rule` has one item with `id == id`, `pattern == "/Edited-Rule/*"`, `statusCode == 403`; list `?search=to-edit` has `total == 0`.
- `Update_UnknownId_Returns404`: `PutAsync(999999, "/x", 410)` → 404, body `detail` not empty.
- `Update_IntoExistingRule_Returns409WithDetail`: post `/conflict-a` and `/conflict-b`, read b's id, `PutAsync(idB, "/conflict-a", 410)` → 409, `detail == "A rule with this pattern, domain and culture already exists."`.
- `Update_TooBroadPattern_Returns400WithDetail`: post `/to-break`, `PutAsync(id, "/*", 410)` → 400, `detail` not empty.

- [ ] **Step 3: Run to verify they fail**

Run (from `src/`): `dotnet test test/Umbraco.Community.PageNotFoundTracker.BackOffice.Tests --filter "FullyQualifiedName~BlockRuleControllersTests"` then `dotnet test test/Umbraco.Community.PageNotFoundTracker.Integration.Tests --filter "FullyQualifiedName~BlockRulesApiTests"`
Expected: build error (`UpdateBlockRuleController` not defined); the integration PUTs would answer 405/404.

- [ ] **Step 4: Create `UpdateBlockRuleController`** as `CreateBlockRuleController` with the id: blank pattern → `InvalidRequest("Invalid block rule", "The pattern is required.")`; persistence gate; `UpdateAsync`; map `Saved` → 200 (message fallback as create), `Invalid` → `InvalidRequest("Invalid block rule", message ?? "The block rule is not valid.")`, `NotFound` → 404 problem `"Block rule not found"` / `"The block rule does not exist (anymore)."` (the delete controller's text), `Conflict` → 409 problem `"Block rule exists"` / `message ?? BlockingConstants.RuleExistsMessage`. A private `Problem(int status, string title, string detail)` helper as in `CreateTrackedUrlRedirectController`.

- [ ] **Step 5: Run both test commands.** Expected: PASS.

- [ ] **Step 6: Commit**

```bash
git add src/code/Umbraco.Community.PageNotFoundTracker.BackOffice/Controllers/UpdateBlockRuleController.cs src/test/Umbraco.Community.PageNotFoundTracker.BackOffice.Tests/BlockRuleControllersTests.cs src/test/Umbraco.Community.PageNotFoundTracker.Integration.Tests/ManagementApi/BlockRulesApiTests.cs
git commit -m "feat: add the update block rule endpoint"
```

---

### Task 4: Database and pipeline integration tests

**Files:**
- Test: `src/test/Umbraco.Community.PageNotFoundTracker.Integration.Tests/Blocking/BlockRuleServiceTests.cs`
- Test: `src/test/Umbraco.Community.PageNotFoundTracker.Integration.Tests/Blocking/BlockMiddlewareIntegrationTests.cs`

**Interfaces:**
- Consumes: `IBlockRuleService.UpdateAsync` (Task 2); the fixtures' `Row`, `AddRowsAsync`, `GetRowsAsync`, `GetRulesAsync`, `Hit`, `flushService.FlushAsync`; `PageNotFoundTestBase.GetAsync`, `Recorder.Occurrences`.

- [ ] **Step 1: Write the failing tests** in the SQLite/SQL Server `BlockRuleServiceTests`:

```csharp
[Test]
public async Task Update_changes_the_rule_and_deletes_the_rows_the_new_pattern_covers()
{
    await this.AddRowsAsync(Row("/old-a"), Row("/new-a"), Row("/new-b/x"), Row("/other"));
    await this.service.CreateAsync("/old-a", 410, string.Empty, string.Empty, CancellationToken.None);
    BlockRule before = (await this.GetRulesAsync()).Single();

    CreateBlockRuleOutcome outcome = await this.service.UpdateAsync(before.Id, "/New-*", 403, string.Empty, string.Empty, CancellationToken.None);

    BlockRule after = (await this.GetRulesAsync()).Single();
    Assert.Multiple(async () =>
    {
        Assert.That(outcome, Is.EqualTo(new CreateBlockRuleOutcome(CreateBlockRuleStatus.Saved, null, true)));
        Assert.That(after.Id, Is.EqualTo(before.Id));
        Assert.That(after.Created, Is.EqualTo(before.Created));
        Assert.That((after.Pattern, after.DisplayPattern, after.StatusCode), Is.EqualTo(("/new-*", "/New-*", 403)));
        Assert.That((await this.GetRowsAsync()).Select(r => r.RelativePath), Is.EqualTo(new[] { "/other" }));
    });
}
```

- `Update_into_another_rules_identity_is_refused_and_changes_nothing`: create `/a` 410 and `/b` 404; update b to `/A/` 403 → `Conflict` with `RuleExistsMessage`; `GetRulesAsync()` still `(/a, 410)`, `(/b, 404)`.
- `Update_then_old_path_is_tracked_again_from_one`: `Row("/old")` with `HitCount = 5`; create `/old`; update to `/new`; enqueue `Hit(Row("/old"))`, `FlushAsync` → rows `("/old", 1L)` only.

- [ ] **Step 2: Write the failing pipeline test** in `BlockMiddlewareIntegrationTests`:

```csharp
[Test]
public async Task Edited_rule_frees_the_old_path_and_blocks_the_new_one()
{
    IBlockRuleService rules = this.GetRequiredService<IBlockRuleService>();
    await this.WaitForPersistenceAsync(); // the ready wait AddRuleAsync does; extract it
    await rules.CreateAsync("/pnft-old", 410, string.Empty, string.Empty, CancellationToken.None);
    using (HttpResponseMessage blocked = await this.GetAsync("/pnft-old"))
    {
        Assert.That(blocked.StatusCode, Is.EqualTo(HttpStatusCode.Gone));
    }
    int id = (await rules.QueryAsync(new BlockRuleQuery(0, 10, BlockRuleSortColumn.Created, true, "pnft-old", TrackedUrlFilter.Any, TrackedUrlFilter.Any), CancellationToken.None)).Items.Single().Id;

    CreateBlockRuleOutcome outcome = await rules.UpdateAsync(id, "/pnft-new/*", 403, string.Empty, string.Empty, CancellationToken.None);
    using HttpResponseMessage old = await this.GetAsync("/pnft-old");
    using HttpResponseMessage fresh = await this.GetAsync("/pnft-new/x");

    Assert.Multiple(async () =>
    {
        Assert.That(outcome.Status, Is.EqualTo(CreateBlockRuleStatus.Saved));
        Assert.That(old.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
        Assert.That(this.Recorder.Occurrences.Select(o => o.PathAndQuery), Does.Contain("/pnft-old"));
        Assert.That(fresh.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
        Assert.That(await fresh.Content.ReadAsStringAsync(), Is.Empty);
        Assert.That(this.Recorder.Occurrences.Select(o => o.PathAndQuery), Does.Not.Contain("/pnft-new/x"));
    });
}
```

(Check the `BlockRuleQuery` constructor and the recorder's occurrence property names against the files before writing; adjust the call, not the intent.)

- [ ] **Step 3: Run to verify they fail**

Run (from `src/`): `dotnet test test/Umbraco.Community.PageNotFoundTracker.Integration.Tests --filter "FullyQualifiedName~Blocking.BlockRuleServiceTests|FullyQualifiedName~BlockMiddlewareIntegrationTests"`
Expected: the new tests fail only if Task 2 left a gap; otherwise they pass at once (the code exists). Either way run them: this task's deliverable is the proof on a real database and in the real pipeline.

- [ ] **Step 4: Fix anything the tests expose**, then run the same filter from PowerShell with `PNFT_SQLSERVER_CONNECTION` set (see Global Constraints). Expected: PASS on SQLite and SQL Server.

- [ ] **Step 5: Commit**

```bash
git add src/test/Umbraco.Community.PageNotFoundTracker.Integration.Tests/Blocking/BlockRuleServiceTests.cs src/test/Umbraco.Community.PageNotFoundTracker.Integration.Tests/Blocking/BlockMiddlewareIntegrationTests.cs
git commit -m "test: prove block rule updates on both databases and in the pipeline"
```

---

### Task 5: Generated client and the update mapping

**Files:**
- Regenerate: `src/code/Umbraco.Community.PageNotFoundTracker.BackOffice.Client/src/api/{index.ts,sdk.gen.ts,types.gen.ts}`
- Modify: `src/code/Umbraco.Community.PageNotFoundTracker.BackOffice.Client/src/blocking/block-rule.service.ts`
- Test: `src/code/Umbraco.Community.PageNotFoundTracker.BackOffice.Client/src/blocking/block-rule.service.test.ts`

**Interfaces:**
- Produces: generated `updateBlockRule({ path: { id }, body })` with `UpdateBlockRuleData` / `UpdateBlockRuleResponses` / `UpdateBlockRuleErrors`; in `block-rule.service.ts`:
  - `PnftBlockRuleResult` gains `| { kind: 'conflict'; message: string | undefined }` (409: another rule has the new identity).
  - `export type PnftUpdateRuleSend = (id: number, request: PnftCreateRuleRequest) => Promise<PnftBlockRuleResponse<CreateBlockRuleResponseModel>>;`
  - `export async function updateRule(send: PnftUpdateRuleSend, id: number, request: PnftCreateRuleRequest): Promise<PnftBlockRuleResult>` (maps with `mapCreateRuleResponse`; a rejected send is `error`).

- [ ] **Step 1: Regenerate the client.** Build the client once (`npm run build`), start the site in the background from `test-sites/Website-V17`: `dotnet run --launch-profile single-site`, wait until `curl -k https://localhost:44401/umbraco/management/api/v1/server/status` answers 200, then in the client project `NODE_TLS_REJECT_UNAUTHORIZED=0 npm run generate-client`. Stop the site. Restore `client-ready`-style collateral: `git status` must show changes only in `src/api/index.ts`, `src/api/sdk.gen.ts`, `src/api/types.gen.ts`; `git checkout` any other `src/api/` file (line endings only) and the test site's `appsettings.json` and `.sqlite.db`.

Expected: `grep -n "export const updateBlockRule" src/api/sdk.gen.ts` matches; `grep -c localhost src/api/*.ts` is 0.

- [ ] **Step 2: Write the failing tests** in `block-rule.service.test.ts`:

```ts
it('maps a 409 to conflict with the server message', () => {
  expect(mapCreateRuleResponse({ error: apiError(409, 'A rule with this pattern, domain and culture already exists.') })).toEqual({
    kind: 'conflict',
    message: 'A rule with this pattern, domain and culture already exists.',
  });
});

describe('updateRule', () => {
  it('sends the id and the request and maps the answer', async () => {
    const send = vi.fn().mockResolvedValue({ data: { recordsRemoved: true, message: null } });
    expect(await updateRule(send, 7, request)).toEqual({ kind: 'saved', recordsRemoved: true, message: undefined });
    expect(send).toHaveBeenCalledWith(7, request);
  });

  it('maps a rejected send to error instead of throwing', async () => {
    expect(await updateRule(vi.fn().mockRejectedValue(new Error('network')), 7, request)).toEqual({ kind: 'error' });
  });
});
```

- [ ] **Step 3: Run to verify they fail**

Run (client project): `npx vitest run src/blocking/block-rule.service.test.ts`
Expected: FAIL (`updateRule` not exported; 409 maps to `error`).

- [ ] **Step 4: Implement** the `conflict` kind (`case 409` in `mapError`), `PnftUpdateRuleSend` and `updateRule`.

- [ ] **Step 5: Run to verify they pass** (same command, then `npm test` for the whole suite). Expected: PASS.

- [ ] **Step 6: Commit**

```bash
git add src/code/Umbraco.Community.PageNotFoundTracker.BackOffice.Client/src/api/index.ts src/code/Umbraco.Community.PageNotFoundTracker.BackOffice.Client/src/api/sdk.gen.ts src/code/Umbraco.Community.PageNotFoundTracker.BackOffice.Client/src/api/types.gen.ts src/code/Umbraco.Community.PageNotFoundTracker.BackOffice.Client/src/blocking/block-rule.service.ts src/code/Umbraco.Community.PageNotFoundTracker.BackOffice.Client/src/blocking/block-rule.service.test.ts
git commit -m "feat(client): regenerate the API client and map the update block rule answers"
```

---

### Task 6: The rule modal's edit mode

**Files:**
- Modify: `src/code/Umbraco.Community.PageNotFoundTracker.BackOffice.Client/src/blocking/rule-modal.token.ts`
- Modify: `src/code/Umbraco.Community.PageNotFoundTracker.BackOffice.Client/src/blocking/rule-form.ts`
- Modify: `src/code/Umbraco.Community.PageNotFoundTracker.BackOffice.Client/src/blocking/rule-modal.element.ts`
- Modify: `src/code/Umbraco.Community.PageNotFoundTracker.BackOffice.Client/src/blocking/open-rule-modal.ts`
- Modify: `src/code/Umbraco.Community.PageNotFoundTracker.BackOffice.Client/src/localization/en.ts`
- Test: `src/code/Umbraco.Community.PageNotFoundTracker.BackOffice.Client/src/blocking/rule-form.test.ts`

**Interfaces:**
- Consumes: `updateRule`, `updateBlockRule`, the `conflict` kind (Task 5).
- Produces:
  - `PnftBlockRuleModalData.ruleId?: number` ("set for an edit: the rule to update; absent for a create").
  - `export function buildStatusChoices(current: number): Array<number>` in `rule-form.ts`: `PNFT_BLOCK_STATUS_CODES` plus `current` appended when it is not one of them.
  - Localization keys: `editRule: 'Edit'`, `editRuleHeadline: 'Edit block rule'`, `saveRule: 'Save'`, `ruleUpdated: 'The rule has been updated. Matching requests are blocked and no longer tracked.'`, `blockRuleExists: 'A rule with this pattern, domain and culture already exists.'`, `editRuleNotFound: 'This rule no longer exists. Close the dialog to reload the list.'`.

- [ ] **Step 1: Write the failing tests** in `rule-form.test.ts`:

```ts
describe('buildStatusChoices', () => {
  it('buildStatusChoices_KeepsTheThreeCodesForAKnownCode', () => {
    expect(buildStatusChoices(404)).toStrictEqual([403, 404, 410]);
  });

  it('buildStatusChoices_AddsAnUnknownCurrentCode', () => {
    expect(buildStatusChoices(451)).toStrictEqual([403, 404, 410, 451]);
  });
});
```

- [ ] **Step 2: Run to verify they fail**

Run: `npx vitest run src/blocking/rule-form.test.ts`
Expected: FAIL (`buildStatusChoices` not exported).

- [ ] **Step 3: Implement**

- `buildStatusChoices` in `rule-form.ts`; the texts in `en.ts`; `ruleId?: number` on the modal data.
- `rule-modal.element.ts`: the status select's options come from `buildStatusChoices(values.statusCode)`, the name is `this.localize.term(\`pageNotFoundTracker_status${code}\`)` for the three known codes and `String(code)` otherwise. `#onConfirm` branches on `this.data?.ruleId`: `undefined` → `createRule(...)` as today; a number → `updateRule((id, body) => tryExecute(this, updateBlockRule({ path: { id }, body }), { disableNotifications: true }), this.data.ruleId, toCreateRuleRequest(this._values))`. `#messageOf` adds `conflict` → `result.message ?? this.localize.term('pageNotFoundTracker_blockRuleExists')` and `notFound` → `this.localize.term('pageNotFoundTracker_editRuleNotFound')`. The Confirm label is `pageNotFoundTracker_saveRule` in edit mode, `pageNotFoundTracker_blockConfirm` otherwise. The headline keeps coming from `data.title`.
- `open-rule-modal.ts`: in the `catch` (cancelled) call `reload()` when `data.ruleId !== undefined` before returning; the positive toast message is `pageNotFoundTracker_ruleUpdated` when `data.ruleId !== undefined`, else `pageNotFoundTracker_blockSaved`. Update the function's doc comment.

- [ ] **Step 4: Run to verify they pass**: `npx vitest run src/blocking/rule-form.test.ts`, then `npm run build` (type check: `updateBlockRule` path typing, `ruleId` access). Expected: PASS, build succeeds.

- [ ] **Step 5: Commit**

```bash
git add src/code/Umbraco.Community.PageNotFoundTracker.BackOffice.Client/src/blocking/rule-modal.token.ts src/code/Umbraco.Community.PageNotFoundTracker.BackOffice.Client/src/blocking/rule-form.ts src/code/Umbraco.Community.PageNotFoundTracker.BackOffice.Client/src/blocking/rule-form.test.ts src/code/Umbraco.Community.PageNotFoundTracker.BackOffice.Client/src/blocking/rule-modal.element.ts src/code/Umbraco.Community.PageNotFoundTracker.BackOffice.Client/src/blocking/open-rule-modal.ts src/code/Umbraco.Community.PageNotFoundTracker.BackOffice.Client/src/localization/en.ts
git commit -m "feat(client): give the block rule modal an edit mode"
```

---

### Task 7: The Edit action, the row layout and the E2E tests

**Files:**
- Create: `src/code/Umbraco.Community.PageNotFoundTracker.BackOffice.Client/src/blocking/edit-rule.action.ts`
- Modify: `src/code/Umbraco.Community.PageNotFoundTracker.BackOffice.Client/src/blocking/constants.ts`
- Modify: `src/code/Umbraco.Community.PageNotFoundTracker.BackOffice.Client/src/blocking/manifests.ts`
- Modify: `src/code/Umbraco.Community.PageNotFoundTracker.BackOffice.Client/src/blocking/data-marks.ts` (doc comment: Umbraco's `entity-action:Pnft.EntityAction.BlockRule.Edit`)
- Test: `src/code/Umbraco.Community.PageNotFoundTracker.BackOffice.Client/src/blocking/manifests.test.ts`
- Test: `src/test/Umbraco.Community.PageNotFoundTracker.E2E/tests/blocked.spec.ts`

**Interfaces:**
- Consumes: `openRuleModalAndReload` with `ruleId` (Task 6), `PNFT_BLOCK_RULE_COLLECTION_CONTEXT`, `PnftBlockRuleCollectionModel` (`id`, `pattern`, `domain`, `culture`, `statusCode`).
- Produces: `export const PNFT_EDIT_RULE_ACTION_ALIAS = 'Pnft.EntityAction.BlockRule.Edit';`; `export class PnftEditBlockRuleEntityAction extends UmbEntityActionBase<never>` (`export { PnftEditBlockRuleEntityAction as api }`); the manifest `{ type: 'entityAction', kind: 'default', alias: PNFT_EDIT_RULE_ACTION_ALIAS, name: 'Page Not Found Tracker Edit Block Rule Entity Action', weight: 0, forEntityTypes: [PNFT_BLOCK_RULE_ENTITY_TYPE], api: () => import('./edit-rule.action.js'), meta: { icon: 'icon-edit', label: '#pageNotFoundTracker_editRule', additionalOptions: true } }`; the Unblock manifest gets `weight: -10`.

- [ ] **Step 1: Write the failing Vitest test** in `manifests.test.ts`:

```ts
it('edit is the button and unblock is in the menu on rule rows', () => {
  const edit = byAlias(PNFT_EDIT_RULE_ACTION_ALIAS);
  const unblock = byAlias(PNFT_UNBLOCK_ACTION_ALIAS);

  expect(PNFT_EDIT_RULE_ACTION_ALIAS).toBe('Pnft.EntityAction.BlockRule.Edit');
  expect(edit?.type).toBe('entityAction');
  expect(edit?.forEntityTypes).toStrictEqual([PNFT_BLOCK_RULE_ENTITY_TYPE]);
  expect(edit?.meta?.icon).toBe('icon-edit');
  expect(edit?.weight).toBe(0);
  expect(unblock?.weight).toBe(-10);
});
```

- [ ] **Step 2: Run to verify it fails**: `npx vitest run src/blocking/manifests.test.ts`. Expected: FAIL (alias not exported).

- [ ] **Step 3: Implement** the constant, the action (read the rule from the collection context by `this.args.unique` as `unblock.action.ts` does; return when missing; `openRuleModalAndReload(this, { pattern: rule.pattern, domain: rule.domain, culture: rule.culture, statusCode: rule.statusCode, title: '#pageNotFoundTracker_editRuleHeadline', ruleId: rule.id }, () => collection.loadCollection())`), the manifest and the Unblock weight. Run `npx vitest run src/blocking/manifests.test.ts` → PASS, then `npm run build`.

- [ ] **Step 4: Update `tests/blocked.spec.ts`**

- Add `const EDIT_ACTION = 'entity-action:Pnft.EntityAction.BlockRule.Edit';`.
- `unblockAllMatching`: before `row.getByTestId(UNBLOCK_ACTION).click()`, open the row's dropdown exactly as `clickBlock` does (`row.locator('umb-entity-actions-dropdown uui-button').first().click()` when present). Extract a `clickUnblock(row)` helper.
- New test `Edit_changes_the_pattern_and_the_status`: add rule `/${prefix}-edit-old` (Add rule, default 410); `statusOf('/${prefix}-edit-old')` is 410; open the blocked list for `${prefix}-edit`, click `ruleRowFor(page, oldPattern).getByTestId(EDIT_ACTION)`; the modal shows with the pattern input valued `oldPattern` and the status select `410`; fill pattern `/${prefix}-edit-new/*`, select status `403`, click confirm; the modal hides; `openBlockedAndSearch(page, \`${prefix}-edit\`)` returns exactly `['/${prefix}-edit-new/*']`; `statusOf('/${prefix}-edit-old').status` is 404; `statusOf('/${prefix}-edit-new/x')` is `{ status: 403, body: '' }`; then `unblockAllMatching(page, \`${prefix}-edit\`)`.
- New test `Edit_into_an_existing_rule_shows_the_message_and_keeps_both`: add `/${prefix}-dup-a` and `/${prefix}-dup-b`; click Edit on the `-dup-b` row, fill `/${prefix}-dup-a`, confirm; `MARKS.blockError` visible and contains `already exists`; the modal is still visible; cancel; `openBlockedAndSearch(page, \`${prefix}-dup\`)` returns both patterns (sorted any order: use `expect(patterns.sort()).toEqual([...].sort())`); then `unblockAllMatching(page, \`${prefix}-dup\`)`.

- [ ] **Step 5: Run the E2E tests.** Build the client (`npm run build`), start single-site in the background with `Umbraco__Community__PageNotFoundTracker__FlushInterval=00:00:01`, wait for status 200, then in the E2E project `npm run test:e2e -- tests/blocked.spec.ts` (also run `tests/redirect.spec.ts` and `tests/dashboard.spec.ts`, which share the dashboards). Stop the site and restore `appsettings.json` and the `.sqlite.db`.

Expected: all blocked, redirect and dashboard tests pass.

- [ ] **Step 6: Commit**

```bash
git add src/code/Umbraco.Community.PageNotFoundTracker.BackOffice.Client/src/blocking/edit-rule.action.ts src/code/Umbraco.Community.PageNotFoundTracker.BackOffice.Client/src/blocking/constants.ts src/code/Umbraco.Community.PageNotFoundTracker.BackOffice.Client/src/blocking/manifests.ts src/code/Umbraco.Community.PageNotFoundTracker.BackOffice.Client/src/blocking/manifests.test.ts src/code/Umbraco.Community.PageNotFoundTracker.BackOffice.Client/src/blocking/data-marks.ts src/test/Umbraco.Community.PageNotFoundTracker.E2E/tests/blocked.spec.ts
git commit -m "feat(client): add the Edit action on block rule rows and move Unblock into the menu"
```

---

### Task 8: Documentation

**Files:**
- Modify: `docs/blocking.md`
- Modify: `docs/dashboards.md`
- Modify: `docs/extending/dashboard-actions.md`
- Modify: `docs/how-it-works.md`
- Modify: `CLAUDE.md`
- Modify: `docs/superpowers/specs/2026-11-09-edit-block-rules-design.md` (status line → `implemented (issue #11, branch feature/11-edit-block-rules)`)

- [ ] **Step 1: `docs/blocking.md`.** After the "Add a rule" section add "## Edit a rule": click **Edit** on a row of the **Blocked URLs** dashboard, the same dialog opens with the rule's values; every field can be changed; **Save** applies the change at once on every server; the tracked URLs the new pattern covers are removed from the list; URLs the old pattern covered but the new one does not are answered normally again and tracked again from one; the rule keeps its **Created** date. Under "Safeguards" add a bullet: changing a rule so that it has the same pattern, domain and culture as another rule is refused: *"A rule with this pattern, domain and culture already exists."* (the note "Adding a pattern that already exists ... only updates its status code" stays, it describes adding). Update the sentence "To remove a rule, click **Unblock**" to say it is in the row's **...** menu.

- [ ] **Step 2: `docs/dashboards.md`.** In "Blocked URLs" replace the Unblock bullet with: **Edit** (the button on a row) opens the rule for changes (see [Blocking](blocking.md#edit-a-rule)); **Unblock** (in the **...** menu next to it) removes the rule after a confirmation.

- [ ] **Step 3: `docs/extending/dashboard-actions.md`.** In "How the actions are shown": the package's own actions are **Create redirect** (weight 0) and **Block** (weight -10) on tracked URLs, **Edit** (weight 0) and **Unblock** (weight -10) on block rules; the following sentence about negative and positive weights now applies to both kinds of rows.

- [ ] **Step 4: `docs/how-it-works.md`.** In "How redirects and blocks relate", extend the Block bullet: a changed block applies at once; the URLs it no longer covers are tracked again from one.

- [ ] **Step 5: `CLAUDE.md`, section "Blocked URLs (spec 6)".** Add to the overview paragraph that rules can be edited (spec 9, issue #11). In "Create and delete": rename to "Create, update and delete" and add `UpdateAsync(id, pattern, statusCode, domain, culture)`: validates like create, `NotFound`, `Conflict` (`RuleExistsMessage`, the `DbException` recheck), one `ExecuteUpdate` by id keeping `Id` and `Created`, then the shared tail with one `Saved` payload. In "Endpoints": the `UpdateBlockRule` row (PUT, body, 200/400/404/409/503). In "Client": the Edit action (alias, weight 0, `icon-edit`, `edit-rule.action.ts`), Unblock at -10 and in the dropdown, the modal's `ruleId` edit mode, `buildStatusChoices`, `updateRule`/`conflict`, the opener's reload-on-cancel in edit mode, the new texts. In "Data marks": `entity-action:Pnft.EntityAction.BlockRule.Edit`. In "Tests": the new unit, integration, Vitest and E2E tests by name. Add the spec and plan to the lists at the top of the file (spec 9, plan "Edit block rules"). Keep the section's existing wording otherwise.

- [ ] **Step 6: Run the documentation checks** (from the repo root):

```bash
pwsh .github/scripts/check-doc-links.ps1
pwsh .github/scripts/check-doc-samples.ps1
dotnet test src/test/Umbraco.Community.PageNotFoundTracker.Infrastructure.Tests --filter ConfigurationDocsTests
```

and in the client project `npm run check-samples`. Expected: all pass (no sample or setting changed; the link check covers the new `#edit-a-rule` anchor).

- [ ] **Step 7: Commit**

```bash
git add docs/blocking.md docs/dashboards.md docs/extending/dashboard-actions.md docs/how-it-works.md CLAUDE.md docs/superpowers/specs/2026-11-09-edit-block-rules-design.md
git commit -m "docs: describe editing block rules"
```

---

### Task 9: Manual check and full verification

**Files:**
- Modify: `CLAUDE.md` (the "Manual checks" list of the Blocked URLs section)

- [ ] **Step 1: Full build and tests** (from `src/`): `dotnet build` (zero warnings), `dotnet test` (SQL Server cases from PowerShell with `PNFT_SQLSERVER_CONNECTION`), `pwsh ../scripts/check-pending-model-changes.ps1` from the repo root, client `npm test` and `npm run build`. Expected: all green, no pending model changes.

- [ ] **Step 2: Pack check** (from `src/`): `dotnet pack -c Release -o <scratch dir>` then from the repo root `pwsh .github/scripts/check-package.ps1 -PackageDirectory <scratch dir>`. Expected: one nupkg, seven key files, no `localhost` in the bundle.

- [ ] **Step 3: Manual check on `multi-site-aliases`** with the real API and a client-credentials token (the `.mcp.json` values for `localhost:44402`), `curl -k`, `FlushInterval=00:00:01`: create `/pnft-manual-edit/*` for all domains (410): `/siteA/pnft-manual-edit/x` and `/siteB/pnft-manual-edit/x` answer 410. `PUT` it to domain `localhost:44402/sitea`, status 403: Site A answers 403 with an empty body, Site B gets the 404 page and is tracked again. `PUT` it to the pattern of another existing rule: 409 with the message. Delete every rule created. Restore `appsettings.json` and the database. Record the result as a new bullet in `CLAUDE.md` "Manual checks" (spec 9) and note what was not checked (hostnames, sql-server).

- [ ] **Step 4: Commit**

```bash
git add CLAUDE.md
git commit -m "docs: record the edit block rules manual check"
```

- [ ] **Step 5: Finish the branch** with the `superpowers:finishing-a-development-branch` skill (PR to `develop`, title "Edit block rules (#11)", body linking issue #11, the spec and the plan, closing with the attribution line from the session).
