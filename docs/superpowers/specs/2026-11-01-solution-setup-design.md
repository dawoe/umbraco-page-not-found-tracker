# Spec 1: Solution setup

Status: draft, builder check done, awaiting approval
Part of: the Page Not Found Tracker spec series (see "Series context")

## Intent

`Umbraco.Community.PageNotFoundTracker` is an Umbraco package that records requests
that end in a 404 and, in later versions, lets editors turn tracked URLs into redirects or
block them. This spec covers only the foundation: an empty but fully working solution that
later specs can add features to without touching the plumbing. It builds, tests, packs and
publishes, and it can be run against four realistic site setups.

The first release targets **Umbraco 17.x (LTS) on .NET 10**.

## Series context

| # | Spec | Depends on |
|---|------|------------|
| 1 | Solution setup (this spec) | none |
| 2 | 404 handling for pages and files | 1 |
| 3 | Tracking of 404 URLs | 2 |
| 4 | Backoffice dashboard for tracked URLs | 3 |
| 5 | Redirect creation | 4 |
| 6 | Blocked URLs | 4 |

Every spec is a file in `docs/superpowers/specs/` with a matching GitHub issue. The issue
holds a short summary, a link to the spec on `develop`, an acceptance checklist and links to
the issues it depends on. A later session reads the spec, writes a plan under
`docs/superpowers/plans/` and implements it.

Why the four site setups below matter for the series: with several sites and languages,
"a 404 for `/foo`" is ambiguous. Specs 2 to 6 must say which domain and culture a tracked
URL belongs to, and redirects and blocks must be scoped the same way. This spec only
provides the environments to prove that.

## Goals

1. A solution that follows the layered layout of the `umbraco-pagespeed-optimization`
   repository.
2. Unit, integration and end-to-end test projects, all wired into CI.
3. A test site with four launch profiles, each with its own committed SQLite database.
4. Umbraco MCP configured in every launch profile.
5. GitHub workflows for PR validation, beta releases and NuGet releases.
6. Repository documentation (`CLAUDE.md`, `README.md`, package readme, marketplace file)
   so a fresh session can build, run and test without asking questions.

## Non-goals

- No 404 handling, tracking, persistence, `DbContext`, migrations or dashboard content.
  Those belong to specs 2 to 4.
- No Management API controllers and no generated TypeScript API client yet. The client
  project only needs a minimal extension that proves the build and packaging pipeline.
  OpenAPI document registration and client generation arrive with spec 4, when there is an
  endpoint to describe.
- No support for Umbraco versions other than 17.x, and no Umbraco 18 work.
- No Docker, no database server other than SQLite.

## Anti-goals

- Per-scenario `appsettings.*.json` files. Scenario differences live in launch profiles only.
- A test site that only works for the single-site case. The other three scenarios are the
  reason the test site exists.
- Hard-coded dev-machine URLs ending up in shipped client code.
- Real credentials anywhere in the repository.

## Constraints

- Umbraco.Cms packages pinned to a 17.x range through central package management
  (`[17.x.y,18.0.0)` style, as the reference repositories do for their line).
- `net10.0`, nullable enabled, `RestorePackagesWithLockFile` on, `packages.lock.json`
  committed.
- StyleCop.Analyzers enforced. `src/stylecop.json` and `src/.editorconfig` are
  authoritative. Warnings are fixed, not suppressed. Every C# file has the copyright header,
  usings go outside the namespace, XML docs are required on public and internal members.
- Node 24.x for the client and E2E projects, matching the reference workflows.
- Branches: `develop` is the default and integration branch, `main` is the release branch.
  There is no `v17/develop` branch, because 17 is the only line for now.

## Approaches considered

Reference material for the structure and pipelines was supplied by the user:
`C:\forks\umbraco-pagespeed-optimization` (layout), `C:\forks\umbraco-search-examine-reindex`
(newer pipelines, `CLAUDE.md` style) and the Umbraco 17 integration testing documentation.

Specs and issues (decided with the user):

- **A. Spec file is the source of truth and the issue links to it.** Chosen. It matches the
  user's other repositories and lets a session read the whole set at once.
- B. Issue body is the spec. Rejected: specs never reach the repo.
- C. Duplicate the text in both. Rejected: the copies drift.

Solution layout (decided with the user): the layered four-project layout of the pagespeed
repository, rather than a single package project plus client.

Scenario databases (decided with the user): committed SQLite files, configured once through
Umbraco MCP, rather than seed-on-first-run or manual-only scenarios.

## Solution layout

```
src/
  Umbraco.Community.PageNotFoundTracker.slnx
  Directory.Build.props
  Directory.Packages.props
  .editorconfig
  stylecop.json
  assets/                     logo and package readme
  code/
    Umbraco.Community.PageNotFoundTracker/                  entry package, WebApplication extension
    Umbraco.Community.PageNotFoundTracker.Core/             settings, models, interfaces only
    Umbraco.Community.PageNotFoundTracker.Infrastructure/   composer, middleware, services, persistence
    Umbraco.Community.PageNotFoundTracker.BackOffice/       Razor class library, Management API
    Umbraco.Community.PageNotFoundTracker.BackOffice.Client/  Vite + Lit + TypeScript
  test/
    Directory.Build.props
    Umbraco.Community.PageNotFoundTracker.Infrastructure.Tests/   NUnit + Moq unit tests
    Umbraco.Community.PageNotFoundTracker.BackOffice.Tests/       NUnit + Moq unit tests
    Umbraco.Community.PageNotFoundTracker.Integration.Tests/      Umbraco integration tests
    Umbraco.Community.PageNotFoundTracker.E2E/                    Playwright
test-sites/
  Website-V17/                the test site
docs/superpowers/{specs,plans}/
.github/workflows/
CLAUDE.md  README.md  umbraco-marketplace.json  LICENSE
```

- The entry package references the other code projects and is the only project packed
  as the NuGet package, with the other projects' output included, as in the reference repo.
  The builder follows the reference repo's packing approach.
- The client builds into the BackOffice project's `wwwroot/App_Plugins/PageNotFoundTracker`
  (gitignored). The client build must run before `dotnet build`.
- The entry package exposes `UsePageNotFoundTracker()` on `WebApplication` as the hook for
  the consuming site's `Program.cs`. In this spec it exists and is wired, but does nothing
  yet. Spec 2 gives it meaning. The builder checks the exact name and signature against
  the reference repo's `WebApplicationExtensions`.
- The BackOffice client registers a minimal extension (exact choice left to the builder, for
  example an empty bundle or a placeholder dashboard) so the packaging path from TypeScript
  source to `App_Plugins` is proven end to end.

## Testing

- **Unit tests:** NUnit and Moq, one project per code project that has logic. Coverage via
  coverlet, as the reference repo does. With no features yet, each project needs at least
  one real test so the pipeline is proven (for example that the composer registers its
  services).
- **Integration tests:** follow the Umbraco 17 integration testing documentation
  (<https://docs.umbraco.com/umbraco-cms/17.latest/develop-with-umbraco/testing-and-debugging/integration-testing>).
  SQLite is the database, so no database service is needed on a Linux runner. One smoke
  test that boots Umbraco with the package composed in is enough for this spec.
- **E2E tests:** Playwright with `@umbraco/playwright-testhelpers`, which is the correct
  helper for Umbraco 17. Note the reference `umbraco-search-examine-reindex` repo uses
  `@umbraco-cms/acceptance-test-helpers` only because Umbraco 18 changed backoffice auth.
  The project follows the reference repo's shape: `.env.example`, an auth setup test,
  `playwright.config.ts`. One smoke test: log in to the backoffice and see the package's
  placeholder extension, or, if the placeholder is not visible UI, that the site and
  backoffice load. E2E runs against the `single-site` profile.

## Test site and launch profiles

One project, `test-sites/Website-V17`, built from Umbraco 17 with the **Clean** starter kit
for content and templates. It project-references the entry package so it runs against local
sources. It is in the solution but is not packed or published.

Everything scenario-specific is in `Properties/launchSettings.json` as `environmentVariables`
per profile. No per-scenario `appsettings` files. The shared `appsettings.json` holds only
what is identical across scenarios.

Each profile sets:

- `ASPNETCORE_ENVIRONMENT=Development`.
- `ConnectionStrings__umbracoDbDSN` pointing at its own SQLite file, and
  `ConnectionStrings__umbracoDbDSN_ProviderName=Microsoft.Data.Sqlite`.
- Unattended install settings (`Umbraco__CMS__Unattended__*`) that create a test admin
  user. Credentials are obvious test values taken from the project, never real ones.
- The configuration the Umbraco MCP connection needs (see below).
- A distinct HTTPS port so profiles can be told apart and do not collide with other sites
  on the machine.

| Profile | Shape | Hostnames |
|---------|-------|-----------|
| `single-site` | One site, one language | `localhost` |
| `multi-site-aliases` | Two sites told apart by path | `localhost/siteA`, `localhost/siteB` |
| `multi-language` | One site, several languages | `localhost` with culture variants |
| `multi-site-hostnames` | Two sites told apart by hostname | `www.siteA.local`, `www.siteB.local` |

Scenario content (decided with the user):

- `multi-site-aliases` and `multi-site-hostnames`: the Clean home tree is cloned into two
  roots, "Site A" and "Site B", each with its own domain. The same path (for example
  `/about`) must be able to exist on both sites.
- `multi-language`: one site with English, Dutch and French. Content is variant by culture,
  and each culture has its own domain path (`localhost/en`, `localhost/nl`, `localhost/fr`).

Database files: one SQLite file per profile, **committed**. After the profiles are run for
the first time, domains and languages for the three non-trivial scenarios are set up through
Umbraco MCP (an agent session, or a manual MCP client), and the resulting databases are
committed. Consequences the documentation must cover:

- Running a profile modifies its database file, which dirties the working tree. `CLAUDE.md`
  tells contributors to `git checkout` the files after local runs, and not to commit
  incidental changes. (The reference repos hit the same issue with a secret Umbraco appends
  to `appsettings.json`; the builder checks whether that also happens here.)
- The databases contain the test admin user and MCP credentials. These are test-only
  values and must say so in the docs.
- Setup steps for the three MCP-configured scenarios are written down so the databases
  can be rebuilt when Umbraco is upgraded.

Hostname profile requirements the documentation must cover: hosts-file entries for the
`.local` names, and an HTTPS development certificate that is valid for those names.
How Kestrel binds those hostnames from `launchSettings.json` is left to the builder.

## Umbraco MCP

Every launch profile has Umbraco MCP configured, following the `init-umbraco-mcp` skill.
Each profile gets its own API user and MCP configuration, so an agent can drive the right
scenario. The same MCP connection is how the multi-site and multi-language scenarios are
configured. The exact Umbraco 17 wiring is left to the builder, who should verify it
against the skill and current Umbraco documentation rather than assume.

## CI and release

Workflows live in `.github/workflows/` and follow `umbraco-search-examine-reindex`:

- **PR validation** (pull requests to `develop`): NPM install and build of the client,
  restore, build, unit tests with coverage, integration tests, and the E2E tests against
  the `single-site` profile. Running E2E on every PR is a call made in this spec, see below.
- **Beta release** (push to `develop`): same build and tests, pack with
  `--version-suffix beta-<run number>`, publish to the MyGet feed with
  `MYGET_API_KEY`.
- **Release** (manual `workflow_dispatch`, only from `main`): build, test, pack, publish to
  NuGet.org through OIDC login (`NuGet/login`, `NUGET_USER` secret), `--skip-duplicate`.

The README documents the secrets and repository settings that must exist. The builder does
not create secrets.

## Documentation deliverables

- `CLAUDE.md` in the style of the reference repos: overview, solution layout, build and
  test commands, launch profile table, coding conventions, gotchas learned during setup.
- `README.md` and `src/assets/readme.md` (the NuGet readme) describing the package.
- `umbraco-marketplace.json` for the Umbraco Marketplace, with author details as in the
  reference repos and package-specific title, tags and URLs.
- Package metadata in `Directory.Build.props`: authors, license MIT, repository URLs, icon,
  readme, symbols as `snupkg`, version `17.0.0` style prefix.

## Calls made in this spec

These are the writer's calls, not decisions the user made:

- Package, namespace and assembly root `Umbraco.Community.PageNotFoundTracker`.
- E2E tests run as part of PR validation, not only after merge. Reason: catches regressions
  before merge. Cost: slower PR builds.
- Management API and TypeScript client generation are deferred to spec 4.
- Version prefix starts at `17.0.0` and tracks the Umbraco major.
- Integration tests use SQLite.
- Node 24.x and the latest 17.x patch of Umbraco at build time. The lock files pin the
  resolved versions, so local and CI builds do not drift.
- Database files live in `test-sites/Website-V17/umbraco/Data/<profile>.sqlite.db`, one per
  profile, referenced from the profile's connection string through `|DataDirectory|`.
  A committed database is already installed, so unattended install does nothing for it. The
  unattended settings stay in each profile so a deleted database is recreated with the test
  admin user. `.gitattributes` marks `*.sqlite.db` as binary. Runtime state in
  `umbraco/Logs` and caches stays ignored.
- The Umbraco MCP credentials are test values. The API user lives in the database, and the
  matching client id and secret sit in the profile's environment variables and in the
  committed MCP client configuration (for example `.mcp.json`). The builder follows
  whatever the `init-umbraco-mcp` skill generates for 17 and names the exact MCP package in
  `CLAUDE.md`.
- `multi-site-aliases` uses Umbraco domains that include a path (`localhost/siteA`,
  `localhost/siteB`).
- Certificate and hosts-file setup for the `.local` hostnames is documented, not scripted.
  The hostname profile is verified manually. Only `single-site` is verified in CI.
- The reference repositories are on this machine at `C:\forks\umbraco-pagespeed-optimization`
  (layout, packing, `Directory.Build.props`) and `C:\forks\umbraco-search-examine-reindex`
  (workflows, `CLAUDE.md`, E2E project shape). Where they conflict, the second one wins for
  pipelines and tooling versions, the first one for solution layout. A builder who cannot
  read them stops and asks rather than guessing.
- Everything ships as one NuGet package: the entry package, with Core, Infrastructure and
  BackOffice output included.
- Package metadata: author Dave Woestenborghs and contributors, repository
  `https://github.com/dawoe/umbraco-page-not-found-tracker`, MyGet feed as in the
  reference beta workflow (`dawoe-umbraco`), MIT licence.

## Acceptance

- `npm ci && npm run build` in the client, then `dotnet restore`, `dotnet build` and
  `dotnet test` succeed from a clean clone with no warnings from the analyzers.
- `dotnet pack` produces the NuGet package, containing the client bundle.
- Each of the four profiles starts, shows the Clean site, and allows backoffice login.
  The aliases and hostnames profiles resolve both of their sites. The multi-language
  profile shows more than one language.
- MCP responds in each profile.
- E2E smoke test passes against `single-site`, locally and in CI.
- The three workflows exist and PR validation passes on the PR that adds them.
- `CLAUDE.md` is enough for a fresh session to do all of the above unaided.

## Left to the builder

- Exact Umbraco 17.x version range, package versions and ports.
- Packing approach details for the multi-project package.
- How the Umbraco 17 integration test base is referenced.
- Exact MCP wiring and API user setup in 17.
- Kestrel binding for the hostname profile.
- The minimal client extension and the exact smoke tests.
