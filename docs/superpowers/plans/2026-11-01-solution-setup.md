# Solution Setup Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** An empty but fully working `Umbraco.Community.PageNotFoundTracker` solution on Umbraco 17.x / .NET 10 that builds, tests, packs and publishes, plus a test site that runs in four scenarios.

**Architecture:** Layered projects (entry package, Core, Infrastructure, BackOffice, BackOffice.Client) copied in shape from `C:\forks\umbraco-pagespeed-optimization`; workflows, `CLAUDE.md` and E2E shape from `C:\forks\umbraco-search-examine-reindex`. One NuGet package (the entry project) carries all project output and the client bundle. The test site `test-sites/Website-V17` has four launch profiles, each with its own committed SQLite database and Umbraco MCP.

**Tech Stack:** .NET 10, Umbraco.Cms 17.x, NUnit + Moq + coverlet, Vite + Lit + TypeScript (Node 24.x), Playwright with `@umbraco/playwright-testhelpers`, GitHub Actions.

**Spec:** `docs/superpowers/specs/2026-11-01-solution-setup-design.md` (issue #1). Read it first. If a reference repo cannot be read, stop and ask.

## Global Constraints

- Package, namespace and assembly root: `Umbraco.Community.PageNotFoundTracker`. Version prefix `17.0.0`.
- `net10.0`, `Nullable` enabled, `RestorePackagesWithLockFile` true, `packages.lock.json` committed in every project.
- Umbraco.Cms packages pinned through central package management as `[17.x.y,18.0.0)` where `17.x.y` is the latest 17.x patch at build time.
- StyleCop.Analyzers enforced; `src/stylecop.json` and `src/.editorconfig` authoritative; warnings fixed, never suppressed. Copyright header on every C# file, usings outside the namespace, XML docs on public and internal members.
- Node 24.x (`24.13.1` as in the reference workflows) for client and E2E.
- Branches: `develop` default/integration, `main` release. No `v17/develop`.
- No 404 handling, tracking, persistence, `DbContext`, Management API controllers or generated TS client in this plan.
- No per-scenario `appsettings.*.json`; scenario differences only in `launchSettings.json`. No real credentials anywhere; test credentials are labelled as test-only.
- Database files: `test-sites/Website-V17/umbraco/Data/<profile>.sqlite.db`, connection string via `|DataDirectory|`, `.gitattributes` marks `*.sqlite.db` binary, `umbraco/Logs` and caches ignored.
- Profiles: `single-site` (`localhost`), `multi-site-aliases` (`localhost/siteA`, `localhost/siteB`), `multi-language` (`localhost/en`, `/nl`, `/fr`), `multi-site-hostnames` (`www.siteA.local`, `www.siteB.local`). Each has a distinct HTTPS port.
- NuGet metadata: author "Dave Woestenborghs and contributors", MIT, repo `https://github.com/dawoe/umbraco-page-not-found-tracker`, symbols `snupkg`, MyGet feed `dawoe-umbraco`.
- Commit messages end with the `Co-Authored-By` line given by the session. Work stays on `feature/1-solution-setup`; never commit secrets.

## Review Focus

1. Clean clone where the client was never built: `dotnet pack` must fail loudly, not silently produce a package without the bundle.
2. Running a profile dirties its `.sqlite.db` (and maybe appends a secret to `appsettings.json`): documented, and `git status` after a run is understood.
3. The same path (`/about`) exists on both sites in the aliases and hostnames scenarios and resolves to the right site.
4. Two profiles started on the same machine must not collide (ports) or share a database.
5. `UsePageNotFoundTracker()` is safe to call in any position of `Program.cs` and does nothing yet.

---

### Task 1: Repository scaffolding and shared build props

**Files:**
- Create: `.gitignore`, `.gitattributes`, `src/.editorconfig`, `src/stylecop.json`, `src/Directory.Build.props`, `src/Directory.Packages.props`, `src/test/Directory.Build.props`, `src/Umbraco.Community.PageNotFoundTracker.slnx`, `src/assets/readme.md`, `src/assets/logo.png`

**Interfaces:**
- Produces: shared props every later project inherits (`net10.0`, StyleCop, `InternalsVisibleTo $(AssemblyName).Tests` and `DynamicProxyGenAssembly2`, package metadata, `Umbraco.Cms.Web.Website` and `Umbraco.Cms.Core` references); central versions for Umbraco 17.x, StyleCop, NUnit, NUnit.Analyzers, NUnit3TestAdapter, Moq, coverlet.collector, coverlet.msbuild, Microsoft.NET.Test.Sdk.

- [ ] **Step 1:** Copy `src/.editorconfig`, `src/stylecop.json`, `src/test/Directory.Build.props`, `src/Directory.Build.props` and `src/Directory.Packages.props` from `umbraco-pagespeed-optimization`; replace names, description and URLs; set `AssemblyVersion`/`VersionPrefix`/`InformationalVersion` to `17.0.0`; change copyright text in `stylecop.json` only if the spec differs (it does not).
- [ ] **Step 2:** Resolve the latest 17.x Umbraco.Cms patch (`dotnet package search Umbraco.Cms --exact-match` or nuget.org) and pin `[17.x.y,18.0.0)` for `Umbraco.Cms.Web.Website`, `Umbraco.Cms.Core`, `Umbraco.Cms.Api.Management` and any other Umbraco package used. Drop the EF Core / ImageSharp entries; they belong to later specs.
- [ ] **Step 3:** `.gitignore`: bin/obj, `.vs/`, `TestResults/`, the client `node_modules`, `BackOffice/wwwroot/App_Plugins/PageNotFoundTracker/`, E2E `.auth/` and `.env`, `test-sites/Website-V17/umbraco/Logs`, `umbraco/Data/TEMP`, `umbraco/MediaCache`, `wwwroot/media`, `umbraco/models` (check the Umbraco 17 default `.gitignore` for the full list). `.gitattributes`: `*.sqlite.db binary`.
- [ ] **Step 4:** Create `src/assets/readme.md` (short NuGet readme: what the package will do, Umbraco 17 only, link to repo) and `logo.png` (copy the reference logo; note in the PR that it is a placeholder).
- [ ] **Step 5:** Create the `.slnx` with the build-props folders and an empty project list; `cd src && dotnet restore` succeeds.
- [ ] **Step 6:** Commit `chore: add repository scaffolding and shared build props`.

### Task 2: Code projects with composers and `UsePageNotFoundTracker`

**Files:**
- Create: `src/code/Umbraco.Community.PageNotFoundTracker.Core/*.csproj`, `.../Infrastructure/*.csproj` + `InfrastructureComposer.cs` + `Extensions/UmbracoBuilderExtensions.cs`, `.../BackOffice/*.csproj` + `BackOfficeComposer.cs` + `Constants.cs`, `.../Umbraco.Community.PageNotFoundTracker/*.csproj` + `Extensions/WebApplicationExtensions.cs`
- Create (tests): `src/test/Umbraco.Community.PageNotFoundTracker.Infrastructure.Tests/` and `.../BackOffice.Tests/` with csproj and tests below
- Modify: the `.slnx`

**Interfaces:**
- Produces: `public static void UsePageNotFoundTracker(this WebApplication app)` in namespace `Umbraco.Community.PageNotFoundTracker.Extensions` (verify name and shape against the reference `WebApplicationExtensions`); `internal sealed class InfrastructureComposer : IComposer` calling `builder.AddPageNotFoundTracker()`; `internal sealed class BackOfficeComposer : IComposer`; `internal static class Constants` in BackOffice with `ApiName = "page-not-found-tracker-management-api"` (unused until spec 4, keep out if it triggers warnings).
- References: entry → Infrastructure + BackOffice; Infrastructure → Core; BackOffice → Core. BackOffice is `Microsoft.NET.Sdk.Razor` with `StaticWebAssetBasePath` `/` and package reference `Umbraco.Cms.Api.Management`. Core and Infrastructure are plain SDK. Test projects reference the project under test.

- [ ] **Step 1: Write failing tests.**
  - `InfrastructureComposerTests.Compose_Registers_Without_Error`: build an `UmbracoBuilder` over an empty `ServiceCollection` as in the reference `InfrastructureComposerTests.Compose`, call `new InfrastructureComposer().Compose(builder)`, assert `BuildServiceProvider()` succeeds. (Registers nothing yet; spec 2 extends this test.)
  - `WebApplicationExtensionsTests.UsePageNotFoundTracker_Does_Not_Throw_And_Is_Idempotent`: create `WebApplication.CreateBuilder().Build()`, call `app.UsePageNotFoundTracker()` twice, assert no exception. Place it in an entry-level test location chosen by the builder (the Infrastructure.Tests project may reference the entry project, or add `Umbraco.Community.PageNotFoundTracker.Tests`; keep the project count to the spec's four test projects, so use Infrastructure.Tests).
  - `BackOfficeComposerTests.Compose_Registers_Without_Error`, same shape, in BackOffice.Tests.
- [ ] **Step 2:** `cd src && dotnet test`; expect FAIL (projects and types missing).
- [ ] **Step 3:** Create the projects and classes above, each with the copyright header and XML docs; `UsePageNotFoundTracker` has an empty body documented as the hook spec 2 fills. Add projects to the `.slnx`.
- [ ] **Step 4:** `cd src && dotnet restore && dotnet build && dotnet test`; expect PASS and zero analyzer warnings. Confirm each project has a committed `packages.lock.json`.
- [ ] **Step 5:** Commit `feat: add layered code projects and unit tests`.

### Task 3: BackOffice client project

**Files:**
- Create: `src/code/Umbraco.Community.PageNotFoundTracker.BackOffice.Client/{package.json,package-lock.json,tsconfig.json,vite.config.ts,.gitignore,public/umbraco-package.json,src/entry.ts,src/manifests.ts}`

**Interfaces:**
- Produces: `npm run build` emitting `entry-point.js` and `umbraco-package.json` into `../Umbraco.Community.PageNotFoundTracker.BackOffice/wwwroot/App_Plugins/PageNotFoundTracker`. The package manifest registers a `backofficeEntryPoint` with alias `Umbraco.Community.PageNotFoundTracker`, version `17.0.0`, js path `/App_Plugins/PageNotFoundTracker/entry-point.js?v=17.0.0` (relative path, no dev-machine URLs).
- Consumes: `@umbraco-cms/backoffice` version matching the pinned Umbraco 17.x.

- [ ] **Step 1:** Copy `package.json`, `tsconfig.json`, `vite.config.ts`, `.gitignore` from the pagespeed client; drop `generate-api`, `@hey-api/openapi-ts` and `openapi-ts.config.ts`; set `@umbraco-cms/backoffice` to the 17.x version, `outDir` to the path above, and the name to the PageNotFoundTracker one.
- [ ] **Step 2:** Implement the minimal extension (builder's choice, spec allows an empty bundle or placeholder dashboard). Prefer a placeholder dashboard in the Settings section with `data-mark="page-not-found-tracker-placeholder"` text so the E2E smoke test in Task 9 has something visible to assert. Entry point `onInit` registers the manifests.
- [ ] **Step 3:** `npm ci && npm run build`; expect success and the `App_Plugins/PageNotFoundTracker` folder with `entry-point.js` and `umbraco-package.json`; `git status` shows that folder ignored.
- [ ] **Step 4:** Commit `feat: add backoffice client with placeholder extension`.

### Task 4: Single NuGet package containing everything

**Files:**
- Modify: `src/code/Umbraco.Community.PageNotFoundTracker/*.csproj`, the other three code csprojs (`IsPackable` false), `src/Directory.Build.props` if needed

**Interfaces:**
- Produces: `dotnet pack` from `src/` yields exactly one `Umbraco.Community.PageNotFoundTracker.17.0.0.nupkg` (+ `.snupkg`) whose `lib/net10.0/` holds Core, Infrastructure, BackOffice and entry assemblies and whose static web assets include `App_Plugins/PageNotFoundTracker/entry-point.js`.

- [ ] **Step 1: Define the verification** (a documented command, not a script): `dotnet pack -c Release -o <scratch>` then list entries of the nupkg (`unzip -l` or `[IO.Compression.ZipFile]`); expected: one nupkg, the four assemblies, the `staticwebassets/App_Plugins/PageNotFoundTracker/entry-point.js` (path as produced by the Razor SDK), no dependency on sibling `Umbraco.Community.PageNotFoundTracker.*` packages in the nuspec.
- [ ] **Step 2:** Run it; expect FAIL (extra packages or missing assemblies).
- [ ] **Step 3:** Follow the reference repo's packing approach for the entry project but make the siblings non-packable and bundle their output (ProjectReference with `PrivateAssets=all` plus a `TargetsForTfmSpecificBuildOutput` target, or `IncludeReferencedProjects` equivalent). Add an MSBuild `Error` target before `Pack` that fails with a clear message when `BackOffice/wwwroot/App_Plugins/PageNotFoundTracker/entry-point.js` is missing (Review Focus 1).
- [ ] **Step 4:** Re-run the check; expect PASS. Delete the bundle folder, run `dotnet pack --no-build`; expect the guard error. Rebuild the client afterwards.
- [ ] **Step 5:** Commit `build: pack everything into one NuGet package`.

### Task 5: Test site base (Website-V17, Clean starter kit)

**Files:**
- Create: `test-sites/Website-V17/{Website-V17.csproj,Program.cs,appsettings.json,appsettings.Development.json,.gitignore,Properties/launchSettings.json}`
- Modify: the `.slnx`

**Interfaces:**
- Produces: a site that project-references the entry package, calls `app.UsePageNotFoundTracker()` after `app.BootUmbracoAsync()` and before `UseUmbraco()`, and runs on SQLite via `Microsoft.Data.Sqlite`.
- Consumes: `UsePageNotFoundTracker` (Task 2).

- [ ] **Step 1:** Base the csproj on the reference `Website-V17.csproj`: `Umbraco.Cms` and `Umbraco.Cms.DevelopmentMode.Backoffice` at the pinned 17.x version, `clean` starter kit 7.x (use the version compatible with that Umbraco patch), no EF Core design package, project reference to the entry package, and the Umbraco MCP-related package(s) the `init-umbraco-mcp` skill requires.
- [ ] **Step 2:** `Program.cs` as in the reference, plus `app.UsePageNotFoundTracker();`. `appsettings.json` holds only shared settings (Serilog, `Umbraco:CMS:Global:Id`, `Unattended:UpgradeUnattended`); nothing scenario specific.
- [ ] **Step 3:** Temporary default profile `single-site` only: `https://localhost:<port>`, `ASPNETCORE_ENVIRONMENT=Development`, `ConnectionStrings__umbracoDbDSN=Data Source=|DataDirectory|/single-site.sqlite.db;Cache=Shared;Foreign Keys=True;Pooling=True`, `ConnectionStrings__umbracoDbDSN_ProviderName=Microsoft.Data.Sqlite`, and `Umbraco__CMS__Unattended__InstallUnattended=true` with `UnattendedUserName`, `UnattendedUserEmail` (`admin@example.com`), `UnattendedUserPassword` (a test value, `1234567890` as in the reference) and `UnattendedUserNameLength` per Umbraco 17 settings.
- [ ] **Step 4:** `dotnet run --launch-profile single-site` from the test site (background); expect the Clean site at `/` and a working backoffice login. Stop it. Record in your notes whether Umbraco appended a secret to `appsettings.json` (Review Focus 2) and `git status`.
- [ ] **Step 5:** Commit `feat: add Website-V17 test site` (including the generated `single-site.sqlite.db` only after Task 7 decides the final committed state; for now leave it uncommitted).

### Task 6: Launch profiles for all four scenarios

**Files:**
- Modify: `test-sites/Website-V17/Properties/launchSettings.json`

**Interfaces:**
- Produces: profiles `single-site`, `multi-site-aliases`, `multi-language`, `multi-site-hostnames`, with these distinct HTTPS ports (builder may change if they collide on the machine, but must keep them distinct and record them in `CLAUDE.md`): 44401, 44402, 44403, 44404 (HTTP 44301..44304). `multi-site-hostnames` binds `https://www.siteA.local:44404;https://www.siteB.local:44404` through `applicationUrl`; if Kestrel needs `Kestrel__Endpoints__*` environment variables instead, use those and keep everything in the profile.

- [ ] **Step 1:** Add the three remaining profiles. Each differs from `single-site` only in ports, database file name (`<profile>.sqlite.db`) and the MCP variables from Task 8 (add those there, not here).
- [ ] **Step 2:** For each profile run `dotnet run --launch-profile <name>` in turn and confirm `/` answers (hostnames profile: add the hosts-file lines `127.0.0.1 www.siteA.local www.siteB.local` and trust the dev cert for those names, or if you cannot edit the hosts file, mark that profile as manually verified later and say so). Expect each creates its own `umbraco/Data/<profile>.sqlite.db` and none touches another.
- [ ] **Step 3:** Commit `feat: add launch profiles for the four scenarios`.

### Task 7: Umbraco MCP in every profile

**Files:**
- Modify: `test-sites/Website-V17/Properties/launchSettings.json`, the test site csproj
- Create: committed MCP client config (for example `.mcp.json` at the repo root or `test-sites/Website-V17/.mcp.json`, whichever `init-umbraco-mcp` generates)

**Interfaces:**
- Produces: per profile an API user (created through the backoffice or the skill's documented mechanism) whose client id and secret appear in that profile's `environmentVariables` and in the MCP client config, named so a client can pick the scenario (`umbraco-single-site`, `umbraco-multi-site-aliases`, ...). All values are labelled test-only.

- [ ] **Step 1:** Invoke the `umbraco:init-umbraco-mcp` skill against `test-sites/Website-V17` and follow what it generates for Umbraco 17; verify against current Umbraco docs instead of assuming. Name the exact MCP package in your notes for `CLAUDE.md`.
- [ ] **Step 2:** Run it for `single-site`, then repeat per profile with its own API user.
- [ ] **Step 3:** Verify: with a profile running, an MCP tool call (for example listing document types or domains) returns data. Expect a successful response for all four profiles. If the MCP server cannot be driven from this session, say so, document the manual verification steps and continue.
- [ ] **Step 4:** Commit `feat: configure Umbraco MCP for every launch profile`.

### Task 8: Scenario content and committed databases

**Files:**
- Modify: `test-sites/Website-V17/umbraco/Data/*.sqlite.db` (binary), `docs/` notes for CLAUDE.md

**Interfaces:**
- Produces: four committed databases with scenario content, and written setup steps so they can be rebuilt after an Umbraco upgrade.

- [ ] **Step 1 (aliases and hostnames):** through MCP (or the backoffice API), clone the Clean home tree into two roots "Site A" and "Site B" in each of `multi-site-aliases` and `multi-site-hostnames`; set domains (`localhost/siteA`, `localhost/siteB` and `www.siteA.local`, `www.siteB.local`); publish; ensure `/about` exists under both sites.
- [ ] **Step 2 (multi-language):** add English, Dutch, French; make the Clean document types vary by culture; create culture variants for content; set domains `localhost/en`, `/nl`, `/fr`; publish.
- [ ] **Step 3 (verify):** per profile, request `/siteA/about` and `/siteB/about` (aliases), `https://www.siteA.local:44404/about` and the B twin (hostnames), `/en`, `/nl`, `/fr` (multi-language); expect HTTP 200 with different content per site or language (Review Focus 3).
- [ ] **Step 4:** Stop the site, `git status`, commit only the four `.sqlite.db` files and any intended config. Record the exact steps taken under a "Rebuilding scenario databases" heading in your notes for Task 11.
- [ ] **Step 5:** Commit `feat: add committed scenario databases`.

### Task 9: Integration test project

**Files:**
- Create: `src/test/Umbraco.Community.PageNotFoundTracker.Integration.Tests/{*.csproj,PackageBootTests.cs}`
- Modify: the `.slnx`

**Interfaces:**
- Consumes: the Umbraco 17 integration testing documentation (<https://docs.umbraco.com/umbraco-cms/17.latest/develop-with-umbraco/testing-and-debugging/integration-testing>) for the base class and SQLite setup; the entry package project.

- [ ] **Step 1: Write the failing test** `PackageBootTests.Umbraco_Boots_With_Package_Composed_In`: boot Umbraco through the documented integration test base on SQLite, assert the host starts and an Infrastructure service or the composer registration is resolvable (for example that `IComposer` discovery included `InfrastructureComposer`, or that the app starts and `IRuntimeState.Level` is `Run`).
- [ ] **Step 2:** `dotnet test` on the project; expect FAIL (project missing / type not found).
- [ ] **Step 3:** Create the project following the doc; no database service required (works on Linux runners).
- [ ] **Step 4:** `cd src && dotnet test`; expect PASS for all test projects, zero analyzer warnings.
- [ ] **Step 5:** Commit `test: add integration test project`.

### Task 10: Playwright E2E project

**Files:**
- Create: `src/test/Umbraco.Community.PageNotFoundTracker.E2E/{package.json,package-lock.json,tsconfig.json,playwright.config.ts,.env.example,.gitignore,tests/auth.setup.ts,tests/smoke.spec.ts}`

**Interfaces:**
- Consumes: the `single-site` profile URL (`UMBRACO_URL` default `https://localhost:44401`), test admin credentials from `.env`, and the placeholder `data-mark` from Task 3.

- [ ] **Step 1: Write the failing test** `smoke.spec.ts`: `backoffice shows the Page Not Found Tracker placeholder` navigates to the Settings section and expects `getByTestId('page-not-found-tracker-placeholder')` visible (if the placeholder is not visible UI, assert instead that the site root and `/umbraco` load).
- [ ] **Step 2:** `npm ci && npx playwright install chromium && npm run test:e2e` with the site not running; expect FAIL.
- [ ] **Step 3:** Copy the reference E2E project shape (`playwright.config.ts`, `auth.setup.ts`) but use `@umbraco/playwright-testhelpers` (the Umbraco 17 helper) in place of `@umbraco-cms/acceptance-test-helpers`; check its `UiHelpers`/`ConstantHelper` import names. Keep `ignoreHTTPSErrors`, `testIdAttribute: 'data-mark'`, `workers: 1`.
- [ ] **Step 4:** Start `single-site`, run `npm run test:e2e`; expect setup + smoke PASS. Stop the site.
- [ ] **Step 5:** Commit `test: add Playwright E2E project`.

### Task 11: GitHub workflows

**Files:**
- Create: `.github/workflows/pr-validation.yml`, `.github/workflows/beta-release.yml`, `.github/workflows/release.yml`

**Interfaces:**
- Produces: PR validation (PRs to `develop`: client npm ci/build, restore, build, unit + integration tests with coverage, then E2E against `single-site`); beta release (push to `develop`: same build/test, `dotnet pack --version-suffix beta-${{github.run_number}}`, push to `https://www.myget.org/F/dawoe-umbraco/api/v3/index.json` with `MYGET_API_KEY`); release (`workflow_dispatch`, `if: github.ref == 'refs/heads/main'`, OIDC via `NuGet/login@v1` with `NUGET_USER`, `--skip-duplicate`).

- [ ] **Step 1:** Copy the three workflows from `umbraco-search-examine-reindex`, replace the client path and names; keep node `24.13.1`, dotnet `10.x`.
- [ ] **Step 2:** In PR validation add E2E steps: install E2E deps and Chromium, `dotnet dev-certs https --trust` (Linux: skip trust and rely on `ignoreHTTPSErrors`), start the site in the background with `--launch-profile single-site`, wait for the URL, run `npm run test:e2e`, upload traces on failure.
- [ ] **Step 3:** Validate YAML locally (`actionlint` if installed, otherwise parse with a YAML loader); expect no errors. Real validation is the PR run in Task 13.
- [ ] **Step 4:** Commit `ci: add PR validation, beta and release workflows`.

### Task 12: Documentation deliverables

**Files:**
- Create/Modify: `CLAUDE.md`, `README.md`, `src/assets/readme.md`, `umbraco-marketplace.json`

**Interfaces:**
- Consumes: notes from Tasks 5-8 (ports, MCP package, appsettings secret behaviour, rebuild steps).

- [ ] **Step 1:** `CLAUDE.md` in the style of the reference: overview, spec/plan links, solution layout, build/test commands (client first), launch profile table (name, shape, URL/ports, database file), conventions, gotchas. It must cover: `git checkout` of `.sqlite.db` files after local runs and not committing incidental changes; the databases contain test-only admin and MCP credentials; hosts-file entries and dev certificate for `www.siteA.local` / `www.siteB.local`; how Kestrel binds them; rebuilding scenario databases after an Umbraco upgrade; the exact MCP package name.
- [ ] **Step 2:** `README.md` and `src/assets/readme.md`: what the package does (records 404s, later redirects and blocks), Umbraco 17 only, installation including `app.UsePageNotFoundTracker()`, repository secrets and settings that must exist (`MYGET_API_KEY`, `NUGET_USER`), branch model.
- [ ] **Step 3:** `umbraco-marketplace.json` copied from the reference with title "Umbraco Page Not Found Tracker", tags (`404`, `redirects`, `seo`), documentation and issue tracker URLs for this repository.
- [ ] **Step 4:** A fresh-session check: re-read `CLAUDE.md` and run only what it says, from a clean clone in a scratch directory (`git clone` the branch), `npm ci && npm run build`, `dotnet restore`, `dotnet build`, `dotnet test`; expect all green with no analyzer warnings.
- [ ] **Step 5:** Commit `docs: add CLAUDE.md, README, nuget readme and marketplace file`.

### Task 13: Final verification and pull request

- [ ] **Step 1:** From the clean clone: run the issue's acceptance checklist in order (build/test, `dotnet pack` containing the client bundle, each profile starts, MCP responds, E2E passes on `single-site`). Note anything not verified (for example the hostnames profile or MCP if it could not be driven) in the PR description.
- [ ] **Step 2:** Push `feature/1-solution-setup` and open a PR to `develop` that closes #1, listing what was verified and what remains manual. Do not create repository secrets.
- [ ] **Step 3:** Check the PR validation run; fix failures on the branch.

---

## Self-review notes

- Spec coverage: layout (T1-T4), tests (T2, T9, T10), test site and four profiles (T5, T6), MCP (T7), scenario DBs (T8), workflows (T11), docs and metadata (T1, T12), acceptance (T13). The spec's `Directory.Build.props` package metadata is in T1.
- Open calls left to the builder, as the spec says: exact 17.x versions, packing mechanics (T4), integration base class (T9), MCP wiring (T7), Kestrel binding (T6), placeholder extension and smoke tests (T3, T10).
