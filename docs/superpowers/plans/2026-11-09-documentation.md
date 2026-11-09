# Documentation for Implementers Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Ship the public documentation set in `docs/` for implementers, extenders and editors, with compiled samples and CI checks that keep it correct.

**Architecture:** Markdown pages written from the shipped code. Code samples are real, compiled files (a C# samples project, a type-checked TypeScript folder, and `Program.cs` of the test site) with `docs:begin`/`docs:end` markers; the pages quote marked regions and a PowerShell script fails CI when a quote drifts. A reflection unit test fails when a setting is missing from `configuration.md`; a PowerShell script checks relative links and anchors.

**Tech Stack:** Markdown, PowerShell 7 (`pwsh`), .NET 10 + NUnit, TypeScript 6 (`tsc` only, no bundling), GitHub Actions.

**Spec:** `docs/superpowers/specs/2026-11-01-documentation-design.md` (approved 2026-11-09; issue #12). Branch `feature/12-documentation`, one pull request, docs only.

## Global Constraints

- Markdown only, relative links, readable on GitHub without a build step. Public docs in `docs/`, never in `docs/superpowers/`.
- English, plain language, short pages, one topic per page. One page owns each fact; others link to it.
- Targets Umbraco 17.x and .NET 10. No docs site, no generated API reference, no translations.
- Docs follow the shipped code, not the specs. `CLAUDE.md` is a source to verify against, never to quote or link to from public pages.
- Docs only: do not change package behaviour. A defect is documented as the code behaves, listed in the PR description and filed as its own issue. Exception: a defect that makes a documented example or the install guide fail may be fixed here, with a test, and called out.
- Every extension page states the registration rule: register from a composer marked `[ComposeAfter(typeof(PageNotFoundTrackerComposer))]`; replaceable services use `AddUnique` (last registration wins; a registration made before the package is overwritten).
- Zero analyzer warnings from `dotnet build`. Every C# file starts with the single-line copyright header from `src/stylecop.json`; XML docs on public/internal members; usings outside the namespace, file-scoped namespaces, braces always.
- The samples project is `IsPackable=false`, in `src/Umbraco.Community.PageNotFoundTracker.slnx`, references Core and Infrastructure directly, and is not referenced by the test site or `check-package.ps1`. TypeScript samples live outside the client `src/` and never reach the Vite build (the bundle must still contain no `localhost`).
- Screenshots are optional (dashboards page only, `docs/images/`); skip a view the Browser pane cannot capture and say so in the PR.
- `DocumentationUrl` is `https://github.com/dawoe/umbraco-page-not-found-tracker/blob/develop/docs/README.md`.
- Commit messages end with `Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>`.

## Review Focus

- A page quotes a sample, then the sample file changes: the check must fail (marker missing, id missing, text differs). CRLF checkout on Windows versus LF on CI must not cause false failures.
- A link works on Windows (case-insensitive) and breaks on Linux CI (case-sensitive): the link check must compare case-sensitively. Anchors must follow GitHub's heading slug rules.
- A new setting is added to `PageNotFoundTrackerSettings` (including list or `TimeSpan` types): the settings test must catch it and the `appsettings.json` example in the page must still bind.
- The doc test is run from another working directory or from the test output folder: the repo root and `docs/` must be found by walking up, not by a relative path.
- A samples file accidentally ends up in the nupkg, the test site or the client bundle: the pack check and the `localhost` bundle check must stay green.

---

### Task 1: C# samples project

**Files:**
- Create: `src/samples/Umbraco.Community.PageNotFoundTracker.Samples/Umbraco.Community.PageNotFoundTracker.Samples.csproj`
- Create: `.../Samples/NotFoundPageResolverSample.cs`, `RedirectProviderSample.cs`, `DatabaseProviderSample.cs`, `SampleComposers.cs`
- Modify: `src/Umbraco.Community.PageNotFoundTracker.slnx` (add a `/samples/` folder)

**Interfaces:**
- Consumes: `INotFoundPageResolver.ResolveAsync(NotFoundRequestContext, Domain?, CancellationToken)`, `IRedirectProvider` (`CanHandle(TrackedUrl)`, `CreateAsync(TrackedUrl, JsonElement?, CancellationToken)`), `RedirectResult.Success()/Failure(string)`, `ITrackingDatabaseProvider` (`ProviderName`, `ConfigureDbContext`), `PageNotFoundTrackerComposer`, `TrackingConstants`.
- Produces: marked regions (`docs:begin <id>` / `docs:end <id>`, in `//` comments) with ids `resolver-class`, `resolver-composer`, `redirect-provider-class`, `redirect-provider-composer`, `db-provider-class`, `db-provider-composer`. Later tasks quote these ids.

- [ ] **Step 1:** Create the csproj (`net10.0`, `IsPackable=false`, project references to Core and Infrastructure, picks up `src/Directory.Build.props`/`Directory.Packages.props`) and add it to the slnx. Run `dotnet build src/Umbraco.Community.PageNotFoundTracker.slnx -c Release`. Expected: succeeds with zero warnings (an empty project builds).
- [ ] **Step 2:** Write `NotFoundPageResolverSample` (picks the 404 page differently than the default: first published child of the domain root named by a fixed alias, otherwise `null`) and its composer with `[ComposeAfter(typeof(PageNotFoundTrackerComposer))]` and `builder.Services.AddUnique<INotFoundPageResolver, NotFoundPageResolverSample>()`. Keep each marked region to the class body and the composer only, without the file header or usings noise the docs do not need. Must be singleton-compatible (spec 2 note).
- [ ] **Step 3:** Write `RedirectProviderSample` (`CanHandle` = not a file; `CreateAsync` returns `RedirectResult.Success()` or `Failure(message)` for an invalid payload) and its `AddUnique<IRedirectProvider, ...>` composer.
- [ ] **Step 4:** Write `DatabaseProviderSample` as a minimal `ITrackingDatabaseProvider` (provider name `My.Data.Provider`, `ConfigureDbContext` with `MigrationsAssembly` and `MigrationsHistoryTable(TrackingConstants.MigrationsHistoryTable)`, as the `CLAUDE.md` Tracking example) plus an `AddSingleton<ITrackingDatabaseProvider, ...>` composer. If the EF provider extension it needs is not available, use the SQLite provider call with a comment that a real provider calls its own `UseXxx`; the file must compile. Add an XML doc comment stating a real provider ships migrations for all package tables.
- [ ] **Step 5:** Run `dotnet build src/Umbraco.Community.PageNotFoundTracker.slnx -c Release`. Expected: zero warnings, zero errors. Run `dotnet pack -c Release -o <tmp>` from `src/` then `pwsh .github/scripts/check-package.ps1 -PackageDirectory <tmp>`. Expected: passes (samples not in the package).
- [ ] **Step 6:** Commit (`docs(samples): add compiled extension samples`).

---

### Task 2: TypeScript samples, type-checked only

**Files:**
- Create: `src/code/Umbraco.Community.PageNotFoundTracker.BackOffice.Client/samples/redirect-form.element.ts`, `redirect-form.manifests.ts`, `row-action.manifests.ts`, `row-action.ts`
- Create: `.../BackOffice.Client/tsconfig.samples.json`
- Modify: `.../BackOffice.Client/package.json` (script `check-samples`)
- Modify: `.github/workflows/pr-validation.yml` (run `npm run check-samples` after `NPM test`)

**Interfaces:**
- Consumes: `PnftRedirectFormElement` and `getPayload(): Promise<unknown | undefined>` from `src/redirect/redirect-form.extension.ts`; `PNFT_TRACKED_URL_ENTITY_TYPE` from `src/entity.ts`; `PNFT_BLOCK_RULE_ENTITY_TYPE` from `src/blocking/constants.ts`; `UmbEntityActionBase`.
- Produces: marked regions (`// docs:begin <id>`) with ids `redirect-form-manifest`, `redirect-form-element`, `row-action-manifest`, `row-action-class`.

- [ ] **Step 1:** Create `tsconfig.samples.json` extending `tsconfig.json` with `include: ["samples", "src"]` (so imports resolve and the samples are checked together) and `noEmit`. Add `"check-samples": "tsc -p tsconfig.samples.json"`. Run it. Expected: passes with no samples yet.
- [ ] **Step 2:** Write the redirect form sample (manifest of type `pnftRedirectForm` with `weight: 10`; a Lit element with `@property({ attribute: false }) trackedUrl` and `getPayload()` returning `undefined` when invalid, default export) and the row action sample (an `entityAction` manifest for a `forEntityTypes` of both tracked URL and block rule entity types, weight 0, plus an `UmbEntityActionBase` subclass). Import package constants by relative path from `../src/...`.
- [ ] **Step 3:** Run `npm run check-samples`. Expected: exit 0. Break a signature on purpose (rename `getPayload`), re-run, expected: fails; revert.
- [ ] **Step 4:** Run `npm run build` and confirm `wwwroot/App_Plugins/PageNotFoundTracker` has no file from `samples/` and `pwsh .github/scripts/check-package.ps1` still passes after a pack.
- [ ] **Step 5:** Commit (`docs(samples): add type-checked client samples`).

---

### Task 3: Quoted-sample check script

**Files:**
- Create: `.github/scripts/check-doc-samples.ps1`
- Create: `.github/scripts/tests/check-doc-samples.tests.ps1` and fixtures under `.github/scripts/tests/fixtures/samples/`
- Modify: `.github/workflows/pr-validation.yml` (new step after Check package content)

**Interfaces:**
- Produces: `check-doc-samples.ps1 -RepoRoot <path> [-DocsRoot <path>]` (defaults: repo root of the script, `docs`). Exit 0 = ok, exit 1 = failures printed one per line as `<doc>:<line>: <reason>`.
- Contract: in a page, `<!-- sample: <repo-relative path>#<id> -->` on the line before a fenced block. In source, lines containing `docs:begin <id>` and `docs:end <id>` bound the region (the marker lines themselves excluded). Compare after CRLF to LF, trailing whitespace trimmed, common leading indentation removed. Also fails when a fenced block tagged `csharp` or `typescript` under `docs/` (excluding `docs/superpowers/`) has no preceding sample comment. `bash`, `json`, `jsonc`, `xml` and untagged blocks are not checked.

- [ ] **Step 1:** Write `check-doc-samples.tests.ps1`: it builds fixture trees in a temp folder and asserts the exit code and a message fragment for: matching block (0), block text differs (1, "differs"), unknown id (1, "not found"), missing file (1, "not found"), unmarked `csharp` block (1, "no sample"), CRLF source with LF page (0), indented region (0), `bash` block ignored (0).
- [ ] **Step 2:** Run `pwsh .github/scripts/tests/check-doc-samples.tests.ps1`. Expected: FAIL (script missing).
- [ ] **Step 3:** Implement `check-doc-samples.ps1` to the contract above.
- [ ] **Step 4:** Re-run the tests. Expected: all pass.
- [ ] **Step 5:** Add the CI step running the tests then the script against the repo (`pwsh ./.github/scripts/tests/check-doc-samples.tests.ps1` and `pwsh ./.github/scripts/check-doc-samples.ps1`). Commit (`ci(docs): fail when a quoted sample drifts from its source`).

---

### Task 4: Link check script

**Files:**
- Create: `.github/scripts/check-doc-links.ps1`, `.github/scripts/tests/check-doc-links.tests.ps1`, fixtures under `.github/scripts/tests/fixtures/links/`
- Modify: `.github/workflows/pr-validation.yml`

**Interfaces:**
- Produces: `check-doc-links.ps1 -RepoRoot <path>`. Checks every `[text](target)` in `docs/**/*.md` (excluding `docs/superpowers/`), `README.md` and `src/assets/readme.md`: relative targets must exist, compared case-sensitively against the real path casing; `#anchor` (same file or `file.md#anchor`) must match a heading slug using GitHub's rules (lower-case, spaces to `-`, punctuation removed except `-`, duplicates get `-1`). External `http(s):` links and `mailto:` are skipped. Output `<file>:<line>: <reason>`, exit 1 on any failure.

- [ ] **Step 1:** Write the tests: valid relative link (0), missing file (1), wrong-case file name (1), valid and broken anchor, duplicate-heading anchor `#x-1`, external link skipped, link inside a fenced code block ignored.
- [ ] **Step 2:** Run them. Expected: FAIL (script missing).
- [ ] **Step 3:** Implement the script.
- [ ] **Step 4:** Re-run. Expected: all pass.
- [ ] **Step 5:** Add the CI step (tests, then the script on the repo). Commit (`ci(docs): check relative links and anchors`).

---

### Task 5: `configuration.md` and the settings test

**Files:**
- Create: `docs/configuration.md`
- Create: `src/test/Umbraco.Community.PageNotFoundTracker.Infrastructure.Tests/Docs/ConfigurationDocsTests.cs`

**Interfaces:**
- Consumes: `PageNotFoundTrackerSettings` (properties `NotFoundPageDocumentTypeAlias`, `TrackingEnabled`, `FlushInterval`, `IncludeQueryString`, `ChannelCapacity`, `DashboardUserGroups`), `Constants.SectionName`.
- Produces: tests `Every_setting_is_documented`, `Example_appsettings_binds_to_the_settings_class`; a helper `FindRepoRoot()` that walks up from `AppContext.BaseDirectory` until `docs/configuration.md` and `src/` exist.

- [ ] **Step 1:** Write the two tests. The first reflects over the public settable properties of `PageNotFoundTrackerSettings` and asserts each name appears as `` `Name` `` in `docs/configuration.md`; the failure message names the missing setting. The second extracts the first fenced `json` block, binds it through `ConfigurationBuilder.AddJsonStream` at `Constants.SectionName`, and asserts `DashboardUserGroups`, `FlushInterval` and the other documented values come through.- [ ] **Step 2:** Run `dotnet test src/test/Umbraco.Community.PageNotFoundTracker.Infrastructure.Tests --filter ConfigurationDocsTests`. Expected: FAIL (page missing).
- [ ] **Step 3:** Write `docs/configuration.md` from `PageNotFoundTrackerSettings` and `CLAUDE.md` Tracking and Dashboard sections (verify each default and rule in the code): a table per area (404 page, tracking, dashboard access) with name, type, default, effect and an example; "read at startup, a change needs a restart"; the `DashboardUserGroups` rules (empty means `admin` and `editor`, `admin` always passes, case-insensitive, blank entries fall back with a warning, set through environment variables as `...__DashboardUserGroups__0`); a full `appsettings.json` example.
- [ ] **Step 4:** Run the filter again. Expected: PASS. Temporarily remove one setting name from the page and confirm the first test fails with that name; restore.
- [ ] **Step 5:** Commit (`docs: add configuration reference with a settings coverage test`).

---

### Task 6: `getting-started.md`

**Files:**
- Create: `docs/getting-started.md`
- Modify: `test-sites/Website-V17/Program.cs` (add `docs:begin program-pipeline` / `docs:end program-pipeline` comment markers only)

**Interfaces:**
- Consumes: the Program.cs pipeline (`BootUmbracoAsync`, `UsePageNotFoundTracker`, `UseUmbraco`); `configuration.md` anchors.

- [ ] **Step 1:** Add the markers around the pipeline in the test site `Program.cs` and confirm `dotnet build` still has zero warnings.
- [ ] **Step 2:** Write the page, quoting `program-pipeline` with a sample comment. Cover, verified against the code and the test site: requirements (Umbraco 17.x, .NET 10, SQLite and SQL Server); `dotnet add package`; the single `UsePageNotFoundTracker()` call, its position and that it registers both the early blocking middleware and the file 404 middleware; creating a 404 document type and page and setting `NotFoundPageDocumentTypeAlias` (link to configuration); first run (migrations run once Umbraco reaches runtime level `Run`: unattended install versus installer UI); verifying (request a missing page and a missing file, wait one flush interval, open the dashboard; mention the short flush interval for trying it out); unsupported database (tracking off, error logged, link to the database provider page); load-balanced setups (what each server does, what is shared: database, block rule cache refresher, and the per-process queue).
- [ ] **Step 3:** Run `pwsh .github/scripts/check-doc-samples.ps1` and `pwsh .github/scripts/check-doc-links.ps1`. Expected: pass (links to pages not written yet are added in the later tasks; until then leave them out and add them as pages appear).
- [ ] **Step 4:** Commit (`docs: add getting started guide`).

---

### Task 7: `how-it-works.md` and `multi-site-and-language.md`

**Files:**
- Create: `docs/how-it-works.md`, `docs/multi-site-and-language.md`

- [ ] **Step 1:** Write `how-it-works.md` from `CLAUDE.md` (404 handling, Tracking, Blocked URLs) verified against the code: the two detection paths (page via last-chance finder, file via middleware) and their differences; paths never handled (`UmbracoOwnedPaths`, `/.well-known`, files only for GET/HEAD with no body); tracking identity (domain, culture, normalised path, optional query string), what a record holds, batching, the flush interval and the queue/pending limits, that the dashboard is as fresh as the last flush; precedence of blocking, redirects and 404 handling (block first, then Umbraco redirects, then the 404 page).
- [ ] **Step 2:** Write `multi-site-and-language.md`: how domains and cultures decide the 404 page, the tracked record, redirects and block rules, with the four scenarios as worked examples using the test site's real URLs and ports (single-site 44401, aliases 44402 `/siteA`, `/siteB`, multi-language 44403 `/en` `/nl` `/fr`, hostnames 44404) and the rule that a request matching no domain falls back to the first site.
- [ ] **Step 3:** Check every example against a running profile with `curl -k` (aliases and multi-language at least; hostnames with `--resolve`): the status code, the page title marker and, after a short flush, the tracked row. Fix the page until each matches. Restore the test site files afterwards (`git checkout -- test-sites/Website-V17/appsettings.json test-sites/Website-V17/umbraco/Data/*.sqlite.db`).
- [ ] **Step 4:** Run the link check. Commit (`docs: explain how detection and tracking work, and multi-site rules`).

---

### Task 8: `dashboards.md`, `redirects.md`, `blocking.md`

**Files:**
- Create: `docs/dashboards.md`, `docs/redirects.md`, `docs/blocking.md`; optionally `docs/images/*.png`

- [ ] **Step 1:** `dashboards.md` (for editors): where to find the "Page not found" and "Blocked URLs" dashboards in the Content section, columns, search (case-insensitive, contains), sorting, domain and culture filters, page size 20/50/100, not live (reload), who can see it (link to `configuration.md#...` for `DashboardUserGroups`; Content section access still required), what the row actions do. Screenshots only where the Browser pane captures a view cleanly.
- [ ] **Step 2:** `redirects.md`: step by step with the Create redirect action; page rows only; the target must be published (and in the row's culture for variant targets); rows with a query string and file rows have no action; replacing an existing redirect for the same route; the result in Umbraco's Redirect URL Management dashboard; there is no setting that turns redirect handling off, only `DisableRedirectUrlTracking` stops Umbraco from creating its own redirects; link to `extending/redirect-providers.md`.
- [ ] **Step 3:** `blocking.md`: pattern syntax with examples (`*` any run including `/`, trailing `/*` also matches the bare prefix, path only, case-insensitive, decoded once), status codes 403/404/410 in the dialog and 400 to 499 through the API, domain and culture scope, precedence, the safeguards (Umbraco-owned paths, `*`-only patterns, 800 characters), that saving deletes tracked URLs the rule covers and unblocking tracks again from 1, empty body and not tracked even when tracking is off, that blocking needs `UsePageNotFoundTracker()`.
- [ ] **Step 4:** Verify claims against a running `single-site` (add a rule through the dashboard or the API, check the 410 and the removed row). Run both check scripts. Commit (`docs: add editor guide, redirects and blocking pages`).

---

### Task 9: `extending/` pages

**Files:**
- Create: `docs/extending/README.md`, `not-found-page-resolver.md`, `database-providers.md`, `redirect-providers.md`, `dashboard-actions.md`

**Interfaces:**
- Consumes: the sample ids from Tasks 1 and 2.

- [ ] **Step 1:** `extending/README.md`: a table of every extension point (resolver, database provider, upsert override, redirect provider, redirect form, row actions on tracked URLs and block rules), what each replaces, the audience, and the page; the registration rule paragraph.
- [ ] **Step 2:** Write the four pages; each has: purpose, the contract with members, how the package uses it, a minimal example quoted with a sample comment from the matching ids, registration, and what is guaranteed and what is not. Content comes from `CLAUDE.md` verified against the code: resolver rules and singleton requirement; provider selection by Umbraco's provider name, last registration wins, migrations for all package tables and how the built-in projects and `scripts/add-migration.ps1` do it; the redirect payload, `CanHandle`, failure messages, what the package still does (delete of the tracked URL, access policy, endpoint), the `pnftRedirectForm` weight rule (default form -100) and the `getPayload` contract; the entity types `page-not-found-tracker-url` and `page-not-found-tracker-block-rule` and the weight/inline-button behaviour of entity actions.
- [ ] **Step 3:** Run both check scripts. Expected: pass. Confirm every `csharp` and `typescript` block on these pages is quoted.
- [ ] **Step 4:** Commit (`docs: add extension point pages`).

---

### Task 10: `troubleshooting.md`

**Files:**
- Create: `docs/troubleshooting.md`

- [ ] **Step 1:** Write symptom, cause, check, fix entries for: nothing in the dashboard (tracking off, flush interval, runtime level not `Run`, unknown database provider, request was blocked, hit was on the file path with a body); 404 page does not appear (alias, unpublished page, culture without a published variant, no domain match falls back to the first root, `UsePageNotFoundTracker()` missing or after `UseUmbraco()` so files are not handled, protected page); redirect created but not applied (file or query string row, unpublished or wrong-culture target, domain or culture mismatch, query string not honoured by Umbraco); block does not match (patterns match the relative path without the domain prefix, scope, normalisation, query string ignored, Umbraco-owned paths exempt, middleware missing); dashboard not visible (`DashboardUserGroups`, Content section access, restart needed). Each entry gives the concrete check (a log line, a URL, a setting).
- [ ] **Step 2:** Reproduce at least one entry per symptom on the test site and confirm the cause and fix are true. Run both check scripts. Commit (`docs: add troubleshooting guide`).

---

### Task 11: Index, linking, stale pages and `CLAUDE.md` rule

**Files:**
- Create: `docs/README.md`
- Modify: `README.md`, `src/assets/readme.md`, `umbraco-marketplace.json`, `CLAUDE.md`

- [ ] **Step 1:** `docs/README.md`: package summary, audiences, a table of every page by audience, supported versions. Run `check-doc-links.ps1`: every page in `docs/` must be reachable from it (extend the script with a failing test first: an orphan page under `docs/` fails with "not linked from docs/README.md").
- [ ] **Step 2:** `README.md`: remove the stale status block and "Planned features" list (the dashboard, redirects and blocking exist), list the real features, add a short "Documentation" section linking to `docs/README.md`. `src/assets/readme.md`: same corrections, add `DashboardUserGroups` to its settings table, keep it short, link to the docs on GitHub with absolute URLs (the NuGet page cannot resolve relative links). `umbraco-marketplace.json`: set `DocumentationUrl` to the URL in Global Constraints; validate the file as JSON.
- [ ] **Step 3:** `CLAUDE.md`: change the overview sentence that spec 7 is planned and not built to done and add the docs layout, the three CI checks and where the samples live; add the rule that a change to a setting, a contract, an extension point or documented behaviour must update the docs (and the quoted samples) in the same change. Keep the file's existing structure.
- [ ] **Step 4:** Run all checks (`check-doc-samples`, `check-doc-links`, the `ConfigurationDocsTests`). Commit (`docs: add index, refresh readmes and marketplace link, add maintenance rule`).

---

### Task 12: Manual verification on a clean site

**Files:** none committed (a scratch site outside the repo, under a short path such as `C:\pnft-verify`).

- [ ] **Step 1:** `dotnet pack -c Release -o <dir>` from `src/` (client build first), add `<dir>` as a local NuGet source with a `nuget.config` in the scratch site, create an Umbraco 17 site from the standard template, and follow only `docs/getting-started.md` on SQLite: install, call, 404 page, request a missing page and file, wait for the flush, see rows in the dashboard. Fix the guide wherever a step needed outside knowledge, then redo the run.
- [ ] **Step 2:** Repeat on SQL Server using the `sql-server` setup from `CLAUDE.md` (LocalDB `(localdb)\pnft-test`, or Docker). Record which was used.
- [ ] **Step 3:** In the scratch site, copy in the resolver and redirect provider samples exactly as the extension pages say and confirm each replaces the default behaviour (a different 404 page is chosen; a redirect created through the sample provider is recorded). Fix the pages if a step fails.
- [ ] **Step 4:** Keep the results (what ran, versions, database, anything not verified) in a draft file `pr-description.md` in the scratchpad directory, not in the repo; Task 13 uses it. Mark any part that could not run as unverified.

---

### Task 13: Whole-branch checks and PR description

- [ ] **Step 1:** From a clean state run the client `npm ci`, `npm test`, `npm run check-samples`, `npm run build`; from `src/` `dotnet build -c Release` (zero warnings), `dotnet test` (SQL Server cases per `CLAUDE.md`), `dotnet pack` then `check-package.ps1`; from the repo root both doc scripts and their self-tests, and `scripts/check-pending-model-changes.ps1`. Expected: all pass. Restore test-site incidental changes with `git checkout`.
- [ ] **Step 2:** Draft the PR description: summary, the acceptance checklist from issue #12 with evidence (install verification results from Task 12, scenario checks from Task 7), a "Differences from the specs and apparent defects" list (include the stale readmes found, the spec's "redirect handling disabled" cause that does not exist, and anything found while verifying), each defect filed as an issue. End with the attribution line.
- [ ] **Step 3:** Stop and ask the user before pushing or opening the pull request.
