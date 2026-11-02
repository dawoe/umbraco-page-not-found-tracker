# CLAUDE.md

Guidance for Claude Code when working in this repository.

## Project overview

`Umbraco.Community.PageNotFoundTracker` is an Umbraco 17 (LTS, .NET 10) package that records
requests that end in a 404 and, in later versions, lets editors turn tracked URLs into redirects
or block them. Built today: the foundation (spec 1: solution, build, test, pack, publish, a
placeholder backoffice dashboard, a test site that runs in four scenarios) and 404 handling
(spec 2: the configured 404 page is shown for missing pages and missing files). Nothing is
recorded yet: tracking, the dashboard, redirects and blocking (specs 3 to 7) are planned, not
built. Do not document or promise them as existing.

Specs (`docs/superpowers/specs/`, each has a matching GitHub issue; read the spec before planning):

1. Solution setup: `docs/superpowers/specs/2026-11-01-solution-setup-design.md` (done)
2. 404 handling for pages and files: `docs/superpowers/specs/2026-11-01-not-found-handling-design.md` (done)
3. Tracking of 404 URLs: `docs/superpowers/specs/2026-11-01-tracking-design.md`
4. Backoffice dashboard: `docs/superpowers/specs/2026-11-01-dashboard-design.md`
5. Redirect creation: `docs/superpowers/specs/2026-11-01-redirect-creation-design.md`
6. Blocked URLs: `docs/superpowers/specs/2026-11-01-blocked-urls-design.md`
7. Documentation for implementers: `docs/superpowers/specs/2026-11-01-documentation-design.md`

Plans (`docs/superpowers/plans/`):

- Solution setup: `docs/superpowers/plans/2026-11-01-solution-setup.md`
- 404 handling: `docs/superpowers/plans/2026-11-02-not-found-handling.md`

Branches: `develop` is the default and integration branch, `main` is the release branch. There is
no `v17/develop`. Feature branches are named `feature/<issue>-<slug>` and branch off `develop`.

## Solution layout

- `src/Umbraco.Community.PageNotFoundTracker.slnx` — solution (slnx format; includes the test site)
- `src/Directory.Build.props`, `src/Directory.Packages.props` — shared build/package metadata,
  central package management (Umbraco pinned to `[17.8.0,18.0.0)`), `packages.lock.json` per project
- `src/code/Umbraco.Community.PageNotFoundTracker/` — entry package, the only packable project.
  Holds `UsePageNotFoundTracker()` (`Extensions/WebApplicationExtensions.cs`)
- `src/code/Umbraco.Community.PageNotFoundTracker.Core/` — settings, models, interfaces only
- `src/code/Umbraco.Community.PageNotFoundTracker.Infrastructure/` — composer, middleware,
  services, persistence
- `src/code/Umbraco.Community.PageNotFoundTracker.BackOffice/` — Razor class library, later the
  Management API. Its `wwwroot/App_Plugins/PageNotFoundTracker` is the client build output (gitignored)
- `src/code/Umbraco.Community.PageNotFoundTracker.BackOffice.Client/` — backoffice client
  (TypeScript, Lit, Vite). Placeholder dashboard in `src/dashboards/placeholder/`
- `src/test/Umbraco.Community.PageNotFoundTracker.Infrastructure.Tests/` and `.BackOffice.Tests/` —
  NUnit + Moq unit tests
- `src/test/Umbraco.Community.PageNotFoundTracker.Integration.Tests/` — Umbraco integration tests
  (boots Umbraco on SQLite with the package composed in)
- `src/test/Umbraco.Community.PageNotFoundTracker.E2E/` — Playwright tests
- `test-sites/Website-V17/` — Umbraco 17 test site (Clean starter kit), project-references the
  entry package; in the solution, never packed (`IsPackable=false`)
- `src/assets/` — NuGet readme (`readme.md`) and icon (`logo.png`)
- `.github/workflows/` — `pr-validation.yml`, `beta-release.yml`, `release.yml`;
  `.github/scripts/check-package.ps1` asserts the packed nupkg is complete
- `.mcp.json` and `.vscode/mcp.json` — Umbraco MCP servers, one per launch profile
- `nuget.config` — nuget.org only (`<clear />`), so user-level feeds do not trigger NU1507 with
  central package management; CI uses nuget.org anyway
- `umbraco-marketplace.json` — Umbraco Marketplace listing

## Build and test

```bash
# Client (must run before dotnet build so App_Plugins exists)
cd src/code/Umbraco.Community.PageNotFoundTracker.BackOffice.Client
npm ci
npm run build

# .NET (all test projects: unit + integration)
cd src
dotnet restore
dotnet build
dotnet test

# Pack: from src/, WITHOUT --no-build (NETSDK1085 at solution level), then check the package
cd src
dotnet pack -c Release -o <output dir>
pwsh .github/scripts/check-package.ps1 -PackageDirectory <output dir>   # run from the repo root

# Test site (see launch profiles below); admin@example.com / 1234567890
cd test-sites/Website-V17
dotnet run --launch-profile single-site

# E2E tests (single-site must be running on https://localhost:44401)
cd src/test/Umbraco.Community.PageNotFoundTracker.E2E
cp .env.example .env                  # test-only values, already correct for single-site
npm ci && npx playwright install chromium
npm run test:e2e
```

Expected: zero analyzer warnings from `dotnet build`.

Packing facts:

- ONE nupkg contains everything: entry dll plus Core, Infrastructure and BackOffice dlls (and xml
  docs) in `lib/net10.0`, and the client bundle as `staticwebassets/App_Plugins/PageNotFoundTracker/*`.
  Only the entry project is packable.
- Pack fails loudly (`EnsureClientBundleBuiltBeforePack`) when the client bundle is missing: run
  the client build first. `check-package.ps1` additionally asserts exactly one nupkg and the five
  key files.
- The sibling projects are `PrivateAssets=all`, so their package references do not flow
  transitively. A new `PackageReference` in Infrastructure or BackOffice must be mirrored in the
  entry csproj (as `Umbraco.Cms.Api.Management` is) or consumers will miss it.
- Known gap: the sibling pdbs are not in the `snupkg`.
- The client bundle is a static web asset of the entry project, not of BackOffice (BackOffice has
  `StaticWebAssetsEnabled=false`). The entry csproj links the BackOffice `wwwroot/**` in as `Content` under
  `wwwroot/` with `ContentRoot` metadata pointing at the BackOffice `wwwroot`. That one item feeds both the
  project-reference case (the test site) and the pack manifest (`obj/<cfg>/net10.0/staticwebassets.pack.json`).
  Without the `ContentRoot` metadata the asset paths point at the entry project's own (non-existent) `wwwroot`
  and the site crashes on start; with `CopyToPublishDirectory=Never` the assets become Build-only and pack
  silently drops them; a `StaticWebAssetDiscoveryPattern` alone defines no assets, so nothing is packed.
- Verify pack changes in a fresh clone: a stale `staticwebassets.pack.json` in `obj/` can hide a broken setup.

## Launch profiles

Defined in `test-sites/Website-V17/Properties/launchSettings.json` (all `launchBrowser=false`,
`ASPNETCORE_ENVIRONMENT=Development`). Scenario differences live only here, no per-scenario
`appsettings` files. Every profile has its own port pair and its own database, so two profiles
can run side by side. Run with `dotnet run --launch-profile <name>` from `test-sites/Website-V17`.

| Profile | Shape | HTTPS / HTTP | Site URLs | Database (`umbraco/Data/`) |
|---|---|---|---|---|
| `single-site` | One site, one language | 44401 / 44301 | `https://localhost:44401/` | `single-site.sqlite.db` |
| `multi-site-aliases` | Two sites told apart by path (Site A, Site B) | 44402 / 44302 | `https://localhost:44402/siteA/...`, `/siteB/...` | `multi-site-aliases.sqlite.db` |
| `multi-language` | One site, en-US (default), nl-NL, fr-FR | 44403 / 44303 | `https://localhost:44403/en`, `/nl`, `/fr` | `multi-language.sqlite.db` |
| `multi-site-hostnames` | Two sites told apart by hostname | 44404 / 44304 | `https://www.siteA.local:44404/`, `https://www.siteB.local:44404/` | `multi-site-hostnames.sqlite.db` |

Backoffice: `https://localhost:<port>/umbraco`, login `admin@example.com` / `1234567890`
(unattended install creates this user; login is by email).

Scenario content: aliases and hostnames have two root nodes "Site A" and "Site B" (Site A is the
original Clean root, renamed; Site B is a copy with descendants), so the same path (`/about`) exists
on both. Umbraco domains are stored lowercase and include the profile's HTTPS port:
`localhost:44402/sitea`, `localhost:44402/siteb` (aliases); `www.sitea.local:44404`,
`www.siteb.local:44404` (hostnames); Home domains `localhost:44403/en`, `/nl`, `/fr` (language,
default en-US). The port is required because the profile ports are fixed and non-default, browsers
send it in the Host header, and Umbraco matches domains on host plus port: with port-less domains
pages still routed, but Umbraco rendered every internal link absolute and without the port
(`https://www.siteb.local/about/`), so clicking a link in a browser went to port 443 and failed. A Host
header without the port now matches no domain (falls back, or 404 for `/siteB/...`). Multi-language: Home, About and Features
vary by culture (`/en/about`, `/nl/over-ons`, `/fr/a-propos`, `/en/features`, `/nl/functies`,
`/fr/fonctionnalites`); other Clean pages are invariant and served under every culture domain.
Site and language markers are in the `<h1>` / `.subheading`, not in `<title>`.

Requests that match no domain fall back to Site A / en-US with 200 (standard Umbraco routing: in
aliases `/` and `/about` without a prefix, in hostnames `https://localhost:44404/about`, in
multi-language `/`). **404 tests must use paths that match no page.**

### Hostnames profile setup (manual, not scripted)

Kestrel cannot bind `www.siteA.local` and `www.siteB.local` as two `applicationUrl` entries (any
non-localhost name binds `https://[::]:44404` and the second collides). The profile therefore
binds `localhost:44404`; Kestrel does not filter on the Host header, so it answers for any name
that resolves to this machine. To use it:

1. Add this line to the hosts file (admin; Windows `C:\Windows\System32\drivers\etc\hosts`,
   Linux/macOS `/etc/hosts`): `127.0.0.1 www.siteA.local www.siteB.local`
2. Browsers warn about `.local` names with the default dev certificate (`dotnet dev-certs https
   --trust` covers `localhost` only). Click through, or use a certificate with SANs. Optional,
   PowerShell recipe (keep the pfx outside the repo and never commit it):

   ```powershell
   $cert = New-SelfSignedCertificate -DnsName localhost,www.siteA.local,www.siteB.local `
       -CertStoreLocation Cert:\CurrentUser\My -FriendlyName 'PageNotFoundTracker test'
   $pw = ConvertTo-SecureString 'test-only' -AsPlainText -Force
   Export-PfxCertificate -Cert $cert -FilePath C:\certs\pnft-test.pfx -Password $pw
   # Trust it (opens a Windows confirmation prompt):
   Import-PfxCertificate -FilePath C:\certs\pnft-test.pfx -CertStoreLocation Cert:\CurrentUser\Root -Password $pw
   # Start the profile from this same shell (not launchSettings.json):
   $env:Kestrel__Certificates__Default__Path = 'C:\certs\pnft-test.pfx'
   $env:Kestrel__Certificates__Default__Password = 'test-only'
   ```
3. Without editing hosts you can still test with `curl -k --resolve www.siteA.local:44404:127.0.0.1
   https://www.siteA.local:44404/about`.

Only `single-site` is verified in CI. The other profiles were verified manually.

## Committed databases (read this before running a profile)

Each profile's SQLite database is **committed** (`.gitattributes` marks `*.sqlite.db` binary), so a
clone starts with content, domains and languages already set up. A committed database is already
installed, so the unattended install settings only matter when a database is deleted.

**The databases contain test-only credentials. They are throwaway values that only work against
local throwaway databases on `localhost`. Never reuse them anywhere real:**

- Test admin user `admin@example.com` / `1234567890`.
- MCP API user `mcp@example.com` (kind `Api`, Administrators group), plus its OpenIddict client
  whose id and secret are the test-only values in `launchSettings.json` and `.mcp.json`.

Running a profile modifies the repo:

- The `.sqlite.db` changes (Umbraco writes runtime rows such as `umbracoServer`, which carry the
  machine name, and uses WAL mode: `-wal`/`-shm` files appear and are gitignored).
- Umbraco appends a generated `Imaging:HMACSecretKey` to the tracked `test-sites/Website-V17/appsettings.json`.

After local runs, restore both and never commit these incidental changes:

```bash
git checkout -- test-sites/Website-V17/appsettings.json test-sites/Website-V17/umbraco/Data/*.sqlite.db
```

Only commit a database deliberately (after a rebuild, see below), `git add` it by exact path.

### Rebuilding scenario databases

Needed after an Umbraco upgrade (databases must be re-run so `UpgradeUnattended` migrates them,
then re-committed deliberately) or when a database is deleted. Drive Umbraco through the MCP
servers in `.mcp.json` (Claude Code picks the file up; each profile has a server
`umbraco-<profile>`). Always look ids up rather than assuming them.

Per profile:

1. Delete `test-sites/Website-V17/umbraco/Data/<profile>.sqlite.db*` (skip for a pure upgrade).
2. `cd test-sites/Website-V17 && dotnet run --launch-profile <profile>`. Unattended install creates
   the admin and the Clean content; `TestSetup/McpApiUserComposer.cs` creates the MCP API user.
3. Wait for `https://localhost:<port>/umbraco/management/api/v1/server/status` to return 200.

Then, per scenario:

- `single-site`: nothing, check `/` and `/about` return 200.
- `multi-site-aliases` / `multi-site-hostnames`:
  1. `get-document-root` for the Home node (Site A).
  2. `copy-document` with `{"idToCopy": "<Home id>", "relateToOriginal": false, "includeDescendants": true}`
     and no `parentId` (content root); returns Site B's id, unpublished.
  3. For each site: `update-document-name` ("Site A"/"Site B"); `get-document-children` to find
     the `About` and `Blog` ids; `get-document-by-id` and point the `articleList` value inside
     `contentRows` at the site's own Blog (the copy still points at Site A's); then
     `update-document-properties` for title, subtitle and `contentRows`, and for the About subtitle.
  4. `put-document-domains` per site, e.g. `{"id": "<id>", "data": {"defaultIsoCode": null,
     "domains": [{"domainName": "localhost:44402/siteA", "isoCode": "en-US"}]}}` (hostnames:
     `www.siteA.local:44404` / `www.siteB.local:44404`). Always include the profile's HTTPS port in the
     name (Umbraco stores it lowercase).
  5. `publish-document-with-descendants` per site with
     `{"includeUnpublishedDescendants": true, "cultures": ["en-US"]}`.
  6. Check with `get-document-urls` and curl (`/siteA/about` and `/siteB/about`; hostnames via
     `curl -k --resolve`), each showing its own "All about Site X" subheading.
- `multi-language`:
  1. `get-default-language` (en-US stays default); `create-language` for `nl-NL` and `fr-FR`.
  2. `get-document-type-by-id` + `update-document-type` (send the full object without `id`): set
     `variesByCulture` on the `headerControls` composition (also its `title` and `subtitle`
     properties), on `home` and on `content`.
  3. For Home, About and Features: `get-document-by-id`, then `update-document` with three variants
     (en-US, nl-NL, fr-FR) and per-culture title/subtitle values (names listed in the scenario
     content above; About/Features leave `title` empty).
  4. `put-document-domains` on Home: `localhost:44403/en`, `localhost:44403/nl`, `localhost:44403/fr`,
     `defaultIsoCode` `en-US`.
  5. `publish-document-with-descendants` with all three cultures.
  6. Dictionary translations for nl-NL/fr-FR (so the `<title>` site name is not empty) are not in the
     configured MCP collections: use the Management API (`GET /umbraco/management/api/v1/dictionary`,
     then `GET`/`PUT /dictionary/{id}` with added translations; token via client credentials), or add
     `dictionary` to `UMBRACO_INCLUDE_TOOL_COLLECTIONS`.
  7. Check `/en`, `/nl`, `/fr` (h1 Welcome / Welkom / Bienvenue) and `/nl/over-ons`, `/fr/a-propos`.

Finish every profile:

1. Stop the site, confirm the port is free, `git checkout -- test-sites/Website-V17/appsettings.json`.
2. Fold the WAL into the main file and shrink it:
   `node -e "const {DatabaseSync}=require('node:sqlite');const d=new DatabaseSync('<db>');d.prepare('PRAGMA wal_checkpoint(TRUNCATE)').get();d.exec('VACUUM');d.close()"`
   (Node 24; `-wal`/`-shm` must be gone).
3. Remove machine-specific runtime rows (Umbraco recreates them on boot):
   `DELETE FROM umbracoServer; DELETE FROM umbracoCacheInstruction; DELETE FROM umbracoLastSynced;` then `VACUUM`.
4. Optionally boot a copy of the db to confirm it starts, then `git add` the exact `.sqlite.db` paths.

## Umbraco MCP

- Package: `@umbraco-cms/mcp-dev@17` (npm dist-tag `lts-17`; floats within 17.x, resolved 17.6.12
  when verified; npm `latest` is 18.x, so keep the `@17`). Docs: https://docs.umbraco.com/umbraco-developer-mcp
- `.mcp.json` (repo root, Claude Code) has four servers `umbraco-single-site`,
  `umbraco-multi-site-aliases`, `umbraco-multi-language`, `umbraco-multi-site-hostnames`.
  `.vscode/mcp.json` is a VS Code-format duplicate: **both files must be changed together.**
- Each server env: `UMBRACO_CLIENT_ID`, `UMBRACO_CLIENT_SECRET`, `UMBRACO_BASE_URL` (the profile's
  HTTPS URL), `NODE_TLS_REJECT_UNAUTHORIZED=0` (local dev only, because of the self-signed dev
  certificate; never copy it to anything real), and `UMBRACO_INCLUDE_TOOL_COLLECTIONS=document,document-type,data-type,media,language,culture,template`.
- The site seeds the matching API user itself: `test-sites/Website-V17/TestSetup/McpApiUserComposer.cs`
  (Development only) creates user `mcp@example.com` (Api kind, Administrators) and its client
  credentials on startup from `UmbracoMcp__ClientId` / `UmbracoMcp__ClientSecret` in each profile's
  environment. Those must match `.mcp.json`. It is idempotent and logs a warning instead of failing boot.
- Domain tools (`get-document-domains`, `put-document-domains`) are in the `document` collection;
  there is no `domain` collection.
- On native Windows, Claude Code may need `"command": "cmd", "args": ["/c", "npx", "-y", "@umbraco-cms/mcp-dev@17"]`
  locally. The committed file keeps plain `npx` to stay cross-platform. The config was verified with a
  scripted stdio client, not from a real Claude Code or VS Code session.
- The `init-umbraco-mcp` skill's advice to never commit secrets and use least privilege is deliberately
  overridden (spec decision): the values are test-only and live in throwaway databases.

## 404 handling (spec 2)

Setting `Umbraco:Community:PageNotFoundTracker:NotFoundPageDocumentTypeAlias` (`PageNotFoundTrackerSettings`,
`Constants.SectionName`). Package default is empty (no custom page, Umbraco's own 404 behaviour). The test
site sets `error` in `appsettings.json`; the integration tests use `notFoundPage`.

**Resolver.** `INotFoundPageResolver` (Core, public):
`Task<NotFoundPage?> ResolveAsync(NotFoundRequestContext context, Domain? domain, CancellationToken ct)`.
`NotFoundRequestContext(PathAndQuery, Host, Port?, IsFile)` has a `Path` property (no query);
`NotFoundPage(IPublishedContent Content, string? Culture)`. The caller matches the domain, the resolver
does not. Returning `null` means Umbraco's default behaviour applies. A consumer replaces it with
`AddSingleton<INotFoundPageResolver, X>()` in a composer; the package uses `TryAddSingleton` for the
resolver, `IRequestDomainMatcher` and `INotFoundRecorder`, so a consumer registration wins in either order
(covered by integration tests). The resolver must be singleton-compatible: the last-chance finder is a
singleton and holds it.

`DefaultNotFoundPageResolver` rules: root = the domain's content node, or the first root without a domain.
Alias lookup: direct children of the root first, then descendants (tree order); the first published
candidate wins (unpublished ones are skipped). Culture = the domain's culture, else the default language
(then `Culture` is null); an invariant page gets a null culture; a variant page without a published variant
in that culture counts as not found. Empty alias, no match or unpublished root gives `null`.

**Page path.** `PageNotFoundLastChanceFinder` (registered with `SetContentLastChanceFinder`, replacing
Umbraco's registration). It asks the resolver (exceptions are logged and treated as not resolved), sets the
published content, culture and 404 status, and keeps the URL. When nothing resolves it delegates to Umbraco's
`ContentFinderByConfigured404`, so the `Error404Collection` configuration still works. A site that registers
its own last-chance finder after the package replaces ours and drops this signal. Umbraco calls the finder a
second time for a page without a template; the once-per-request guard covers that.

**File path.** `FileNotFoundMiddleware`, added once by `UsePageNotFoundTracker()` (via the internal
`UseFileNotFoundHandling()`). It runs `next` first and acts when: GET or HEAD, last path segment has an
extension, the path is not Umbraco-owned and not under `/.well-known`, status is 404, the response has not
started and has no content type or length. So it replaces any bodyless 404 on an extension path, including a
site endpoint's `NotFound()`; a component that writes a body wins. File requests never reach Umbraco's
routing, so it builds a synthetic published request (`IUmbracoContextFactory.EnsureUmbracoContext`,
`IPublishedRouter`, `IUmbracoRouteValuesFactory`) and invokes the page's MVC action through
`IActionInvokerFactory` with status 404; the URL stays and HEAD gets headers without a body. It runs Umbraco's
public access check, so a protected 404 page shows the login page (status stays 404). `[Authorize]` on a
hijacked controller gives a plain 404 (no challenge). A render failure resets to a plain 404, never a 500.

**Umbraco-owned paths.** `UmbracoOwnedPaths` (Core, public static): `/umbraco`, `/App_Plugins`, `/_framework`
(segment boundary, case-insensitive), plus `IsFileRequest`. Spec 6 (blocking) is meant to reuse it; do not
duplicate the list.

**Recording call point (spec 3).** `INotFoundRecorder.RecordAsync(NotFoundOccurrence, ct)` (internal);
`NoOpNotFoundRecorder` is the default. `NotFoundOccurrence(PathAndQuery, Domain?, Culture?, IsFile, PageResolved)`.
Both paths call it once per request: `NotFoundRequestItems.TryMarkRecorded` (key in `HttpContext.Items`)
guards it. The middleware records before rendering, so a failed render is still `PageResolved=true`. Recorder
exceptions are logged and swallowed. The finder is a singleton: keep a recorder singleton-compatible or
resolve scoped work per request.

**Current behaviour to know.** A protected 404 page: the page path answers 200 with the login page (Umbraco
replaces the content), the file path answers 404 with the login page. An existing page with no template is
answered by the 404 page with 404 and recorded as missing (Umbraco's semantics). Requesting the 404 page's own
URL gives 200. Without a domain the file path renders the default language even if a custom resolver returns
another culture.

**Integration tests** (`Integration.Tests`, derive from `PageNotFoundTestBase` on `UmbracoTestServerTestBase`):
- Infrastructure grants `InternalsVisibleTo` to `Integration.Tests` (the props-wide IVT covers only
  `<Assembly>.Tests`) and to the entry project.
- A csproj target copies the test dll's `deps.json` as `Umbraco.Tests.Integration.deps.json`
  (`WebApplicationFactory` needs it).
- `LocalServerMessenger` plus the distributed-cache notification handlers make publishing refresh the
  navigation and published caches. The test server has no `WebApplication`, so
  `PageNotFoundTrackerStartupFilter` calls `UseFileNotFoundHandling()` before `UseUmbraco()`.
- The test server compiles no Razor: test document types render through hijacked render controllers
  (`TextPageController`, `NotFoundPageController`, ...). Real template rendering is covered by the E2E
  tests and manual checks, not by integration tests.
- `HttpContextRequestAppCache` is registered where culture rendering is tested (the base uses `NoCache`).
- Examine indexes land in `<test project>/umbraco/Data/TEMP/` (gitignored).

**Test site.** Clean's `error` document type (template `error`) already existed as a published `Error` page
under every Home root in all four databases, so no type was created. Titles: "Page not found" (single-site);
"Page not found - Site A/B" (aliases, hostnames, one Error per root); multi-language varies by culture
(en-US "Page not found", nl-NL "Pagina niet gevonden", fr-FR "Page introuvable", URLs `/en/error/`,
`/nl/fout/`, `/fr/erreur/`; the body text stays English). A 404 body is byte-identical to the Error page's
own URL in every profile. E2E (`tests/not-found.spec.ts`, single-site) covers page, file, query
preservation, `/about` 200 and the Management API; the other three profiles were verified manually.
Clean quirk, not ours: on alias and culture domains the canonical link doubles the prefix
(`/siteb/siteb/error/`), also on normal 200 pages; `<html lang="en">` is hardcoded.

## Tests

- Unit: NUnit + Moq, coverlet. One project per code project with logic.
- Integration (`Umbraco.Cms.Tests.Integration` 17.8.0): own `appsettings.Tests.json` (SQLite),
  `GlobalSetup.cs` in the global namespace. NUnit is pinned to 3.14.0 with `VersionOverride` in that
  project only (AutoFixture.NUnit3 needs NUnit 3.x); the central `Moq` 4.21.0 and
  `Microsoft.NET.Test.Sdk` 18.10.1 are what that package requires. Do not lower them.
- E2E: `@umbraco/playwright-testhelpers` 17.0.36, the correct helper for Umbraco 17 (the Umbraco 18
  helper is `@umbraco-cms/acceptance-test-helpers`; the API is the same, only the import differs).
  `tslib` is an explicit dev dependency because the helper needs it without declaring it. Copy
  `.env.example` to `.env` (gitignored). The site must be running on single-site (44401). Umbraco marks
  elements with `data-mark` (configured as `testIdAttribute`). The smoke test asserts the placeholder
  dashboard element `data-mark="page-not-found-tracker-placeholder"` at
  `/umbraco/section/settings/dashboard/page-not-found-tracker`.

## CI and repository settings

- `pr-validation.yml` (PRs to `develop`): client build, restore, build, test with coverage, pack and
  `check-package.ps1`, then E2E against `single-site` (dev cert without trust, `curl -k` wait loop).
  The Linux dev-cert/E2E step is unverified until the first PR run.
- `beta-release.yml` (push to `develop`): unit + integration tests, pack with
  `--version-suffix beta-<run number>`, push to MyGet feed `dawoe-umbraco`.
- `release.yml` (manual `workflow_dispatch`, runs only from `main`): build, test, pack, publish to
  NuGet.org through OIDC (`NuGet/login`), `--skip-duplicate`.
- After releasing a version from `main`, bump `VersionPrefix` (and `AssemblyVersion`) in
  `src/Directory.Build.props` on `develop`; otherwise betas such as `17.0.0-beta-N` sort below the
  released `17.0.0`.
- Known limitation: NuGet compares the prerelease label as a string, so `beta-10` sorts below `beta-9`
  and MyGet's "latest prerelease" is only reliable up to run 9 with `beta-<n>`. Changing the suffix to
  `beta.<n>` in `beta-release.yml` fixes it (left unchanged for the maintainer to decide).
- Secrets that must exist (maintainer creates them, never this repo): `MYGET_API_KEY` and `NUGET_USER`
  (NuGet.org username, plus a trusted publishing policy for this repository and `release.yml` on NuGet.org).
- Repository settings: default branch `develop`, release branch `main`.

## Coding conventions

### C#

- `src/.editorconfig` and `src/stylecop.json` are authoritative. Analyzer warnings must be fixed, not
  suppressed (zero warnings).
- Every file starts with the single-line StyleCop header
  `// Copyright (c) Dave Woestenborghs and contributors. Licensed under the MIT License. See LICENSE in the project root for license information.`
  (the `copyrightText` in `src/stylecop.json`).
- Usings outside the namespace, file-scoped namespaces, braces always.
- XML documentation on all public and internal members.
- Service registration through `IUmbracoBuilder` extension methods plus an `IComposer`.
- Layering: Core holds settings, models and interfaces only; behaviour goes in Infrastructure; the
  Management API in BackOffice; the entry project only wires.
- Version prefix `17.0.0` tracks the Umbraco major. Umbraco packages come only through central package
  management as `[17.8.0,18.0.0)`.

### Client

- Output goes to `BackOffice/wwwroot/App_Plugins/PageNotFoundTracker` (gitignored). The extension
  registers through `public/umbraco-package.json` and `src/entry.ts`.
- Interactive elements carry `data-mark` attributes for E2E selectors.
- No hard-coded dev-machine URLs in shipped client code.
- Node 24.x (`24.13.1` in CI).

## Gotchas

- On Windows keep the checkout path short (for example `C:\forks\...`). In a clone under a very long path
  (the harness scratch directory) the solution build failed with MSB3030 on `Core.dll` although each project built alone.
- The client build must run before `dotnet build`/`dotnet pack`; `App_Plugins` is gitignored.
- `UsePageNotFoundTracker()` goes after `BootUmbracoAsync()` and before `UseUmbraco()` in `Program.cs`
  (see the test site). It registers the file 404 middleware (once); placed after `UseUmbraco()` the
  middleware is inert. Without the call only the page path (last-chance finder) works.
- Project-reference use and packing of the client bundle both depend on the linked `Content` item with
  `ContentRoot` metadata in the entry csproj (see Packing facts); keep that metadata.
- Umbraco 17.8.0 strips `Cache=Shared` from the SQLite connection string on every start, so the profiles
  omit it.
- `clean` 7.0.8 is the Clean starter kit version that pairs with Umbraco 17; 8.x needs Umbraco 18.
- `test-sites/Website-V17/Views/_ViewImports.cshtml` was copied from the reference site because the Clean
  package does not ship it (without it `home.cshtml` fails to compile).
- The first start of a fresh database logs harmless "database not available" warnings before the
  unattended install creates it.
- `Umbraco.Cms` appends secrets to `appsettings.json` (see Committed databases); always revert.
- Scenario domains must include the profile's HTTPS port (`localhost:44402/sitea`, `www.sitea.local:44404`,
  `localhost:44403/en`). Without it pages still route, but Umbraco renders links absolute and without the
  port, so browser navigation breaks; a curl of a single page does not show this, check the rendered
  links. If a profile's port changes in `launchSettings.json`, update the domains in its database (and
  re-commit it deliberately).
- Deleted databases are recreated on the next run by the unattended settings (admin) and the MCP composer
  (API user), but the scenario content must be rebuilt (see Rebuilding scenario databases).
