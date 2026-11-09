# CLAUDE.md

Guidance for Claude Code when working in this repository.

## Project overview

`Umbraco.Community.PageNotFoundTracker` is an Umbraco 17 (LTS, .NET 10) package that records
requests that end in a 404 and, in later versions, lets editors turn tracked URLs into redirects
or block them. Built today: the foundation (spec 1: solution, build, test, pack, publish, a test site
that runs in four scenarios), 404 handling (spec 2: the configured 404 page is shown for missing pages
and missing files), tracking (spec 3: every 404 is counted per URL in a database table, SQLite or SQL
Server), the backoffice dashboard (spec 4: a searchable, sortable, filterable, paged list of tracked
URLs in the Content section, served by the package's own Management API) and redirect creation (spec 5: a
"Create redirect" row action that creates a redirect and removes the tracked URL; replaceable provider and form) and
blocked URLs (spec 6: wildcard block rules with a status code and a domain/culture scope, answered before Umbraco with an
empty body, never tracked; a "Block" row action and a second "Blocked URLs" dashboard tab).
Implementer documentation (spec 7: the public `docs/` set, compiled samples and three CI checks) is built; see
Documentation (spec 7) below. **A change to a setting, a contract, an extension point or documented behaviour must
update the docs (and the quoted samples) in the same change.**

Specs (`docs/superpowers/specs/`, each has a matching GitHub issue; read the spec before planning):

1. Solution setup: `docs/superpowers/specs/2026-11-01-solution-setup-design.md` (done)
2. 404 handling for pages and files: `docs/superpowers/specs/2026-11-01-not-found-handling-design.md` (done)
3. Tracking of 404 URLs: `docs/superpowers/specs/2026-11-01-tracking-design.md` (done)
4. Backoffice dashboard: `docs/superpowers/specs/2026-11-01-dashboard-design.md` (done)
5. Redirect creation: `docs/superpowers/specs/2026-11-01-redirect-creation-design.md` (done)
6. Blocked URLs: `docs/superpowers/specs/2026-11-01-blocked-urls-design.md` (done)
7. Documentation for implementers: `docs/superpowers/specs/2026-11-01-documentation-design.md` (done)

Plans (`docs/superpowers/plans/`):

- Solution setup: `docs/superpowers/plans/2026-11-01-solution-setup.md`
- 404 handling: `docs/superpowers/plans/2026-11-02-not-found-handling.md`
- Tracking: `docs/superpowers/plans/2026-11-03-tracking.md`
- Dashboard: `docs/superpowers/plans/2026-11-03-dashboard.md`
- Redirect creation: `docs/superpowers/plans/2026-11-04-redirect-creation.md`
- Blocked URLs: `docs/superpowers/plans/2026-11-04-blocked-urls.md`
- Documentation: `docs/superpowers/plans/2026-11-09-documentation.md`

Branches: `develop` is the default and integration branch, `main` is the release branch. There is
no `v17/develop`. Feature branches are named `feature/<issue>-<slug>` and branch off `develop`.

## Solution layout

- `src/Umbraco.Community.PageNotFoundTracker.slnx` — solution (slnx format; includes the test site)
- `src/Directory.Build.props`, `src/Directory.Packages.props` — shared build/package metadata,
  central package management (Umbraco pinned to `[17.8.0,18.0.0)`), `packages.lock.json` per project
- `src/code/Umbraco.Community.PageNotFoundTracker/` — entry package, the only packable project.
  Holds `UsePageNotFoundTracker()` (`Extensions/WebApplicationExtensions.cs`)
- `src/code/Umbraco.Community.PageNotFoundTracker.Core/` — settings, models, interfaces only (block rule
  model and contracts in `Blocking/`)
- `src/code/Umbraco.Community.PageNotFoundTracker.Infrastructure/` — composer, middleware,
  services, the tracking `DbContext`, queue, flush and migration services, the dashboard access rule
  and the tracked URL query service; `Blocking/` holds pattern matching, the rule snapshot and its provider, the block
  middleware, the rule service and the cache refresher
- `src/code/Umbraco.Community.PageNotFoundTracker.Persistence.Sqlite/` and `.Persistence.SqlServer/` —
  tracking database providers, each with its own EF Core `Migrations/` and a design-time factory
  (`IsPackable=false`, bundled into the entry package)
- `src/code/Umbraco.Community.PageNotFoundTracker.BackOffice/` — Razor class library with the
  Management API (controllers, response models, the dashboard access policy, the OpenAPI document). Its
  `wwwroot/App_Plugins/PageNotFoundTracker` is the client build output (gitignored)
- `src/code/Umbraco.Community.PageNotFoundTracker.BackOffice.Client/` — backoffice client
  (TypeScript, Lit, Vite, Vitest): the generated API client (`src/api/`), the tracked URL collection
  (`src/collection/`), the dashboard (`src/dashboards/tracked-urls/`), the access condition
  (`src/conditions/`), the create redirect flow (`src/redirect/`: form extension type, modal, default form, row action), the
  blocked URLs pieces (`src/blocking/`: rule collection and dashboard, Block/Unblock actions, Add rule, rule modal) and the
  English texts (`src/localization/`). Shared paged-collection bases live in `src/collection/`. `src/client-ready.ts` lives outside `src/api/` on purpose (see Redirects). The spec 1
  placeholder dashboard is gone
- `src/test/Umbraco.Community.PageNotFoundTracker.Infrastructure.Tests/` and `.BackOffice.Tests/` —
  NUnit + Moq unit tests (blocking tests in `Infrastructure.Tests/Blocking/`, `BackOffice.Tests/BlockRuleControllersTests.cs`)
- `src/test/Umbraco.Community.PageNotFoundTracker.Integration.Tests/` — Umbraco integration tests
  (boots Umbraco on SQLite with the package composed in; tracking database tests on SQLite and SQL Server; blocking in
  `Blocking/` and `ManagementApi/BlockRulesApiTests.cs`)
- `src/test/Umbraco.Community.PageNotFoundTracker.E2E/` — Playwright tests (`tests/not-found.spec.ts`,
  `tests/dashboard.spec.ts`, `tests/redirect.spec.ts`, `tests/blocked.spec.ts`)
- `test-sites/Website-V17/` — Umbraco 17 test site (Clean starter kit), project-references the
  entry package, both persistence projects and BackOffice (the entry project references them `PrivateAssets=all`
  and never uses them in code, so a project-referencing site would not get their dlls: tracking would be off and
  the Management API missing); in the solution, never packed (`IsPackable=false`)
- `scripts/` — `add-migration.ps1` and `check-pending-model-changes.ps1` (tracking migrations);
  `.config/dotnet-tools.json` pins `dotnet-ef` 10.0.12 (the scripts run `dotnet tool restore`)
- `src/assets/` — NuGet readme (`readme.md`) and icon (`logo.png`)
- `docs/` — the public documentation (spec 7), `docs/extending/` for the extension points; `docs/superpowers/` holds the
  internal specs and plans and is not public documentation
- `src/samples/Umbraco.Community.PageNotFoundTracker.Samples/` — compiled C# samples quoted by `docs/extending`
  (`IsPackable=false`, in the solution, not referenced by the test site); the TypeScript samples are in the client's
  `samples/` folder, type-checked only (`npm run check-samples`), never bundled
- `.github/workflows/` — `pr-validation.yml`, `beta-release.yml`, `release.yml`;
  `.github/scripts/check-package.ps1` asserts the packed nupkg is complete; `check-doc-samples.ps1` and
  `check-doc-links.ps1` check the docs (each has a self-test in `.github/scripts/tests/`)
- `.mcp.json` and `.vscode/mcp.json` — Umbraco MCP servers, one per launch profile
- `nuget.config` — nuget.org only (`<clear />`), so user-level feeds do not trigger NU1507 with
  central package management; CI uses nuget.org anyway
- `umbraco-marketplace.json` — Umbraco Marketplace listing

## Build and test

```bash
# Client (must run before dotnet build so App_Plugins exists)
cd src/code/Umbraco.Community.PageNotFoundTracker.BackOffice.Client
npm ci
npm test        # Vitest, pure client logic (query mapping, states, filter choices, columns, access check)
npm run build

# .NET (all test projects: unit + integration)
cd src
dotnet restore
dotnet build
dotnet test     # SQL Server tracking tests are skipped unless PNFT_SQLSERVER_CONNECTION is set (see Tracking)

# Migrations (from the repo root): both providers have no pending model changes
pwsh scripts/check-pending-model-changes.ps1

# Pack: from src/, WITHOUT --no-build (NETSDK1085 at solution level), then check the package
cd src
dotnet pack -c Release -o <output dir>
pwsh .github/scripts/check-package.ps1 -PackageDirectory <output dir>   # run from the repo root

# Test site (see launch profiles below); admin@example.com / 1234567890
cd test-sites/Website-V17
dotnet run --launch-profile single-site

# E2E tests (single-site must be running on https://localhost:44401, started with a short flush
# interval so the dashboard tests see their 404s quickly; CI does the same):
#   Umbraco__Community__PageNotFoundTracker__FlushInterval=00:00:01 dotnet run --launch-profile single-site
cd src/test/Umbraco.Community.PageNotFoundTracker.E2E
cp .env.example .env                  # test-only values, already correct for single-site
npm ci && npx playwright install chromium
npm run test:e2e
```

Expected: zero analyzer warnings from `dotnet build`.

Documentation checks (from the repo root; CI runs the first two in `pr-validation.yml`, the third is a unit test):

```bash
pwsh .github/scripts/check-doc-samples.ps1     # every csharp/typescript block in docs/ matches its compiled sample
pwsh .github/scripts/check-doc-links.ps1       # relative links, anchors, every page reachable from docs/README.md
dotnet test src/test/Umbraco.Community.PageNotFoundTracker.Infrastructure.Tests --filter ConfigurationDocsTests
(cd src/code/Umbraco.Community.PageNotFoundTracker.BackOffice.Client && npm run check-samples)   # TypeScript samples
```

Packing facts:

- ONE nupkg contains everything: entry dll plus Core, Infrastructure, BackOffice, Persistence.Sqlite and
  Persistence.SqlServer dlls (and xml docs) in `lib/net10.0`, and the client bundle as
  `staticwebassets/App_Plugins/PageNotFoundTracker/*`. Only the entry project is packable.
- Pack fails loudly (`EnsureClientBundleBuiltBeforePack`) when the client bundle is missing: run
  the client build first. `check-package.ps1` additionally asserts exactly one nupkg and the seven
  key files (six dlls and `entry-point.js`), and fails when any file of the client bundle (source maps
  included) contains `localhost` (no dev-machine server URL may ship, see Dashboard).
- The sibling projects are `PrivateAssets=all`, so their package references do not flow
  transitively. A new `PackageReference` in any sibling project (Core, Infrastructure, BackOffice, a
  persistence project) must be mirrored in the entry csproj (as `Umbraco.Cms.Api.Management` and `Umbraco.Cms.Persistence.EFCore` are) or
  consumers will miss it. `Microsoft.EntityFrameworkCore.Design` is `PrivateAssets=all` (migrations tooling only)
  and is not mirrored.
- Known gap: the sibling pdbs are not in the `snupkg`.
- The client bundle is a static web asset of the entry project, not of BackOffice (BackOffice has
  `StaticWebAssetsEnabled=false`). The entry csproj links the BackOffice `wwwroot/**` in as `Content` under
  `wwwroot/` with `ContentRoot` metadata pointing at the BackOffice `wwwroot`. That one item feeds both the
  project-reference case (the test site) and the pack manifest (`obj/<cfg>/net10.0/staticwebassets.pack.json`).
  Without the `ContentRoot` metadata the asset paths point at the entry project's own (non-existent) `wwwroot`
  and the site crashes on start; with `CopyToPublishDirectory=Never` the assets become Build-only and pack
  silently drops them; a `StaticWebAssetDiscoveryPattern` alone defines no assets, so nothing is packed.
- Controllers in the sibling dlls are found by a NuGet consumer without extra code: the consumer's build generates an
  `ApplicationPart` attribute for every package dll that references MVC. Checked once (spec 4) with a minimal Umbraco
  17.8 site that referenced the packed nupkg: the attributes listed BackOffice, the API routes answered 401 (not 404)
  anonymously, and the swagger document and `entry-point.js` were served. Build such a check site under a short path
  (the long-path gotcha below drops package dlls from `ReferencePath` silently).
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
| `sql-server` | One site, one language, SQL Server | 44405 / 44305 | `https://localhost:44405/` | SQL Server `PageNotFoundTracker` on LocalDB `(localdb)\pnft-test` (not committed) |

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

### SQL Server profile setup

`sql-server` proves the package on SQL Server (tracking, migrations). The profile uses a LocalDB named instance, so it
needs no Docker (Windows only: `Integrated Security`). Its connection string is
`Server=(localdb)\pnft-test;Database=PageNotFoundTracker;Integrated Security=True;TrustServerCertificate=True`
(provider `Microsoft.Data.SqlClient`). It has no MCP user, no committed database and no scenario content beyond what
the Clean install creates (tracking needs no 404 page). Create the instance once, then run the profile:

```powershell
sqllocaldb versions                      # pick a version Microsoft.Data.SqlClient can start (see below)
sqllocaldb create pnft-test 15.0 -s      # -s also starts it; an existing instance starts on first connect
dotnet run --launch-profile sql-server
# afterwards: sqlcmd -S "(localdb)\pnft-test" -Q "ALTER DATABASE [PageNotFoundTracker] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [PageNotFoundTracker]"
```

The first start logs "Cannot open database" / "database not available" errors until the unattended install creates the
`PageNotFoundTracker` database, installs Umbraco and Clean; then the package migrates its tables. If the log instead
shows "TCP Provider ... timed out" the profile is pointing at a SQL Server that is not running (an old Docker connection
string): check the connection string in `launchSettings.json`.

To use a SQL Server in Docker (or on Linux/macOS) instead, start it and override the connection string with a
command-line argument. `launchSettings.json` environment variables override shell variables in `dotnet run`, so a
command-line argument (highest configuration precedence) is the way, not `$env:`:

```bash
docker run -d --name pnft-sqlserver -e ACCEPT_EULA=Y -e 'MSSQL_SA_PASSWORD=PnftTest-only1!' \
  -p 14330:1433 mcr.microsoft.com/mssql/server:2022-latest   # test-only password, never reuse it
dotnet run --launch-profile sql-server -- "--ConnectionStrings:umbracoDbDSN=Server=localhost,14330;Database=PageNotFoundTracker;User Id=sa;Password=PnftTest-only1!;TrustServerCertificate=True"
```

Reset the container with `docker rm -f pnft-sqlserver`.

Pin the LocalDB version. Without it `sqllocaldb create` uses the newest installed version. On a machine with
LocalDB 2019 (15.0) and 2025 (17.0) installed, an instance created with 17.0 accepted `sqlcmd` connections, but the site
(`Microsoft.Data.SqlClient`) failed on start with "error: 50 - Local Database Runtime error occurred ... SQL Server
process failed to start". Recreating the instance with `15.0` fixed it. `sqllocaldb info pnft-test` shows an existing
instance's version. The same applies to the instance used for `PNFT_SQLSERVER_CONNECTION` (see Tracking).

Use a named instance, not the shared `MSSQLLocalDB` or a full local SQL Server. Running the profile still rewrites
`appsettings.json` (HMAC key): restore it with `git checkout`.

Only `single-site` is verified in CI. The other profiles were verified manually (`sql-server` against LocalDB, the
profile default; not against the Docker container).

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
`builder.Services.AddUnique<INotFoundPageResolver, X>()` in a composer marked
`[ComposeAfter(typeof(PageNotFoundTrackerComposer))]` (see Composer order below). The resolver must be
singleton-compatible: the last-chance finder is a singleton and holds it.

**Composer order.** `PageNotFoundTrackerComposer` (Core, public sealed) registers nothing; it is the anchor. Every package
composer (`InfrastructureComposer`, `BackOfficeComposer`, `SqlitePersistenceComposer`, `SqlServerPersistenceComposer`,
all internal) is marked `[ComposeBefore(typeof(PageNotFoundTrackerComposer))]`, so all package registrations exist
before it runs. Replaceable services (`INotFoundPageResolver`, `IRequestDomainMatcher`, the internal `INotFoundRecorder`)
use Umbraco's `AddUnique` (last registration wins), not `TryAdd`; database providers use `AddSingleton` and the
selector picks the last one per name. A consumer composer marked `[ComposeAfter(typeof(PageNotFoundTrackerComposer))]`
therefore replaces any of them. A registration made **before** the package composers is overwritten by `AddUnique`.
`TimeProvider` stays `TryAddSingleton` (a framework service the site may already register). New package composers must
carry the same `[ComposeBefore]`. Tests: `ComposerOrderTests` (unit: attributes, AddUnique; integration: a probe composer
in the test assembly runs after the anchor in the real composition), `NotFoundResolverReplacement*Tests`. Implementer
documentation of this rule belongs to spec 7.

`DefaultNotFoundPageResolver` rules: root = the domain's content node, or the first root without a domain.
Alias lookup: direct children of the root first, then descendants (tree order); the first published
candidate wins (unpublished ones are skipped). Culture = the domain's culture, else the default language
(then `Culture` is null); an invariant page gets a null culture; a variant page without a published variant
in that culture counts as not found. Empty alias, no match or unpublished root gives `null`.

**Page path.** `PageNotFoundLastChanceFinder` (registered with `SetContentLastChanceFinder`, replacing
Umbraco's registration). It asks the resolver (exceptions are logged and treated as not resolved), sets the
published content, culture and 404 status, and keeps the URL. When nothing resolves it delegates to Umbraco's
`ContentFinderByConfigured404`, so the `Error404Collection` configuration still works. Registration order:
`SetContentLastChanceFinder` (`AddUnique`) overwrites whatever last-chance finder another composer registered
before the package, and the fallback is always `ContentFinderByConfigured404`, never that earlier finder; a site
that registers its own last-chance finder after the package replaces ours and drops this signal. Umbraco calls
the finder a second time for a page without a template; the once-per-request guard covers that. When a page
resolved, the finder also registers one `Response.OnStarting` callback that turns a 200 into a 404 (Umbraco's
public-access rewrite drops the 404 status, see below); other statuses are untouched. A client abort while
resolving is logged at debug and falls back to Umbraco's finder. `[Authorize]` on a hijacked 404 controller
gives Umbraco's challenge (302/401) for anonymous visitors on the page path, whereas the file path answers a
plain 404.

**File path.** `FileNotFoundMiddleware`, added once by `UsePageNotFoundTracker()` (via the internal
`UseFileNotFoundHandling()`; the block middleware is added next to it, see Blocked URLs, Middleware placement). It runs `next` first and acts when: GET or HEAD, last path segment has an
extension, the path is not Umbraco-owned and not under `/.well-known`, status is 404, the response has not
started and has no content type or length. So it replaces any bodyless 404 on an extension path, including a
site endpoint's `NotFound()`; a component that writes a body wins. File requests never reach Umbraco's
routing, so it builds a synthetic published request (`IUmbracoContextFactory.EnsureUmbracoContext`,
`IPublishedRouter`, `IUmbracoRouteValuesFactory`) and invokes the page's MVC action through
`IActionInvokerFactory` with status 404; the URL stays and HEAD gets headers without a body. It runs Umbraco's
public access check, so a protected 404 page shows the login page (status stays 404). `[Authorize]` on a
hijacked controller gives a plain 404 (no challenge). A render failure resets to a plain 404, never a 500.
For the render it sets `CultureInfo.CurrentCulture`/`CurrentUICulture` to the page's culture (else the published
request's, else the default language) and restores them: on the page path Umbraco's request localization does
that inside the pipeline, which does not flow out to the middleware. Without it a Linux CI runner (no `LANG`, process
culture invariant) failed every dictionary lookup in the template (`GetLanguageByIsoCode("")`) and fell back to the
plain 404 while Windows passed; the regression test uses `new CultureInfo("")`, because Umbraco's culture
dictionary stops at the `CultureInfo.InvariantCulture` instance by reference, so neither that instance nor Windows
`DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1` reproduces it.

**Umbraco-owned paths.** `UmbracoOwnedPaths` (Core, public static): `/umbraco`, `/App_Plugins`, `/_framework`
(segment boundary, case-insensitive), plus `IsFileRequest`. Spec 6 (blocking) reuses it for the rule validator and the
runtime exemption; do not duplicate the list.

**Recording call point (spec 3).** `INotFoundRecorder.RecordAsync(NotFoundOccurrence, ct)` (internal);
`NotFoundTracker` (spec 3, see Tracking) is the default. `NotFoundOccurrence(PathAndQuery, Domain?, Culture?, IsFile, PageResolved)`.
Both paths call it once per request: `NotFoundRequestItems.TryMarkRecorded` (key in `HttpContext.Items`)
guards it. The middleware records before rendering, so a failed render is still `PageResolved=true`. Recorder
exceptions are logged and swallowed. The finder is a singleton: keep a recorder singleton-compatible or
resolve scoped work per request.

**Current behaviour to know.** A protected 404 page: both paths answer 404 with the protection's login page
(on the page path Umbraco's public-access rewrite builds a new published request that drops the 404 status,
and the finder's `OnStarting` callback restores it). An existing page with no template is answered by the 404
page with 404 and recorded as missing (Umbraco's semantics). Requesting the 404 page's own URL gives 200.
Without a domain the file path renders the resolver's culture variant, but `PublishedRequest.Culture` stays
Umbraco's default language (anything reading the request culture rather than the variation context sees the
default).

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

## Tracking (spec 3)

**Settings** (`PageNotFoundTrackerSettings`, section `Umbraco:Community:PageNotFoundTracker`, read once at startup,
a change needs a restart):

| Setting | Default | Notes |
|---|---|---|
| `TrackingEnabled` | `true` | Off: nothing is queued and the flush service does nothing. Migrations still run. |
| `FlushInterval` | `00:01:00` | Minimum 1 s; zero or negative means the default (`EffectiveFlushInterval`). |
| `IncludeQueryString` | `false` | On: the query string is part of the identity (parameter order not normalised). |
| `ChannelCapacity` | `10000` | Queue size and the bound of the pending (merge + retry) buffer; below 1 means the default. |
| `DashboardUserGroups` | empty (= `admin`, `editor`) | User group aliases that may use the dashboard and its API; `admin` always passes. See Dashboard (spec 4). |

**Flow.** Both 404 call points (spec 2) call `INotFoundRecorder`; `NotFoundTracker` (internal, `AddUnique`)
normalises the occurrence with `TrackedUrlNormalizer` and writes it to `TrackingQueue` without awaiting and without
throwing. It does nothing when tracking is off or the persistence state is `Unavailable`. `TrackingQueue` is a bounded
channel: when full the new hit is dropped, with at most one warning per minute stating the number dropped (the drop
counter is interlocked and the warning time a compare-and-swap, so request threads never take the queue lock). The hosted
`TrackingFlushService` (also `ITrackingFlusher`, which integration tests use to force a flush) waits for the startup
gate, then every `FlushInterval` drains the channel into a pending buffer merged per identity (count summed, earliest
`FirstSeen`, latest `LastSeen`, first-seen `DisplayUrl` and `IsFile`) and writes it. Hits not written stay pending for
the next flush; when they bring the pending buffer past `ChannelCapacity` identities, the entries with the oldest
`LastSeen` are dropped (with a warning). Pending buffer, retry and in-flight batch share one lock (a later discard can take it). `StopAsync` flushes
once more within the host's shutdown time; whatever is still queued after it (cancelled or timed out at the shutdown
deadline, or a failed write) is lost and logged as one warning with the count. No database write happens in the request pipeline.

**Identity** = `UrlHash` + `Domain` + `Culture` (unique index). `Url` is the path as the call point passes it, already
decoded by Kestrel (everything but `%2F`; the finder branch without an `HttpContext` decodes `Uri.AbsolutePath` the same
way with `PathString.FromUriComponent`). The normaliser does **not** decode again (that merged `/a%2541` with `/aA`). It is
lower-cased (invariant),
repeated and trailing slashes removed, with the full alias prefix (`/sitea/...`); with `IncludeQueryString` the query
is appended lower-cased but **not** percent-decoded. `UrlHash` is the SHA-256 (hex) of the full `Url` before
truncation, so long URLs stay distinct. `DisplayUrl` is the first-seen spelling (without the query when
`IncludeQueryString` is off). `RelativePath` is `Url`'s path with the domain's own path removed (indexed, not unique).
`Domain` is the matched domain name lower-cased, `Culture` as Umbraco returns it; both are an empty string (never null)
without a match. Truncation: `Url`/`DisplayUrl` 2000, `RelativePath` 800, `Domain` 255, `Culture` 50.

**Data model.** Table `pageNotFoundTrackerUrl` (`TrackingConstants.TableName`, entity `TrackedUrl` in Core):
`Id`, `Url`, `UrlHash`, `RelativePath`, `DisplayUrl`, `Domain`, `Culture`, `IsFile`, `HitCount` (`long`, `bigint` on
SQL Server; `TrackedUrlHit.Count` is `long` too, so merged counts cannot overflow), `FirstSeen`,
`LastSeen` (UTC `DateTime`, never `DateTimeOffset`). EF Core history table `__pageNotFoundTrackerMigrations`
(`TrackingConstants.MigrationsHistoryTable`, schema `dbo` on SQL Server). `PageNotFoundTrackerDbContext`
(Infrastructure, public so provider projects can hold its migrations) is registered with `AddUmbracoDbContext<T>`
(`Umbraco.Cms.Persistence.EFCore`, connection `umbracoDbDSN` as Umbraco resolves it, `|DataDirectory|` included).

**Startup gate.** `TrackingMigrationService` waits for `RuntimeLevel.Run` (so after Umbraco's own unattended
install), selects the provider for Umbraco's provider name and runs `Database.MigrateAsync`. `ITrackingPersistenceState`
(Core, public) reports `Pending` / `Available` / `Unavailable`, with `WaitUntilReadyAsync` (the Management API uses it). No matching
provider: one log naming the provider (error, or warning when `TrackingEnabled` is off since nothing is lost),
`Unavailable`, the site keeps serving. A failed migration: error log, `Unavailable` (no retry in that run). Hits
arriving while `Pending` are queued and flushed once the gate opens; when the state becomes `Unavailable` the queue
(channel, pending buffer, in-flight batch) is cleared, and `NotFoundTracker` clears it again if the state turned
`Unavailable` while it enqueued.

**Upsert.** `DefaultTrackedUrlUpsert` (shared EF Core, every provider): per hit an `ExecuteUpdate` (`HitCount +=
n`, `LastSeen = max`), then an insert when no row matched; on a unique violation (another server inserted) the update
is retried once. Per-hit failures are logged at debug; after 3 consecutive failures (`MaxConsecutiveFailures`) the
database counts as down and the rest of the batch is returned unattempted, so a dead or slow database cannot stall a
flush. The returned `FailedTrackedUrlHits` carries the first exception, and the flush logs one summary warning per
failed flush with it attached. On cancellation it does not throw: it returns the current and the unattempted hits.
`ITrackedUrlUpsert.UpsertAsync` returns the hits it did not write; only those stay pending, so written hits are never
counted twice (also when a flush is cancelled mid-batch). An implementation that throws counts as having written
nothing, so a batching override must be all-or-nothing, and should return its unwritten hits on cancellation rather
than throw.

**Providers.** `ITrackingDatabaseProvider` (Core): `ProviderName` (Umbraco's `umbracoDbDSN_ProviderName`, e.g.
`Microsoft.Data.Sqlite`, `Microsoft.Data.SqlClient`), `ConfigureDbContext(options, connectionString)` (provider,
`MigrationsAssembly`, `MigrationsHistoryTable(TrackingConstants.MigrationsHistoryTable)`), optional `Upsert` (default
interface member, `null` = the shared upsert). Built in: `SqliteTrackingDatabaseProvider` / `SqlitePersistenceComposer`
and `SqlServerTrackingDatabaseProvider` / `SqlServerPersistenceComposer` (providers public sealed, composers internal,
`[ComposeBefore(typeof(PageNotFoundTrackerComposer))]`). Providers are registered with `AddSingleton` (not `TryAdd`);
`TrackingDatabaseProviderSelector` picks the **last** registration for a name, so a composer that runs after
`PageNotFoundTrackerComposer` replaces a built-in one. The SQLite provider does not set WAL (Umbraco 17 already runs SQLite
in WAL). A custom provider must ship migrations for **all** package tables (later specs add tables; implementers
must follow), generated against `PageNotFoundTrackerDbContext` with an `IDesignTimeDbContextFactory`, as the built-in
projects do. Minimal example:

```csharp
public sealed class MyTrackingDatabaseProvider : ITrackingDatabaseProvider
{
    public string ProviderName => "My.Data.Provider"; // Umbraco's connection string provider name

    public void ConfigureDbContext(DbContextOptionsBuilder options, string connectionString) =>
        options.UseMyDatabase(connectionString, db => db // the database's EF Core provider
            .MigrationsAssembly(typeof(MyTrackingDatabaseProvider).Assembly.GetName().Name)
            .MigrationsHistoryTable(TrackingConstants.MigrationsHistoryTable));

    // Optional: public ITrackedUrlUpsert? Upsert => new MyUpsert();
}

[ComposeAfter(typeof(PageNotFoundTrackerComposer))]  // after every package registration
public sealed class MyTrackingComposer : IComposer
{
    public void Compose(IUmbracoBuilder builder) =>
        builder.Services.AddSingleton<ITrackingDatabaseProvider, MyTrackingDatabaseProvider>();
}
```

**Migrations.** `pwsh scripts/add-migration.ps1 -Name <Name>` adds the same migration to both provider projects
(design-time factories, no database needed; prepends the copyright header; `src/.editorconfig` marks
`Migrations/*.cs` generated, so the build stays at zero warnings). `pwsh scripts/check-pending-model-changes.ps1`
fails when either provider has model changes no migration covers (CI runs it). Both scripts run `dotnet tool restore`
(`dotnet-ef` 10.0.12, `.config/dotnet-tools.json`, `rollForward=false`; keep it at the EF Core version Umbraco
resolves). The two `InitialCreate` ids differ by a few seconds per provider; that is not drift.

**SQL Server tests.** The database-layer integration tests (`TrackingDatabaseFixture`: migrations, upsert, concurrent
flushes) run on SQLite and, when `PNFT_SQLSERVER_CONNECTION` is set, on SQL Server; otherwise the SQL Server cases are
ignored with a message. The value is a **server-level** connection string (no `Database=`); each fixture creates and
drops a `pnft_test_<guid>` database. Docker (see SQL Server profile setup):
`Server=localhost,14330;User Id=sa;Password=PnftTest-only1!;TrustServerCertificate=True`. LocalDB:
`Server=(localdb)\<instance>;Integrated Security=True;TrustServerCertificate=True` (a named instance, not
`MSSQLLocalDB`, created with a pinned version such as `sqllocaldb create pnft-test 15.0 -s`; see SQL Server profile setup). CI uses a `mssql` service container on port 1433 and also sets `PNFT_REQUIRE_SQLSERVER=true`:
with that switch a missing `PNFT_SQLSERVER_CONNECTION` fails the SQL Server cases instead of ignoring them, so a broken
CI configuration cannot pass with SQL Server coverage silently skipped. Leave it unset locally. Umbraco itself boots only on SQLite in the tests
(`TrackingEndToEndTests`: a 404 request plus a forced flush gives a row); the full site on SQL Server is the
`sql-server` profile.

**Test site.** Running a SQLite profile creates the tracking tables in its committed database and fills them; never
commit that (restore with `git checkout`, see Committed databases). Committed databases hold no tracking tables or rows.

## Dashboard (spec 4)

A dashboard "Page not found" in the Content section (`/umbraco/section/content/dashboard/page-not-found-tracker`) lists
the tracked URLs: URL, domain, culture, type (page or file), first seen, last seen, hits; default order hits descending.
Search by URL, domain and culture filters, page size 20 (default), 50 or 100, every column sortable. Not live: users
reload (or change a filter) to see new data. Row actions: Create redirect (spec 5, see Redirects) and Block (spec 6, see
Blocked URLs). A second tab "Blocked URLs" lists the block rules (spec 6). States: loading,
empty (nothing tracked), empty after filtering, error, and unavailable (503: database unsupported or migrations failed;
final for the process, needs a fix and a restart).

**Access setting.** `DashboardUserGroups` (`PageNotFoundTrackerSettings`, a list of user group aliases, e.g.
`"DashboardUserGroups": ["admin", "editor", "writer"]` or `...__DashboardUserGroups__0=writer`). The settings default is an
**empty** list and `DashboardAccess` (Infrastructure, `IDashboardAccess` in Core, `AddUnique`) applies `admin` and `editor`
when nothing usable is configured: the configuration binder appends configured items to a pre-filled list, so a non-empty
default could never be narrowed. Rules: `admin` always passes (a bad setting cannot lock out the person who fixes it);
matching is case-insensitive and trims; a user in any one listed group passes; an empty configured array is the same as
absent (defaults, no warning); null or blank-only entries fall back to the defaults with one warning. Computed once at
startup (a change needs a restart). Group aliases are the role claims (`ClaimTypes.Role`) on the backoffice token. The
dashboard is registered for the Content section only, so an allowed user without Content access still does not see it.

**Management API** (BackOffice project, `AddPageNotFoundTrackerManagementApi()` from `BackOfficeComposer`). Routes under
`/umbraco/pagenotfoundtracker/api/v1` (`[BackOfficeRoute("pagenotfoundtracker/api/v{version:apiVersion}")]`, not
Umbraco's `/management/api`), controllers derive `TrackedUrlsControllerBase : ManagementApiControllerBase`, OpenAPI tag
`Tracked URL`:

| Operation ID | Route | Auth | Answers |
|---|---|---|---|
| `GetTrackedUrls` | `GET tracked-urls` | policy | 200 `PagedViewModel<TrackedUrlResponseModel>` (`total`, `items`); 400; 503 |
| `GetTrackedUrlFilterOptions` | `GET tracked-urls/filter-options` | policy | 200 `{ domains, cultures, hasEmptyDomain, hasEmptyCulture }`; 503 |
| `GetDashboardAccess` | `GET access` | any backoffice user | 200 `{ allowed }` |

- Anonymous gets 401 (the base controller's `BackOfficeAccess`); a user outside the groups gets 403 from the policy
  `PageNotFoundTracker.DashboardAccess` (`Constants.DashboardAccessPolicy`: the OpenIddict validation scheme plus
  `DashboardAccessRequirement`, evaluated by `DashboardAccessHandler` with `IDashboardAccess`). 503 problem details when
  persistence is `Unavailable`; while `Pending` the request waits (`WaitUntilReadyAsync`). Validation (400) runs first.
- `GET access` exists because the backoffice client cannot evaluate the setting itself: the dashboard condition asks the
  server, so the UI and the API share one rule. It is not a security boundary; the policy on the data endpoints is.
- `GetTrackedUrls` query: `skip` (default 0, `< 0` is 400), `take` (default 20, outside 1 to 100 is 400; the query service
  clamps to 1 to 100 as well), `orderBy` (`Hits` default, `Url`, `Domain`, `Culture`, `IsFile`, `FirstSeen`, `LastSeen`;
  string enum, an unknown name is 400), `direction` (`Ascending`/`Descending` default), `search`, `domain`, `noDomain`,
  `culture`, `noCulture`. `domain=x` is an exact match; `noDomain=true` means "no domain" (rows with an empty `Domain`);
  both together is 400; an empty `domain=` binds to null, so it means no filter. Culture the same.
- Items: `id`, `url` (the `DisplayUrl`), `relativePath` (not a column, for row actions), `domain`, `culture`, `isFile`,
  `hitCount`, `firstSeen`, `lastSeen` (UTC ISO 8601 with `Z`; SQLite returns an unspecified kind, the controller sets UTC).
- Search: whitespace-only means no search; a non-blank term is **not** trimmed (`" about"` is literal). The term is
  percent-decoded once (a malformed escape stays as typed), lower-cased and given the normaliser's slash rules (runs of
  `/` collapsed to one, one trailing `/` dropped, a lone `/` kept), so a URL copied from the table as displayed
  (`/Old-Page/`) finds the stored `/old-page`; then `%`, `_`, `[` and `\` are escaped (`LikePattern`, escape `\`) and a
  "contains" `LIKE` runs on the stored, normalised, lower-cased `Url`. So it is case-insensitive without depending on
  collation, and it covers only the stored part of a URL truncated at 2000 characters. Search does not look at domain or
  culture. Known limitation (not fixed): with `IncludeQueryString` on, the stored query part is lower-cased but not
  percent-decoded while the search term is decoded, so a term with an encoded character in the query part does not
  match itself.
- Sorting: every order ends with ascending `Id` as tie-break, so paging is stable. `Url`/`Domain`/`Culture` ordering and
  the exact `domain`/`culture` filters follow the database collation (SQLite binary, SQL Server's default case-insensitive,
  which for example sorts `/missing.css` before `/missing-page`); the filter options are sorted in memory (ordinal). Count and page are two queries; everything runs in the database.
- `TrackedUrlQueryService` (Infrastructure, `ITrackedUrlQueryService` in Core, `AddUnique`) takes `IServiceProvider` and
  resolves `IDbContextFactory<PageNotFoundTrackerDbContext>` per call: the pooled factory runs its options action on
  first resolve, which must happen after the persistence gate opened, so an early API request must not resolve it.
- Operation IDs are the action method names (`TrackedUrlsOperationIdHandler`; Umbraco's own handler only covers
  `Umbraco.Cms.Api*`). Renaming an action method changes the generated client function name.

**OpenAPI and the generated client.** Document name `page-not-found-tracker-management-api` (`Constants.ApiName`), served
at `/umbraco/swagger/page-not-found-tracker-management-api/swagger.json` when the environment is not Production, with
Umbraco's `Backoffice-User` security on every operation. The client (`@hey-api/openapi-ts` 0.99, `openapi-ts.config.ts`
in the client project root, flat SDK functions, `throwOnError: true` so Umbraco's `tryExecute` gets a rejected promise)
is committed in `src/api/`. Regenerate after an API change:

```bash
# single-site running on 44401 (any running site works with OPENAPI_URL=<its swagger.json URL>)
cd src/code/Umbraco.Community.PageNotFoundTracker.BackOffice.Client
NODE_TLS_REJECT_UNAUTHORIZED=0 npm run generate-client   # self-signed dev cert: local dev only, never anywhere real
```

Never commit the swagger file and never a base URL: the config sets `baseUrl: false` and patches `servers: [{ url: '/' }]`
into the document (otherwise the generator writes the fetch origin into `types.gen.ts`). `entry.ts` configures the
client at runtime with `UMB_AUTH_CONTEXT`'s `configureClient(client)` (server URL, credentials, bearer token with
refresh, Umbraco's interceptors), then `markClientConfigured()` (`client-ready.ts`) releases code that waits for it.
`check-package.ps1` fails the pack check if `localhost` appears anywhere in the client bundle.

**Client setup** (`src/code/...BackOffice.Client/src/`):

- Dashboard `Umbraco.Community.PageNotFoundTracker.Dashboard.TrackedUrls` (pathname `page-not-found-tracker`, label
  `#pageNotFoundTracker_dashboardLabel`, weight 5 = after Umbraco's Content dashboards), conditions
  `Umb.Condition.SectionAlias` = `Umb.Section.Content` and `Pnft.Condition.DashboardAccess`. Its element hosts
  `<umb-collection alias="Pnft.Collection.TrackedUrl">` (17.8 has a collection kind only for workspace views). No icon:
  17.8 dashboards have none (`MetaDashboard` is label and pathname only).
- The condition calls `GET access` after the client is configured; concurrent instances share the request and a
  successful answer (also `allowed: false`) is cached for the page load. It fails closed: an error (rejection, error
  status, missing body) means not permitted, but is not cached, so the next condition instance asks again.
- Collection aliases: collection `Pnft.Collection.TrackedUrl`, repository `Pnft.Repository.TrackedUrl`, view
  `Pnft.CollectionView.TrackedUrl.Table` (the only view), search box `Pnft.CollectionTextFilter.TrackedUrl`
  (`collectionTextFilter`, kind `default`; the framework's `filter` is mapped to the API's `search` in `query.ts`).
- Custom pieces, because the 17.8 framework lacks them: a `collectionView` element on `UmbCollectionViewElementBase` +
  `umb-table` (the 17.8 `table` kind cannot sort and renders plain values; shared base
  `collection/views/table/paged-table.element.base.ts`), and `PnftPagedCollectionContextBase`
  (`collection/paged-collection.context.base.ts`) extending `UmbDefaultCollectionContext` (keeps the repository error for
  the error and unavailable states, resets to page 1 on sort/search/filter/page size, page size 20 instead of 50,
  `setPageSize`, `requestFilterOptions`); `PnftTrackedUrlCollectionContext` is a thin subclass. The base overrides
  protected 17.8 internals and copies `collection-default.context.js` logic; the header `data-mark`s pass a Lit template
  as the column name. **Re-check both on every Umbraco upgrade.** Its token `PNFT_TRACKED_URL_COLLECTION_CONTEXT` sits on
  the framework alias with a discriminator: `pnftCollection === 'tracked-url'` (the block rule collection has the same
  shape, see Blocked URLs). `PNFT_PAGED_COLLECTION_CONTEXT` matches any package collection (`pnftCollection` is a string)
  for the shared toolbar pieces. The toolbar, states, search base, domain/culture filter and page size element are shared
  too (`paged-collection.element.base.ts`, `states/state-message.element.ts`, `filters/search.base.ts`,
  `filters/domain-culture-filter.element.ts`, `pagination/page-size.element.ts`).
- "No domain"/"No culture" are the `PNFT_EMPTY` marker (`collection/types.ts`, the string `'\u0000empty'`), mapped to
  `noDomain=true`/`noCulture=true`; undefined or `''` means all. It must be a string: Umbraco's `UmbDeepState` only emits
  when `JSON.stringify` of the value changes, and a symbol serialises like "All", so the change was invisible to
  observers. The leading U+0000 never occurs in a domain name or culture code, so a real value named `empty` or `all` is
  still sent as a value. Select option values are `all`, `empty` and `v:<value>`.
- `data-mark` hooks: `collection/data-marks.ts` (`pnft-collection`, `pnft-search`, `pnft-filter-domain`,
  `pnft-filter-culture`, `pnft-page-size`, `pnft-row` with `data-unique`, `pnft-total`, the five `pnft-state-*`, and
  `pnft-sort-<column>`). The E2E tests keep a copy: change both together.
- Texts in `localization/en.ts`, keys `pageNotFoundTracker_*`. The data source passes `disableNotifications: true`, so
  failures show inline states, not toasts. After `npm run build` while a site runs, new hashed chunks 404 until restart.
- The filter choices load once when the filter element connects; switching dashboard tabs keeps the element, so new
  domains or cultures appear after a page reload (the list itself reloads).
- The tab appears only after the `GET access` answer. In the manual checks a cold deep link (and once the first click on
  the tab) sometimes kept showing the Welcome dashboard under the dashboard's URL; a second click rendered it (the E2E
  helper reloads until the collection shows). Not diagnosed further (seen in a background browser pane).

**Row actions (specs 5 and 6).** Umbraco 17 has no `entityType` manifest: the entity type is the constant
`PNFT_TRACKED_URL_ENTITY_TYPE = 'page-not-found-tracker-url'` (`src/entity.ts`). Every table row gets an
`UmbEntityContext` with that entity type and `unique = String(id)`, and the table has the framework's entity actions
column (`umb-entity-actions-table-column-view`, last column, hidden when the collection hides item actions). An action
is a manifest plus a class extending `UmbEntityActionBase`. Two actions are registered on tracked rows:
`src/redirect/manifests.ts` (`Pnft.EntityAction.TrackedUrl.CreateRedirect`, kind `default`, weight 0,
`forEntityTypes: [PNFT_TRACKED_URL_ENTITY_TYPE]`, icon `icon-arrow-right`, label `#pageNotFoundTracker_createRedirect`,
`additionalOptions: true`, condition `Pnft.Condition.CanCreateRedirect`) with `src/redirect/create-redirect.action.ts`, and
`src/blocking/manifests.ts` (`Pnft.EntityAction.TrackedUrl.Block`, kind `default`, weight **-10**, icon `icon-block`,
label `#pageNotFoundTracker_block`, no condition, so on every row) with `src/blocking/block.action.ts`.

- The action's `args` carry only `entityType` and `unique` (the id as a string). The other row fields
  (`PnftTrackedUrlCollectionModel`: `id`, `url`, `relativePath`, `domain`, `culture`, `isFile`, `hitCount`, `firstSeen`,
  `lastSeen`, `canCreateRedirect`) are read from the collection context's items by `unique`
  (`PNFT_TRACKED_URL_COLLECTION_CONTEXT`); `relativePath` is there for redirects and blocking.
- Per-row visibility is a condition on the action (`Pnft.Condition.CanCreateRedirect`,
  `src/redirect/can-create-redirect.condition.ts`): it consumes the row's `UMB_ENTITY_CONTEXT` and the collection context
  and re-evaluates when the items change. The entity actions bundle evaluates action conditions with itself (inside the
  row) as host and renders nothing when no action is permitted, so a hidden action leaves no button.
- With a single permitted action the bundle renders an inline button in the actions column (data-mark
  `entity-action:<alias>`), not a dropdown. With two or more it renders the first (highest weight) inline plus a "..."
  dropdown (`umb-entity-actions-dropdown`) listing all of them. So on page rows Create redirect stays the inline button and
  Block is reached through the dropdown (E2E: click `umb-entity-actions-dropdown uui-button` in the row, then
  `entity-action:Pnft.EntityAction.TrackedUrl.Block`); file rows and query-string rows (no Create redirect) show only an
  inline Block button. A further action changes this again: re-check the E2E selectors.
- Capture the notification context and localization before opening a modal: the reload after success removes the row
  and with it the action's host. After a change call the collection context's `loadCollection()`.

**Tests.** Unit (`DashboardAccessTests`, `LikePatternTests`, `TrackedUrlsControllerTests`, `DashboardAccessHandlerTests`)
and Vitest for client logic. `TrackedUrlQueryServiceTests` (Integration) run the query service on SQLite and, with
`PNFT_SQLSERVER_CONNECTION`, on SQL Server: every sort column in both directions, search edge cases, filters, paging,
ties. `TrackedUrlsApiTests` (Integration, real backoffice tokens) cover 200/400/401/403, filter options, `access` and the
OpenAPI document, on SQLite only, because Umbraco's test host boots only on SQLite; it adds the BackOffice assembly as an
MVC application part (`CustomMvcSetup`), since the test server's entry assembly is Umbraco's. SQL Server is proven by
the query-service tests plus a manual run of the `sql-server` profile. E2E (`tests/dashboard.spec.ts`, single-site) sends
real 404s under a run-scoped prefix (`/pnft-e2e-<Date.now()>-...`, plus 22 `/pnft-e2e-pager-<run>-NN` URLs for page 2),
so reruns on the same database work, and needs `FlushInterval=00:00:01` (it retries with reloads for up to 90 s). The
pager checks depend on the English `uui-pagination` texts ("Pagination navigation. Current page: N.", "Go to page N"):
re-check on an Umbraco/UUI upgrade. The domain and culture filters are checked manually on the multi-site and
multi-language profiles (single-site has no domains).

## Redirects (spec 5)

A "Create redirect" row action (page rows only) opens a modal; confirming creates a redirect through the active
`IRedirectProvider` and deletes the tracked URL. Spec: `docs/superpowers/specs/2026-11-01-redirect-creation-design.md`.

**Contract** (Core, public, `Umbraco.Community.PageNotFoundTracker.Core.Redirects`):

- `IRedirectProvider`: `bool CanHandle(TrackedUrl url)` (decides the per-row `canCreateRedirect` flag and guards the
  endpoint) and `Task<RedirectResult> CreateAsync(TrackedUrl url, JsonElement? payload, CancellationToken ct)`.
  `RedirectResult(Succeeded, Message)` with `Success()` / `Failure(message)`; the message is shown to the editor inline.
  The provider must be singleton-compatible (the list controller asks `CanHandle` for every row).
- `ITrackedUrlRedirectService.CreateAsync(int trackedUrlId, JsonElement? payload, ct)` returns
  `CreateRedirectOutcome(Status, Message)`, `Status` = `Created`, `CreatedRecordNotRemoved`, `NotFound`, `NotSupported`,
  `Failed`. Internal default: `TrackedUrlRedirectService` (`AddUnique`). Flow: load the row (`GetByIdAsync`), else
  `NotFound`; `CanHandle` false gives `NotSupported`; `CreateAsync` failure or a (non-cancellation) exception gives
  `Failed` (exceptions are logged and answered with a generic text, never leaked); then `ITrackedUrlRemover.DeleteAsync`;
  a delete that throws gives `CreatedRecordNotRemoved` with a fixed text (the redirect exists, the row stays); a delete
  that finds the row already gone counts as `Created`.
- `ITrackedUrlRemover.DeleteAsync(TrackedUrl, ct)` (Core, `AddUnique`, default `TrackedUrlRemover`) runs inside
  `ITrackingFlusher.RunExclusiveAsync` (the flush service's own lock): it first `TrackingQueue.Discard`s the hits still
  queued, pending, retrying or in flight for that identity (`UrlHash` + `Domain` + `Culture`), then `ExecuteDeleteAsync`
  by id. So a flush cannot bring the row back and a flush never runs half-way through a delete. Hits for that URL that
  arrive after the delete create a new row (legitimate). Known, accepted limit: the lock and the queue are per process;
  with several servers another server's queued hits for the same identity can recreate the row after the delete (it
  shows up as a new row; creating the redirect again replaces the existing one).

**Default provider** `UmbracoRedirectProvider` (Infrastructure, internal, `AddUnique`), built on Umbraco's
`IRedirectUrlService` (the "Redirect URL Management" dashboard shows the result and lets editors remove it). The target must be published; for a record with a culture and a culture-variant target it must be published in that culture (`IsInvariantOrHasCulture`, unit-tested only: no multi-culture integration setup). Facts
verified against Umbraco 17.8 (`RedirectOrderingTests`, `UmbracoRedirectProviderTests` in Integration.Tests):

- Payload `{ "targetKey": "<document key>" }`. A missing, garbage or empty-guid payload, or a target that is not published
  (`IPublishedContentCache.GetByIdAsync(key, false)` is null), gives a `Failure` with a readable message. For a record with
  a culture, a variant target that is not published in that culture is refused too (`IsInvariantOrHasCulture`).
- `CanHandle` = not a file and no `?` in the URL. Query strings are NOT honoured by Umbraco's redirect lookup (it matches
  the decoded absolute path only; a route registered with `?x=1` never matches), so rows whose stored URL has a `?`
  (tracking with `IncludeQueryString` on) are unsupported: no action, and the endpoint answers 409.
- Route format (`RedirectRouteBuilder`): no domain: `/<relativePath>`; with a domain: `"<domainRootContentId>" + relativePath`
  (the relative path starts with `/`, e.g. `1234/old-page`). The domain is found by its trimmed, lower-cased name in
  `IDomainCache.GetAll(false)`; several same-name domains are told apart by the row's culture, no match gives a failure
  naming the domain. The culture is registered lower-case (`en-us`), an empty culture as null (matches every culture; a
  culture-specific entry wins over a culture-less one).
- An existing redirect with the same route and culture is replaced (deleted, then registered). It is deleted **by key**:
  `IRedirectUrlService.Delete(IRedirectUrl)` throws an `InvalidCastException` in 17.8 (int id cast to a Guid).
- Umbraco has NO setting that disables applying redirects. `Umbraco:CMS:WebRouting:DisableRedirectUrlTracking` only stops
  Umbraco from creating redirects on rename or move. The spec's "fail when redirect handling is switched off" is
  therefore not implementable and not implemented; do not document it as a feature.
- Redirects apply before spec 2's last-chance finder: a registered missing path answers 301 to the target (the visitor's
  query string is appended), other missing paths still get the 404 page. The `Location` is Umbraco's URL of the target
  (`/about/`, `/sitea/about/` under an alias domain). Request path matching was case-insensitive on SQLite (SQL Server
  not verified).

**Endpoint** (BackOffice, base `/umbraco/pagenotfoundtracker/api/v1`, policy `PageNotFoundTracker.DashboardAccess`):

| Operation ID | Route | Body | Answers |
|---|---|---|---|
| `CreateTrackedUrlRedirect` | `POST tracked-urls/{id:int}/redirect` | `{ "payload": <any JSON, or absent> }` | 200 `{ recordRemoved, message }`; 404 not found; 409 not supported (file row, query string row, provider says no); 422 provider failure (problem `detail` is the provider's message); 503 unavailable; 401/403 |

`recordRemoved: false` (status `CreatedRecordNotRemoved`) means the redirect exists but the row could not be deleted;
`message` says so and the client shows a warning toast. List items carry `canCreateRedirect` (the provider's `CanHandle`
for that row), which drives the action's visibility.

**Replacing the provider.** A composer after the anchor, `AddUnique` (see Composer order); the redirect service, the
endpoint and the client keep working. Minimal example:

```csharp
public sealed class MyRedirectProvider : IRedirectProvider
{
    public bool CanHandle(TrackedUrl url) => !url.IsFile;

    public Task<RedirectResult> CreateAsync(TrackedUrl url, JsonElement? payload, CancellationToken cancellationToken)
    {
        // payload is whatever the custom form returned. url.RelativePath, url.Domain and url.Culture identify the
        // request; return RedirectResult.Failure(message) to show a message inline.
        return Task.FromResult(RedirectResult.Success());
    }
}

[ComposeAfter(typeof(PageNotFoundTrackerComposer))]
public sealed class MyRedirectComposer : IComposer
{
    public void Compose(IUmbracoBuilder builder) =>
        builder.Services.AddUnique<IRedirectProvider, MyRedirectProvider>();
}
```

`CustomProviderReplacementTests` (Integration) prove this in the real composition with real backoffice tokens.

**Replacing the form (client).** The modal hosts `<umb-extension-slot type="pnftRedirectForm" single>`: of all permitted
`pnftRedirectForm` manifests the one with the highest `weight` wins (the framework sorts `b.weight - a.weight` and
`single` keeps the first; conditions and `overwrites` are honoured). The default form has weight **-100**, so a form with
the default weight 0 already replaces it. The contract (`src/redirect/redirect-form.extension.ts`,
`PnftRedirectFormElement`): a custom element with a `trackedUrl` property (the row) and
`getPayload(): Promise<unknown | undefined>`, which resolves to the payload sent as `payload`, or `undefined` when the
form is invalid (the form shows its own message and nothing is sent). The form never calls the API. The default form
returns `{ targetKey }` (a `umb-input-document`, max 1, required).

```ts
// in your extension's manifests
{
  type: 'pnftRedirectForm',
  alias: 'My.RedirectForm',
  name: 'My redirect form',
  weight: 10, // anything above -100 beats the default form
  element: () => import('./my-redirect-form.element.js'),
}
```

```ts
export class MyRedirectFormElement extends UmbLitElement {
  @property({ attribute: false }) trackedUrl?: { url: string; relativePath: string; domain: string; culture: string };

  async getPayload(): Promise<unknown | undefined> {
    return this._valid ? { someField: this._value } : undefined;
  }
}
export default MyRedirectFormElement; // the element loader needs a default export
```

The modal (`Pnft.Modal.CreateRedirect`) shows 409/422 problem `detail` inline (`pnft-redirect-error`), keeps Confirm
disabled while a request runs, and on success closes with `{ removed, message }`; the action then reloads the list and
shows a success toast (a warning toast when `removed` is false). A 404 from the endpoint shows the client's own text,
because Umbraco's error interceptor replaces 404 problem details.

**Data marks** (`src/redirect/data-marks.ts`, copied in `tests/redirect.spec.ts`: change both together):
`pnft-create-redirect-modal`, `pnft-redirect-confirm`, `pnft-redirect-cancel`, `pnft-redirect-error`,
`pnft-redirect-target-picker`, `pnft-redirect-target-required`, `pnft-redirect-summary`; the row button is Umbraco's
`entity-action:Pnft.EntityAction.TrackedUrl.CreateRedirect`.

**Client regeneration.** `npm run generate-client` cleans `src/api/` (it deleted the hand-written `client-ready.ts` and
rewrote core files with line-ending-only diffs), so `client-ready.ts` now lives in `src/client-ready.ts`. After
regenerating, commit only the real changes (`index.ts`, `sdk.gen.ts`, `types.gen.ts`) and revert the line-ending-only
ones with `git checkout`.

**Tests.** Unit: `RedirectRouteBuilderTests`, `UmbracoRedirectProviderTests`, `TrackedUrlRedirectServiceTests`
(Infrastructure.Tests), `CreateTrackedUrlRedirectControllerTests` (BackOffice.Tests). Integration: `RedirectOrderingTests`
(Umbraco behaviour spike), `UmbracoRedirectProviderTests` (real `IRedirectUrlService`, domain and culture routes),
`CustomProviderReplacementTests` (a custom provider replaces the default), plus the create endpoint, the list flag and the
remover (SQLite; SQL Server where `PNFT_SQLSERVER_CONNECTION` is set). Vitest: `redirect.service`, `can-create-redirect`,
`tracked-url.mapping`, `manifests` (the default form is a `pnftRedirectForm` with weight below 0). E2E
`tests/redirect.spec.ts` (single-site, needs `FlushInterval=00:00:01`): a `.css` row has no action, cancel keeps the row
and Confirm without a page shows the required message, and creating a redirect to About removes the row and the URL
answers 301 to `/about/`. The picker is driven through its shadow DOM (`umb-document-picker-modal`, the caret of
`uui-menu-item[label="Home"]`, then `uui-menu-item[label="About"]`): re-check on an Umbraco upgrade.

**Manual checks** (spec 5, real API with a client-credentials token, results read from the profile's URLs):
multi-site-aliases: `/siteA/old-a` and `/siteB/old-b` answer 301 to each site's own About, and the same relative path
`/old-both` on both sites redirects to different targets per site; multi-language: `/en/old-x`, `/nl/old-x` and
`/fr/old-x` answer 301 to `/en/about/`, `/nl/over-ons/` and `/fr/a-propos/` (the right culture). Not checked: the
`multi-site-hostnames` and `sql-server` profiles (there is no redirect-specific SQL Server code; the delete runs on the
shared EF Core context).

**Not verified in a browser:** the warning toast for `recordRemoved: false` (needs a failing delete), the 404/503/network
error texts of the modal (Vitest only) and Confirm being disabled while the request is in flight (too fast to observe;
the code sets `disabled` and `state="waiting"`). Not built: a picker filter for published pages (an unpublished page is
selectable; the provider rejects it with the inline message).

## Blocked URLs (spec 6)

A block rule (wildcard pattern, status code, domain and culture scope) answers matching requests at once with that status
and an empty body, for any method, before static files, redirects and routing; a blocked request is never tracked,
independent of `TrackingEnabled`, and no database is read on the request path. Saving a rule deletes the tracked URLs it
covers. Spec: `docs/superpowers/specs/2026-11-01-blocked-urls-design.md`; plan `docs/superpowers/plans/2026-11-04-blocked-urls.md`.

**Rule model and identity.** `BlockRule` (Core, `...Core.Blocking`): `Id`, `Pattern` (normalised), `PatternHash`,
`DisplayPattern`, `StatusCode`, `Domain`, `Culture`, `Created` (UTC `DateTime`). Table `pageNotFoundTrackerBlockRule`
(`BlockingConstants.TableName`) in `PageNotFoundTrackerDbContext`, migration `AddBlockRules` in both provider projects.
Columns: `Pattern`/`DisplayPattern` 800, `PatternHash` 64, `Domain` 255 and `Culture` 50 (both default `''`, never null;
empty = all). Identity = unique index (`PatternHash`, `Domain`, `Culture`); `PatternHash` is the SHA-256 hex of the
normalised pattern (`TrackedUrlNormalizer.Hash`), because 800 + 255 + 50 characters exceed SQL Server's 1700-byte index
key limit (the spec said "unique on the pattern"; this is the deliberate deviation, as `UrlHash` for tracked URLs).
Re-adding the same identity (any spelling or case) only updates `StatusCode`; the first `DisplayPattern` (the trimmed input,
shown in the list) and `Created` stay. The domain is trimmed and lower-cased. The culture is trimmed and canonicalised:
the Umbraco language ISO code that matches ignoring case, else the .NET `CultureInfo` name when it equals the input
ignoring case, else lower-case (so `en-us` and `en-US` are one rule on SQLite and SQL Server alike).

**Normalisation and matching** (`BlockPatternNormalizer`, `CompiledBlockPattern`, both Infrastructure). A pattern is
percent-decoded once (`Uri.UnescapeDataString`, a malformed escape stays as typed), then given `TrackedUrlNormalizer.NormalizePath`
(leading `/`, runs of `/` collapsed, trailing `/` dropped, a lone `/` kept, lower-cased); at most 800 characters (the
`RelativePath` column). `*` matches any run of characters including `/`; matching is ordinal-ignore-case on the whole path
(anchored both ends). A pattern ending in `/*` also matches the bare prefix: `/wp-admin/*` matches `/wp-admin` (a request
to `/wp-admin/` normalises to that). A decoded `%2A` becomes a live wildcard (decoding runs before compiling). Path only:
the query string is never matched (`/wp-login.php?x=1` is blocked by `/wp-login.php`). The request side is Kestrel's
already-decoded `Request.Path` (everything but `%2F`), normalised with the same rules and **not** decoded again (as tracking).

**Domains, alias stripping, precedence.** `BlockSnapshot.FindStatus(path, host, port)` does the domain selection itself, with
`DomainSelector` (Infrastructure `NotFound/`, shared with `RequestDomainMatcher`: host case-insensitive, port equal, longest
segment-boundary path prefix wins), and only when `NeedsDomainMatch` (a scoped rule exists or a domain has a path). The
matched domain's own path is removed (`TrackedUrlNormalizer.RelativeTo`), so `/wp-admin/*` matches `/siteA/wp-admin/x` under
`localhost:44402/sitea`. A rule's domain is compared with the lower-cased matched domain name (as tracking stores it), its
culture with that domain's culture, both ignoring case. A request with no matched domain gets only all-domain, all-culture
rules. Rules are sorted once when the snapshot is built and the first match wins: domain-scoped before all domains, then
culture-scoped before all cultures, then the longer literal prefix (characters before the first `*`), then the lowest status.

**Safeguards.** `BlockPatternValidator.Validate(normalisedPattern, statusCode)` (null = valid, else the message):
status outside 400 to 499 (`BlockingConstants.MinStatusCode`/`MaxStatusCode`); more than 800 characters; no character
besides `/` and `*` (`*`, `/*`, `/**`); an owned-path overlap, checked per `UmbracoOwnedPaths.Prefixes` entry (lower-cased):
the pattern matches the owned path itself (`/um*`, `/*umbraco*`, `/umbraco`), or its literal prefix equals it or lies inside
its subtree (`/umbraco/x*`, `/App_Plugins/*`); the message names the path ("The pattern covers /umbraco, which belongs to
Umbraco and cannot be blocked."). Accepted on purpose: `/*.php`, `/*/management/*` (no literal prefix inside an owned
subtree). `/umbracoextra` is not owned (segment boundary) and can be blocked. The real protection is the runtime exemption:
the middleware passes owned paths through before touching the snapshot, and `FindStatus` checks the normalised path and the
alias-stripped path again, so `/umbraco/x.php` is never blocked by `/*.php` and `/sitea/umbraco/x` neither.

**Status codes.** 400 to 499 through the API; the dialog offers 403, 404 and 410 (default 410,
`BlockingConstants.DefaultStatusCode`). The answer is that status with `Content-Length: 0` and no body; `next` is not called.

**Snapshot lifecycle** (`BlockSnapshotProvider`, singleton, `IBlockSnapshotProvider` `AddUnique`; timings
`BlockSnapshotTimings.Default`). There is always one current immutable `BlockSnapshot` (rules plus a copy of Umbraco's
domains), swapped with `Volatile.Write`, so readers never see a half-built set. `BlockSnapshotService` (hosted) waits for
`RuntimeLevel.Run` and the persistence gate, then loads; a failed load is retried every 30 s. `GetAsync`: the loaded
snapshot at once; `Empty` at once when persistence is `Unavailable` (one warning from the service, blocking is off for the
process), below `Run`, or after a failed first attempt; otherwise the request waits for the shared first load at most 5 s,
then passes through (`Empty`, one warning per process). `RefreshAsync` returns `bool` (true = swapped) and does nothing
(false) unless persistence is `Available`; it is serialised, and a failed rebuild logs an error and keeps the old snapshot.
`BlockSnapshotInvalidationHandler` rebuilds on `DomainCacheRefresherNotification` and `LanguageCacheRefresherNotification`.
The middleware skips normalisation when `snapshot.IsEmpty` (the common case), and returns quietly when the request is
aborted while waiting (other cancellations propagate).

**Middleware placement.** `BlockMiddleware` is added by `UsePageNotFoundTracker()` (entry project) through the internal
`UseBlocking()` (once, `Properties` guard), **before** the file 404 middleware, so it runs after `BootUmbracoAsync()` and before
`UseUmbraco()` (ahead of static files, media, redirects and routing). A site that does not call `UsePageNotFoundTracker()` has
no blocking at all (rules can be saved, nothing is answered). The integration test server calls `UseBlocking()` then
`UseFileNotFoundHandling()` from `PageNotFoundTrackerStartupFilter`.

**Refresher (all servers).** `BlockRuleCacheRefresher` (Infrastructure, internal, `PayloadCacheRefresherBase` with
`BlockRuleCacheRefresherNotification`, `UniqueId` `6d1c8a40-3f2b-4e57-9b1d-5a7c0e2f9d31`, name "Page Not Found Tracker Block
Rules"), registered with `builder.CacheRefreshers().Add<>()`. Payload `BlockRuleCachePayload(Pattern, Domain, Culture, Change)`
(Core) with `BlockRuleChange` `Saved`/`Removed`. `Refresh` rebuilds the snapshot, then for each `Saved` payload discards the
queued hits it covers (`TrackingQueue.DiscardMatching` inside `ITrackingFlusher.RunExclusiveAsync`); every step is logged and
swallowed, then `base.Refresh`. The service talks to it through the internal seam `IBlockRuleChangeNotifier`
(`DistributedBlockRuleChangeNotifier`, `DistributedCache.RefreshByPayload`; `AddUnique`). `BlockHitMatcher.Create` is the one
in-memory predicate (pattern + domain + culture, ignoring case) used by the service and the refresher. The originating server
runs both its own local refresh/discard and the refresher (harmless repetition). `BlockRuleCacheRefresherIntegrationTests` push a payload through Umbraco's `IJsonSerializer` into `Refresh(string json)` (the only path that reaches other servers; the positional record and its enum bind fine, no payload change needed).

**Create and delete** (`IBlockRuleService` in Core, `BlockRuleService` internal, `AddUnique`). `CreateAsync(pattern,
statusCode, domain, culture, ct)`: normalise, canonicalise the scope, validate (`Invalid` with the message, nothing else
happens), upsert (`ExecuteUpdate` of the status by identity, else insert; on a unique violation the update is retried once),
then (using `CancellationToken.None`: once the rule is committed a client abort must not leave the stored and the enforced rules out of step, so the request token only guards the upsert and, in delete, the rule delete) `RefreshAsync`, then notify `Saved` (a failing notifier is a warning), then inside `RunExclusiveAsync`:
`DiscardMatching` and an `ExecuteDelete` of the tracked rows. The delete (`WhereMatches`) is SQL: `RelativePath LIKE @like
ESCAPE '\'` (`BlockLikePattern.ToLike` returns `(Like, Exact)`: `*` to `%`, `\ % _ [` escaped; `Exact` is the pattern
without `/*` for a trailing `/*`, added as `OR RelativePath = @exact`), plus `Domain = @domain` and
`LOWER(Culture) = @culture` when scoped. Because it runs on `RelativePath`, all query-string variants of a path go too.
`Like_and_matcher_agree` (unit, SQLite) and `Like_delete_and_matcher_select_the_same_rows` (integration, SQLite and SQL
Server) prove the SQL and `CompiledBlockPattern` select the same rows, `%`, `_`, `[`, `\` included. A failing removal keeps
the rule: `CreateBlockRuleOutcome(Saved, BlockingConstants.RecordsNotRemovedMessage, RecordsRemoved: false)`.
`DeleteAsync(id)` deletes by id (false for an unknown id), then refreshes and notifies `Removed`; hits after that are
tracked again from count 1. `QueryAsync` mirrors the tracked URL query (search on the normalised `Pattern` with
`LikePattern.Contains`, Any/Empty/Exactly filters, Id tie-break, take clamped 1 to 100). `GetScopeOptionsAsync`: Umbraco's
domain names (no wildcard domains) trimmed, lower-cased, distinct, ordinal-sorted, and the language ISO codes.

**Accepted limits.** A hit that passed the middleware just before the refresh can be enqueued after the discard and be
written back as a count-1 row on that server (`NotFoundTracker` does not consult the snapshot). On other servers the discard
is best effort (their refresher runs whenever the distributed cache delivers the instruction).
A failed `RefreshAsync` in create/delete is not logged by the service (the provider logs it).

**Endpoints** (BackOffice, base `/umbraco/pagenotfoundtracker/api/v1`, policy `PageNotFoundTracker.DashboardAccess`, same
OpenAPI document; 401 anonymous, 403 outside the groups, 503 when persistence is `Unavailable`):

| Operation ID | Route | Answers |
|---|---|---|
| `GetBlockRules` | `GET block-rules` | 200 `PagedViewModel<BlockRuleResponseModel>` (`id`, `pattern` = `DisplayPattern`, `statusCode`, `domain`, `culture`, `created` UTC); 400; 503. Query as `GetTrackedUrls` (`skip`, `take` 1 to 100, `search`, `domain`/`noDomain`, `culture`/`noCulture`), `orderBy` `Created` (default), `Pattern`, `StatusCode`, `Domain`, `Culture`, `direction` default `Descending` |
| `CreateBlockRule` | `POST block-rules` | body `{ pattern, statusCode, domain?, culture? }` (null domain/culture = all); 200 `{ recordsRemoved, message }`; 400 (blank pattern, malformed body, or the validator's message as problem `detail`); 503 |
| `DeleteBlockRule` | `DELETE block-rules/{id:int}` | 200 empty; 404 problem; 503 |
| `GetBlockScopeOptions` | `GET block-rules/scope-options` | 200 `{ domains, cultures }` (configured in Umbraco, not from the rules); 503 |

**Client** (`src/blocking/`, constants in `constants.ts`). Entity type `PNFT_BLOCK_RULE_ENTITY_TYPE =
'page-not-found-tracker-block-rule'`. Dashboard `Umbraco.Community.PageNotFoundTracker.Dashboard.BlockRules` (label
`#pageNotFoundTracker_blockedDashboardLabel`, pathname `blocked-urls`, so `/umbraco/section/content/dashboard/blocked-urls`,
same conditions as the tracked dashboard) with weight **4**: 17.8 sorts dashboard tabs by weight descending, so 4 puts
"Blocked URLs" right after "Page not found" (5); 6 put it first. Collection `Pnft.Collection.BlockRule`, repository
`Pnft.Repository.BlockRule`, view `Pnft.CollectionView.BlockRule.Table` (columns pattern, status code, domain, culture,
created; empty scope shown as "All domains"/"All cultures"; default order created descending), search
`Pnft.CollectionTextFilter.BlockRule`. Collection action `Pnft.CollectionAction.BlockRule.Add` (kind `button`: 17.8 has no
`default` collection action kind) opens the rule modal empty. Entity actions: `Pnft.EntityAction.TrackedUrl.Block` (see Row
actions; defaults: the row's relative path with any `?query` dropped, the row's domain and culture, 410) and
`Pnft.EntityAction.BlockRule.Unblock` (Umbraco's confirm modal `umb-confirm-modal`, buttons `#confirm`/`#cancel`; a 404 shows
the client's own text). Modal `Pnft.Modal.CreateBlockRule` (sidebar, small; `rule-modal.element.ts`): pattern required
(placeholder `/wp-admin/*`), domain and culture selects from `GetBlockScopeOptions` plus the preselected value, status
403/404/410, a static warning, the 400 `detail` inline, Confirm disabled while sending; `open-rule-modal.ts` (shared by
Block and Add rule) captures the notification context and localization first, reloads the collection and shows a success
toast, or a warning toast with the server message when `recordsRemoved` is false. The list's domain/culture filter choices
also come from `GetBlockScopeOptions` (there is no rule filter-options endpoint), so a rule for a domain later removed from
Umbraco cannot be filtered by its value (it still shows under "Any domain"). `PnftBlockRuleCollectionContext` is a thin
subclass of `PnftPagedCollectionContextBase` with `pnftCollection = 'block-rule'`; its token
`PNFT_BLOCK_RULE_COLLECTION_CONTEXT` discriminates on that, as the tracked token does on `'tracked-url'` (without it either
token would bind to the other collection). Texts: `pageNotFoundTracker_*` in `localization/en.ts`.

**Data marks** (`src/blocking/data-marks.ts`, `PNFT_BLOCK_DATA_MARKS` and `PNFT_BLOCK_SORT_MARKS`, all `pnft-block-*`: list,
filters, states, modal fields and buttons, `pnft-block-sort-<column>`); `tests/blocked.spec.ts` keeps a copy in its `MARKS`
constant: change both together. Umbraco's own: `entity-action:Pnft.EntityAction.TrackedUrl.Block`,
`entity-action:Pnft.EntityAction.BlockRule.Unblock`, `collection-action:Pnft.CollectionAction.BlockRule.Add`. Select option
values `all`, `empty`, `v:<value>`; status values `403`, `404`, `410`.

**Tests.** Unit (Infrastructure.Tests `Blocking/`): `BlockPatternNormalizerTests`, `CompiledBlockPatternTests`,
`BlockPatternValidatorTests`, `BlockLikePatternTests` (incl. the SQLite parity test), `BlockSnapshotTests` (precedence table,
alias stripping, owned paths), `BlockSnapshotProviderTests`, `BlockSnapshotServiceTests`, `BlockMiddlewareTests`,
`BlockRuleServiceTests`, `BlockRuleCacheRefresherTests`; plus `NotFound/DomainSelectorTests`,
`Tracking/TrackingQueueDiscardMatchingTests`; BackOffice.Tests `BlockRuleControllersTests`. Integration (`Blocking/`):
`BlockRuleMigrationTests` and `BlockRuleServiceTests` (SQLite and SQL Server), `BlockSnapshotLoadTests`,
`BlockMiddlewareIntegrationTests` (410 without body and not recorded, existing page, static file ahead of the static file
middleware, block beats a redirect, owned paths), `BlockMiddlewareTrackingDisabledTests`,
`BlockRuleCacheRefresherIntegrationTests`, `BlockScopeOptionsTests`; `ManagementApi/BlockRulesApiTests` (200/400/401/403/404,
SQLite). Vitest: `block-rule.service`, `block-rule.mapping`, blocking `query`, `block-rule-table.columns`, `rule-form`,
blocking `manifests`. E2E `tests/blocked.spec.ts` (single-site, `FlushInterval=00:00:01`, run-scoped prefix): block from a
page row (dropdown) moves the URL to the blocked list and answers 410 with no new tracked row; Add rule with a wildcard;
unblock makes the URL tracked again from 1; an Umbraco-owned pattern shows the server message inline; a `.css` row is blocked
through its inline button. Cleanup unblocks every rule with the run prefix.

**Manual checks** (spec 6, real API with a client-credentials token, `curl -k`, `FlushInterval=00:00:01`; all rules deleted
afterwards):
- multi-site-aliases: all-domains `/wp-admin/*` (410) answered 410 with an empty body on `/siteA/wp-admin/x`,
  `/siteB/wp-admin/x`, `/siteA/wp-admin`, `/siteB/WP-ADMIN/a/b?q=1` and a `POST`, and deleted the tracked rows of both sites;
  `/siteA/about` stayed 200. `/pnft-manual-scoped` scoped to `localhost:44402/siteA` (stored lower-case) answered 403 on Site
  A only; `/siteB/...` and the no-domain `/pnft-manual-scoped` still got the 404 page and were tracked, and only Site A's row
  was deleted. A Site B-scoped `/wp-admin/*` (403) beat the all-domains rule on Site B only. `/umbraco/x*` and `/*` were
  rejected with 400. After unblocking, `/siteA/wp-admin/x` was tracked again with count 1.
- multi-language: `/pnft-manual-x` with culture `nl-nl` (stored `nl-NL`) answered 410 on `/nl/...` only; `/en/...`, `/fr/...`
  and the no-domain `/pnft-manual-x` kept the 404 page and their rows; an all-scope `/pnft-all/*` blocked all four. After
  unblocking `/nl/pnft-manual-x` was tracked again from 1.
- sql-server (LocalDB `(localdb)\pnft-test`; the API client was seeded by passing `--UmbracoMcp:ClientId`/`ClientSecret`
  test-only values on the command line): `/PNFT-SQL/*` deleted the rows `/pnft-sql/a`, `/pnft-sql/b.php` and `/pnft-sql`
  (exact) and kept `/pnft-sqlx`; the table held `/pnft-sql/*` with `DisplayPattern` `/PNFT-SQL/*` and a 64-character hash; the
  paths answered 410; re-adding as `/pnft-sql/*/` updated the same rule to 403; unblocking restored 404 and tracking from 1.
- Not checked: `multi-site-hostnames` (needs the hosts file).

**Not verified:** the modal's visual layout (it renders off-screen in the automation pane; fields and messages were driven
through the DOM), the `recordsRemoved: false` warning toast, the unblock 404/503/network texts (Vitest mapping only), the
hostnames profile, and the upsert's duplicate-key retry (no seam to inject the race; untested).

## Documentation (spec 7)

Spec: `docs/superpowers/specs/2026-11-01-documentation-design.md`; plan `docs/superpowers/plans/2026-11-09-documentation.md`.
The public pages are markdown in `docs/` (index `docs/README.md`, extension points in `docs/extending/`), written from the
shipped code, not from the specs. **One page owns each fact**; other pages link to it (settings: `configuration.md`,
precedence of blocking, redirects and the 404 page: `how-it-works.md`, pattern syntax: `blocking.md`). Public pages never
link to or quote `CLAUDE.md` or `docs/superpowers/`.

**Keeping it correct.** A change to a setting, a contract, an extension point or behaviour that a page describes updates
the page (and the sample it quotes) in the same change. Three checks fail the build otherwise:

- **Quoted samples.** A `csharp` or `typescript` block in a page is preceded by `<!-- sample: <repo path>#<id> -->` and
  must equal the region between `docs:begin <id>` and `docs:end <id>` comment lines in that file (blank edge lines,
  indentation and CRLF are ignored; `check-doc-samples.ps1`). Samples live in `src/samples/...Samples` (C#, built with the
  solution, one type per file, StyleCop-clean like the rest), in the client's `samples/` (TypeScript, `npm run
  check-samples`, outside the Vite build so nothing reaches the shipped bundle) and, for the `Program.cs` snippet of
  `getting-started.md`, in `test-sites/Website-V17/Program.cs`. To change an example, edit the sample, then the quoting
  page. `bash`, `json` and untagged blocks are not checked.
- **Settings.** `ConfigurationDocsTests` (Infrastructure.Tests) fails when a public setting of `PageNotFoundTrackerSettings`
  is not written as `` `Name` `` in `docs/configuration.md`, and when the first `json` block there does not bind to the
  settings class (its values are asserted, change both together).
- **Links.** `check-doc-links.ps1` checks relative links and anchors (case-sensitive, GitHub slugs) in `docs/`, `README.md`
  and `src/assets/readme.md`, and that every page is reachable from `docs/README.md`. External links are not checked. The
  NuGet readme uses absolute GitHub URLs, because nuget.org cannot resolve relative ones.

Facts the pages state that came from checking the running test site (not only the code): the 404, file 404 and tracked
rows of the four scenarios (`multi-site-and-language.md`), block and redirect behaviour on `multi-site-aliases`
(`blocking.md`, `redirects.md`), and the troubleshooting causes (wrong alias, tracking off, alias in a block pattern).
Entity type strings (`page-not-found-tracker-url`, `-block-rule`) are public: `src/entity.test.ts` pins them because the
row action sample writes them as literals. Dashboards have no screenshots (optional; the Browser pane renders the dialogs
off-screen). Not built: a docs site, a generated API reference, translations.

## Tests

- Unit: NUnit + Moq, coverlet. One project per code project with logic.
- Integration (`Umbraco.Cms.Tests.Integration` 17.8.0; SQL Server tracking cases need `PNFT_SQLSERVER_CONNECTION`,
  see Tracking): own `appsettings.Tests.json` (SQLite),
  `GlobalSetup.cs` in the global namespace. NUnit is pinned to 3.14.0 with `VersionOverride` in that
  project only (AutoFixture.NUnit3 needs NUnit 3.x); the central `Moq` 4.21.0 and
  `Microsoft.NET.Test.Sdk` 18.10.1 are what that package requires. Do not lower them.
- Client: Vitest (`npm test` in the client project, `*.test.ts` next to the code) for pure logic only; the
  collection elements and the context subclasses (`PnftPagedCollectionContextBase` and its subclasses) are covered by E2E and manual checks. CI runs it before the client build.
- E2E: `@umbraco/playwright-testhelpers` 17.0.36, the correct helper for Umbraco 17 (the Umbraco 18
  helper is `@umbraco-cms/acceptance-test-helpers`; the API is the same, only the import differs).
  `tslib` is an explicit dev dependency because the helper needs it without declaring it. Copy
  `.env.example` to `.env` (gitignored). The site must be running on single-site (44401) with
  `Umbraco__Community__PageNotFoundTracker__FlushInterval=00:00:01` (see Dashboard). Umbraco marks
  elements with `data-mark` (configured as `testIdAttribute`). `tests/not-found.spec.ts` covers spec 2,
  `tests/redirect.spec.ts` the create redirect flow (spec 5), `tests/blocked.spec.ts` blocking (spec 6), `tests/dashboard.spec.ts` the dashboard at `/umbraco/section/content/dashboard/page-not-found-tracker`. The spec 1
  smoke test (placeholder dashboard in Settings) is gone with the placeholder.

## CI and repository settings

- All three workflows run a `mssql` service container (`mcr.microsoft.com/mssql/server:2022-latest`, port 1433,
  health check `/opt/mssql-tools18/bin/sqlcmd -C ... -Q "SELECT 1"`), run `scripts/check-pending-model-changes.ps1`
  after the build and set `PNFT_SQLSERVER_CONNECTION=Server=localhost,1433;User Id=sa;Password=PnftTest-only1!;TrustServerCertificate=True`
  and `PNFT_REQUIRE_SQLSERVER=true` on the test step (test-only values, literal because service settings cannot read the `env` context). Keep the three
  in step. The service container is unverified until the first CI run.
- All three workflows run the client `npm test` (Vitest) between `npm ci` and `npm run build`.
- `pr-validation.yml` (PRs to `develop`): client test and build, restore, build, test with coverage, pack and
  `check-package.ps1`, then E2E against `single-site` (dev cert without trust, `curl -k` wait loop). The "Start
  test site" step sets `Umbraco__Community__PageNotFoundTracker__FlushInterval: '00:00:01'` for the dashboard tests.
  The Linux dev-cert/E2E step and that step env are unverified until the first PR run.
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
- Service registration through `IUmbracoBuilder` extension methods plus an `IComposer`. Every package composer is
  marked `[ComposeBefore(typeof(PageNotFoundTrackerComposer))]`; services implementers may replace are registered with
  `AddUnique`, never `TryAdd` (see Composer order).
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
  (see the test site). It registers the block middleware and the file 404 middleware (once each); placed after `UseUmbraco()` the
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
