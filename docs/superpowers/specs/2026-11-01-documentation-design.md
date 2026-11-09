# Spec 7: Documentation for implementers

Status: revised 2026-11-09 for issue #12 (specs 1 to 6 are merged); awaiting builder check and approval
Depends on: specs 1 to 6 (it documents what they build)
Series: see the table in `2026-11-01-solution-setup-design.md`. This spec was added after the
original six.

## Intent

Someone who finds `Umbraco.Community.PageNotFoundTracker` should be able to install it,
configure it, understand what it does and extend it without reading the source. This spec
defines the documentation set: where it lives, how it is organised, what each page must
cover and how it is kept correct.

The documentation is written for three audiences:

1. **Site implementers** who install and configure the package on an Umbraco 17 site.
2. **Package extenders**, developers who replace or add to parts of the package: the 404
   page resolver, the database provider, the redirect provider and form, and dashboard row
   actions.
3. **Editors** who use the two dashboards in the backoffice.

## Goals

1. A documentation set as markdown files in `docs/`, readable on GitHub and linked from the
   README, the NuGet readme and the Marketplace file.
2. A complete installation guide, from an empty Umbraco 17 site to a working tracker.
3. A reference for every setting.
4. One page per extension point, each with an explanation and a minimal working example.
5. A behaviour guide that explains how detection, tracking, redirects and blocking work,
   including the multi-site and multi-language rules.
6. A short guide for editors to the dashboards.
7. Troubleshooting for the problems the specs predict.
8. A way to keep the documentation correct as the code changes.

## Non-goals

- No documentation website, generated site or hosted docs. Markdown in the repository only.
- No API reference generated from XML comments.
- No translations. English only.
- No video or long tutorials.
- No contributor handbook. How to build and test the repository stays in `CLAUDE.md` and
  the README. The docs set may link to them.

## Anti-goals

- Documentation that describes the specs instead of the shipped code. The builder checks
  every statement against the implemented package, the test site and the tests, because the
  implementation may have diverged from the specs.
- Example code that does not compile.
- Duplicating settings or behaviour in several pages so that they drift apart. One page owns
  each fact and the others link to it.
- Mixing the internal design documents with the public docs. The public set lives
  directly in `docs/`, the design material stays in `docs/superpowers/`.

## Constraints

- Markdown only, with relative links, readable on GitHub without a build step.
- Public docs in `docs/`, not in `docs/superpowers/`.
- English, written for the audiences above. Plain language, short pages, one topic per page.
- Targets Umbraco 17.x and .NET 10, like the package.

## Approaches considered

Location and format (decided with the user): markdown files in the `docs/` folder of the
repository. A documentation site and the GitHub wiki were not chosen.

Keeping examples correct (decided with the user on 2026-11-09): sample code is real code that
is built in CI, instead of text that is only pasted into markdown. Three ways were weighed:

- **A. A dedicated samples folder, quoted by the docs (chosen).** A C# samples project under
  `src/samples/` (in the solution, so `dotnet build` and CI compile it) holds one small class per
  extension point: resolver, database provider, redirect provider, composer. The TypeScript
  samples (redirect form, row action) live in a small folder that the client's `tsc` type-checks.
  Each doc page quotes a marked region of those files, and a CI script fails when a quoted block
  no longer matches its source. Cost: one more project and one script to maintain.
- B. Samples in the test site `Website-V17`: rejected, because they would replace the defaults
  that the four scenarios and the E2E tests rely on.
- C. Reusing the integration tests as samples: rejected, because test scaffolding would end up in
  the quoted code and the client samples would stay unchecked. The existing replacement tests
  (`CustomProviderReplacementTests`, `NotFoundResolverReplacement*Tests`) stay as the proof that
  replacing a default works.

Unchecked snippets were rejected from the start: extension contracts change and unchecked
snippets silently rot.

Delivery (decided with the user on 2026-11-09): one pull request for the whole documentation set
and its CI checks, not one per audience.

Scope (decided with the user on 2026-11-09): docs, samples and CI checks only. A defect found while
writing is documented as the code behaves, listed in the pull request description and filed as its
own issue; it is not fixed here. The one exception (writer's call): a defect that makes a documented
example or the install guide fail may be fixed in this pull request, with a test, and is called out
in the description.

## Layout

```
docs/
  README.md                         index, audiences, where to start
  getting-started.md                requirements, install, first run, verification
  configuration.md                  every setting, its default and an example
  how-it-works.md                   detection, tracking, redirects, blocking
  multi-site-and-language.md        domains, cultures, aliases, hostnames
  dashboards.md                     guide for editors, both dashboards, access
  redirects.md                      the default redirect provider and its limits
  blocking.md                       patterns, status codes, safeguards, precedence
  extending/
    README.md                       overview of the extension points
    not-found-page-resolver.md      replace how the 404 page is chosen
    database-providers.md           support another database
    redirect-providers.md           backend contract and the redirect form
    dashboard-actions.md            add row actions to the dashboards
  troubleshooting.md                symptoms, causes, fixes
  superpowers/                      internal specs and plans, not public docs
```

Page names may be adjusted by the builder if a better split appears, as long as every
required topic below has one home and the index links to all pages.

## Content of each page

### `README.md` (index)

What the package is in a few lines, who the docs are for, a table of pages by audience, and
the supported versions.

### `getting-started.md`

- Requirements: Umbraco 17.x, .NET 10, supported databases (SQLite and SQL Server in the box).
- Installing the NuGet package.
- Adding `UsePageNotFoundTracker()` to `Program.cs`, in the right place in the pipeline. That one
  call registers the package's early middleware (blocking, ahead of routing and static files)
  and its later middleware (file 404 handling), so the page explains the call position and that
  there is only one call to add.
- Creating the 404 page document type and a page, and the setting for its alias.
- First run: the tables are created by migrations once Umbraco has finished installing and
  reached runtime level `Run`, in the same run, or after the restart Umbraco does itself.
  Say what a person sees when the installer UI is used and when unattended install is used.
- Verifying: request a missing page and a missing file, wait for the flush, open the
  dashboard.
- What happens with an unsupported database (tracking disabled with a logged error) and a
  pointer to the database provider page.
- Load-balanced setups: what each server does and what is shared.

### `configuration.md`

A table for each area with setting name, type, default, effect and an example: the 404
page alias, tracking on and off, flush interval, include query string, channel capacity,
and the dashboard user groups. It states that settings are read at startup and need a
restart. It shows a full `appsettings.json` example.

### `how-it-works.md`

- How a 404 for a page and a 404 for a file are detected, and the two code paths.
- Which paths are never handled.
- How tracking identifies a URL (domain, culture, normalised path, optional query string),
  what a record holds, and how batching and the flush interval work.
- That the dashboard is as fresh as the last flush.
- How redirects and blocks relate and which wins.

### `multi-site-and-language.md`

How domains and cultures decide the 404 page, the tracked record, redirects and block rules,
with the four scenarios from the test site as worked examples: one site, aliases,
hostnames and several languages.

### `dashboards.md`

For editors: the tracked URLs dashboard (columns, search, sort, filters, page size), the
blocked URLs dashboard, who can see them and how that is configured, and what the row
actions do. A few screenshots from the test site are allowed, stored in `docs/images/`.

### `redirects.md` and `blocking.md`

What each does, step by step with the dashboard actions, the limits of the default redirect
provider (pages only, content targets, how Umbraco's own redirect dashboard relates), the
block pattern syntax with examples, the status codes offered, the safeguards and the
precedence rules.

### `extending/`

Each page has: what the extension point is for, the contract (interface or extension
type) with its members, how the package uses it, a minimal working example, how to
register it, and what is guaranteed and what is not.

- `not-found-page-resolver.md`: the resolver interface, replacing it through DI, what the
  default does, and an example that picks the page differently.
- `database-providers.md`: the provider contract, how a provider is selected from the
  connection string, adding migrations, registering it from a composer, how failure is
  handled, and a minimal example, plus how the built-in providers are structured.
- `redirect-providers.md`: the backend contract, the payload, the supported check, the
  redirect form extension type and weight, an example of a custom provider with a custom
  form, and what the package still does for the implementer (delete, refresh, access).
- `dashboard-actions.md`: the entity types for tracked URLs and blocked rules and how to
  add an entity action to each.
- `README.md`: a table listing every extension point, what it replaces, the audience and
  the page.
- Every extension page states the registration rule (decided with the user on 2026-11-03): register
  from a composer marked `[ComposeAfter(typeof(PageNotFoundTrackerComposer))]`, because every
  package composer runs before that anchor and replaceable services use `AddUnique` (last
  registration wins; a registration made before the package is overwritten).

### `troubleshooting.md`

At least: nothing shows up in the dashboard (disabled tracking, flush interval, runtime
level, unknown database provider), a 404 page does not appear (alias, culture, domain,
publishing), a redirect created but not applied (file rows and query string rows have no action,
unpublished or wrong-culture target, wrong domain or culture on the row; Umbraco 17.8 has no
setting that switches redirect handling off, so that is not a cause: writer's call, spec 5
found it), a block that does not match (relative paths, scope, normalisation),
dashboard not visible (user groups, Content section access), and the checks to run for each.

## Keeping the documentation correct

- Every code sample in `docs/` comes from real code that is compiled in CI, for example a
  samples project in the repository or the test site. The documents quote it, or link to
  it. The builder chooses the mechanism.
- Every setting in the settings class appears in `configuration.md`. A test or script in CI
  fails if a setting is missing from the page. (Writer's call.)
- CI runs a markdown link check over `docs/` and the README.
- `CLAUDE.md` gets a rule that a change to a setting, a contract, an extension point or
  behaviour that the docs describe must update the docs in the same change.
- The builder follows the installation guide on a clean Umbraco 17 site created from the
  standard template, using the packed NuGet package, and fixes the guide until it works
  without outside knowledge.

## Linking

- The README gets a short "Documentation" section linking to `docs/README.md`.
- The NuGet readme (`src/assets/readme.md`) stays short and links to the docs on GitHub.
- `umbraco-marketplace.json`'s `DocumentationUrl` points to `docs/README.md` on `develop`.

## Calls made in this spec

These are the writer's calls, not decisions the user made:

- The page layout above, including the split of `extending/` into four pages.
- Sample code that is compiled in CI, and a CI check that settings are documented.
- A markdown link check in CI.
- Screenshots allowed for the dashboards guide only.
- English only, no generated reference, no docs site.
- Writing the docs from the shipped code, not from the specs.
- Sequencing: this spec is built after specs 1 to 6 are implemented and merged. It is not
  started earlier and pages are not drafted from the specs. Until then, each of those
  specs only maintains its own part of `CLAUDE.md`. Where the shipped code differs from a
  spec, the docs follow the code. A difference that looks like a defect rather than a
  deliberate change is listed in the pull request description so it can be looked at.
- Verifying the install guide: the builder runs `dotnet pack` and adds the output folder as
  a local NuGet source for a clean Umbraco 17 site made from the standard template. SQLite
  is the default. For SQL Server it uses the `sql-server` profile's setup in `CLAUDE.md`
  (LocalDB `(localdb)\pnft-test`, or the Docker container and connection string
  documented there); the pull request records which one was used. This is a manual verification step
  recorded in the pull request. It is not a CI job. The CI checks stay the samples, the
  settings check and the link check.
- Stating in `CLAUDE.md` that doc updates belong in the same change as behaviour changes.
- Added 2026-11-09 after the builder check (writer's calls):
  - The samples project is `IsPackable=false`, in the solution, and references the Core and
    Infrastructure projects directly (the entry project hides them with `PrivateAssets=all`). The
    test site and `check-package.ps1` do not know about it. The database provider sample is a
    minimal provider that compiles and names the migrations requirement; it does not ship a full
    migration set (the built-in provider projects are the reference).
  - TypeScript samples live outside the client `src/` bundle input, are type-checked by `tsc` only
    and never reach the Vite build, so nothing in them can leak into the shipped bundle.
  - The settings check is a unit test that reflects over `PageNotFoundTrackerSettings` and reads
    `configuration.md`. The quoted-block check is a PowerShell script run from `pr-validation.yml`.
    The link check covers relative links and anchors only, through a pinned action or tool; external
    URLs are not checked because they are flaky.
  - Public pages are self-contained and link to nothing internal. `CLAUDE.md` is a source to verify
    against, not to quote. The new `CLAUDE.md` rule says docs change in the same change as the
    behaviour they describe.
  - Screenshots are optional. If the Browser pane cannot capture a view reliably, the page ships
    without it and the pull request says so.
  - The builder runs the install verification itself on this machine (LocalDB for SQL Server). If a
    part cannot run here, the pull request marks that part unverified rather than claiming it.
  - `DocumentationUrl` is the `https://github.com/dawoe/umbraco-page-not-found-tracker/blob/develop/docs/README.md` form.

## Acceptance

- `docs/` contains the pages above (or a documented alternative split), and the index links
  to every page.
- A clean Umbraco 17 site, following only `getting-started.md`, ends with a working tracker
  on SQLite and on SQL Server.
- Every setting is documented with default and effect, and CI fails when one is missing.
- Each extension point has a page with a minimal example that compiles in CI. Following
  the example replaces the default behaviour.
- The behaviour and multi-site pages match the test site's four scenarios when checked by
  hand.
- Troubleshooting covers each symptom listed above.
- The link check passes. The README, the NuGet readme and the Marketplace file link to the
  docs.
- `CLAUDE.md` contains the documentation maintenance rule.

## Left to the builder

- Exact page names and any extra pages, as long as the topics are covered.
- How the samples are built and referenced from the pages.
- How the settings check and the link check are implemented in CI.
- Which screenshots to include and how they are captured.
- The wording and the order within pages.
