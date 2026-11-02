# 404 Handling for Pages and Files Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Missing pages and missing files are answered with a resolved, Umbraco-rendered 404 page (status 404) per site and language, chosen by a replaceable `INotFoundPageResolver`.

**Architecture:** Contracts, settings and the Umbraco-owned path list live in `.Core`. `.Infrastructure` holds the default resolver, a domain matcher (for file requests, which have no published request), a last-chance finder (pages) and a middleware (files). Both paths call the same resolver and end in one internal `INotFoundRecorder` call point (a no-op now, spec 3 fills it). `UsePageNotFoundTracker()` registers the middleware.

**Tech Stack:** .NET 10, Umbraco.Cms 17.8.x, NUnit + Moq, Umbraco integration test base, Playwright E2E, Umbraco MCP for the test site databases.

**Spec:** `docs/superpowers/specs/2026-11-01-not-found-handling-design.md` (issue #2). Read it first; the "Calls made in this spec" section is binding.

## Global Constraints

- Umbraco 17.x on .NET 10; layering from `CLAUDE.md`: `.Core` = settings, models, interfaces, shared constants only; behaviour in `.Infrastructure`; entry project only wires.
- Settings section `Umbraco:Community:PageNotFoundTracker`, bound to a class in `.Core`. Setting `NotFoundPageDocumentTypeAlias`.
- The response status is always 404, never 200 or a redirect; the visitor's URL stays the original one.
- When nothing resolves, Umbraco's default 404 behaviour runs unchanged; the built-in `ContentFinderByConfigured404` is not removed.
- File handling: GET and HEAD only, every extension, no `Accept` narrowing, `/media` included. "File" = last path segment has an extension, in both code paths.
- Excluded (shared list in `.Core`): backoffice, Management API, `/umbraco`, `/App_Plugins`, `/_framework`. `/.well-known` is excluded from file handling only, not on the shared list.
- Culture = the matched domain's culture; otherwise Umbraco's default language (used only to pick the variant, never reported). No domain: first root node only.
- The package ships no document type. Integration tests create their own 404 type and pages.
- StyleCop zero warnings, copyright header, usings outside namespace, XML docs on public and internal members, tests are `internal sealed` NUnit fixtures. Any new `PackageReference` in Infrastructure must be mirrored in the entry csproj.
- Work stays on `feature/2-not-found-handling`; commits end with the session's `Co-Authored-By` line. Never commit incidental `.sqlite.db` or `appsettings.json` changes from a profile run (only deliberate database commits, added by exact path).

## Review Focus

1. A missing file under `/media/...` or a `.map`/`.pdf` URL with a query string: status 404 with the page, URL unchanged (Task 6).
2. A POST (or any non-GET/HEAD) to a missing file path: untouched, plain 404 (Task 6).
3. A path like `/umbraco/foo.js`, `/App_Plugins/x.js`, `/.well-known/x.txt`: not handled (Task 1, Task 6).
4. Resolved 404 page that itself has a missing asset or is the request target (`/error`, a 404 page requested directly): no redirect loop or double render (Tasks 5, 6).
5. Domain with no culture, unpublished 404 page, culture without a published variant: resolver returns nothing and Umbraco's default runs (Task 3).
6. Resolver throws: request must not turn into a 500 for what was a 404; log and fall back (Tasks 5, 6).

---

### Task 1: Core contracts, settings and Umbraco-owned paths

**Files:**
- Create: `src/code/Umbraco.Community.PageNotFoundTracker.Core/Configuration/PageNotFoundTrackerSettings.cs`
- Create: `src/code/Umbraco.Community.PageNotFoundTracker.Core/NotFound/INotFoundPageResolver.cs`, `NotFoundRequestContext.cs`, `NotFoundPage.cs`, `UmbracoOwnedPaths.cs`
- Modify: `src/code/Umbraco.Community.PageNotFoundTracker.Core/Constants.cs`, Core csproj (add `Umbraco.Cms.Core` reference if missing; mirror it in the entry csproj if it is not already there)
- Test: `src/test/Umbraco.Community.PageNotFoundTracker.Infrastructure.Tests/UmbracoOwnedPathsTests.cs`

**Interfaces:**
- Produces:
  - `Constants.SectionName` becomes `"Umbraco:Community:PageNotFoundTracker"` (full section path; update its doc comment).
  - `public sealed class PageNotFoundTrackerSettings { public string NotFoundPageDocumentTypeAlias { get; set; } = string.Empty; }`. The package default is empty (no sensible alias exists for arbitrary sites); the test site sets `error` (the Clean kit ships an `error` document type).
  - `public sealed record NotFoundRequestContext(string PathAndQuery, string Host, int? Port, bool IsFile)` plus `string Path` computed (path without query); `PathAndQuery` is the original URL.
  - `public sealed record NotFoundPage(IPublishedContent Content, string? Culture)`.
  - `public interface INotFoundPageResolver { Task<NotFoundPage?> ResolveAsync(NotFoundRequestContext context, IPublishedRequest? publishedRequest, CancellationToken cancellationToken); }` — `publishedRequest` is present on the page path (its domain is used) and null on the file path.
  - `public static class UmbracoOwnedPaths { public static IReadOnlyList<string> Prefixes { get; } ; public static bool IsUmbracoOwned(string path); public static bool IsFileRequest(string path); }` — `Prefixes` is `/umbraco`, `/App_Plugins`, `/_framework` (the Management API and backoffice live under `/umbraco`); comparison is case-insensitive on segment boundaries (`/umbracofoo` is not owned). `IsFileRequest` = last segment has an extension.

- [ ] **Step 1: Write failing tests** in `UmbracoOwnedPathsTests`: `IsUmbracoOwned` true for `/umbraco`, `/umbraco/management/api/v1/x`, `/UMBRACO/x`, `/App_Plugins/a/b.js`, `/_framework/blazor.js`; false for `/umbracofoo/x.js`, `/media/a.png`, `/about`. `IsFileRequest` true for `/x.pdf`, `/a/b.min.js`, `/media/a.png`; false for `/about`, `/a.b/c`, `/`.
- [ ] **Step 2: Run** `dotnet test src --filter UmbracoOwnedPathsTests`. Expected: FAIL (does not compile).
- [ ] **Step 3: Implement** the types above in `.Core` (settings bound later in Task 4).
- [ ] **Step 4: Run** the same command. Expected: PASS; `dotnet build src` has zero warnings.
- [ ] **Step 5: Commit** `feat: add 404 handling contracts and settings`.

### Task 2: Domain matcher for file requests

**Files:**
- Create: `src/code/Umbraco.Community.PageNotFoundTracker.Infrastructure/NotFound/IRequestDomainMatcher.cs`, `RequestDomainMatcher.cs`
- Test: `src/test/Umbraco.Community.PageNotFoundTracker.Infrastructure.Tests/RequestDomainMatcherTests.cs`

**Interfaces:**
- Consumes: `NotFoundRequestContext` (Task 1).
- Produces: `internal interface IRequestDomainMatcher { Domain? Match(NotFoundRequestContext context); }` where `Domain` is Umbraco's `Umbraco.Cms.Core.Routing.Domain` (has `ContentId`, `Culture`). Default `RequestDomainMatcher` uses `IDomainCache`/`IPublishedSnapshot` equivalent in 17.x.

- [ ] **Step 1: Write failing tests.** First check whether Umbraco 17's `DomainUtilities.SelectDomain(...)` is public API; if yes the matcher wraps it and the tests use it through mocked domain cache; if not, implement the longest-match rule (most specific path first, host before bare path). Tests, with domains `localhost:44402/sitea`, `localhost:44402/siteb`, `localhost:44402` style entries (lowercase, with port, as stored in the test databases): `Match` for host `localhost`, port 44402, path `/siteA/x.png` returns the siteA domain; `/siteB/x.png` the siteB domain; `/x.png` with only a bare-host domain returns that; no match returns null; hostname scenario `www.sitea.local:44404` matches by host, `localhost:44404` returns null; case-insensitive host and path.
- [ ] **Step 2: Run** `dotnet test src --filter RequestDomainMatcherTests`. Expected: FAIL.
- [ ] **Step 3: Implement** `RequestDomainMatcher`; prefer Umbraco's public matching, otherwise own longest-match. Document in the XML doc which one was used.
- [ ] **Step 4: Run** the filter. Expected: PASS.
- [ ] **Step 5: Commit** `feat: add domain matcher for file requests`.

### Task 3: Default resolver

**Files:**
- Create: `src/code/Umbraco.Community.PageNotFoundTracker.Infrastructure/NotFound/DefaultNotFoundPageResolver.cs`
- Test: `src/test/Umbraco.Community.PageNotFoundTracker.Infrastructure.Tests/DefaultNotFoundPageResolverTests.cs`

**Interfaces:**
- Consumes: `INotFoundPageResolver`, `NotFoundPage`, `PageNotFoundTrackerSettings` (via `IOptionsMonitor`), `IRequestDomainMatcher`, Umbraco's `IPublishedContentCache`/`IDocumentNavigationQueryService` (root nodes, children, descendants), `ILanguageService` (default culture), `IPublishedRequest` (page path domain).
- Produces: `internal sealed class DefaultNotFoundPageResolver : INotFoundPageResolver` implementing the spec's five rules.

- [ ] **Step 1: Write failing tests** (mocked services, one test each): domain match uses that domain's root; no domain falls back to the first root only (a second root with an alias page is not tried); direct child beats a descendant that appears earlier in the tree; domain culture selects the variant and is returned as `Culture`; domain without culture uses the default language for variant selection; alias empty returns null; no page of the alias returns null; unpublished page returns null; page with no published variant in the culture returns null; page path takes the domain from the `IPublishedRequest`, file path (null request) from the matcher.
- [ ] **Step 2: Run** `dotnet test src --filter DefaultNotFoundPageResolverTests`. Expected: FAIL.
- [ ] **Step 3: Implement** `ResolveAsync` per the spec ("Resolving the 404 page"). Return `Culture` = the matched domain's culture or null; the default-language fallback chooses the variant but is not returned (spec 3 contract).
- [ ] **Step 4: Run** the filter. Expected: PASS.
- [ ] **Step 5: Commit** `feat: add default 404 page resolver`.

### Task 4: Registration, call point and DI replacement

**Files:**
- Create: `src/code/Umbraco.Community.PageNotFoundTracker.Infrastructure/NotFound/INotFoundRecorder.cs`, `NoOpNotFoundRecorder.cs`, `NotFoundOccurrence.cs`
- Modify: `Infrastructure/Extensions/UmbracoBuilderExtensions.cs`
- Test: `src/test/Umbraco.Community.PageNotFoundTracker.Integration.Tests/NotFoundRegistrationTests.cs`

**Interfaces:**
- Produces:
  - `internal sealed record NotFoundOccurrence(string PathAndQuery, Domain? Domain, string? Culture, bool IsFile, bool PageResolved)`.
  - `internal interface INotFoundRecorder { Task RecordAsync(NotFoundOccurrence occurrence, CancellationToken cancellationToken); }`; `NoOpNotFoundRecorder` is the default (spec 3 replaces it).
  - `AddPageNotFoundTracker` binds `PageNotFoundTrackerSettings` from `Constants.SectionName`, registers `INotFoundPageResolver` with `TryAddSingleton`/`TryAddTransient` (so a consumer's registration wins regardless of order), `IRequestDomainMatcher`, `INotFoundRecorder`.

- [ ] **Step 1: Write failing tests**: settings bind (`NotFoundPageDocumentTypeAlias` from `Umbraco:Community:PageNotFoundTracker`, default empty); the default `INotFoundPageResolver` resolves to `DefaultNotFoundPageResolver`; a test composer/`ConfigureServices` replacement with a stub resolver is what `GetRequiredService<INotFoundPageResolver>()` returns (this is the replacement proof; Tasks 5 and 6 extend it end to end).
- [ ] **Step 2: Run** `dotnet test src --filter NotFoundRegistrationTests`. Expected: FAIL.
- [ ] **Step 3: Implement** the types and registration.
- [ ] **Step 4: Run** the filter. Expected: PASS.
- [ ] **Step 5: Commit** `feat: register 404 handling services`.

### Task 5: Last-chance finder for pages

**Files:**
- Create: `src/code/Umbraco.Community.PageNotFoundTracker.Infrastructure/NotFound/PageNotFoundLastChanceFinder.cs`
- Modify: `UmbracoBuilderExtensions.cs` (`builder.SetContentLastChanceFinder<PageNotFoundLastChanceFinder>()`)
- Test: `src/test/Umbraco.Community.PageNotFoundTracker.Integration.Tests/PageNotFoundTests.cs`

**Interfaces:**
- Consumes: `INotFoundPageResolver`, `INotFoundRecorder`, `NotFoundOccurrence`.
- Produces: `internal sealed class PageNotFoundLastChanceFinder : IContentLastChanceFinder`. It calls the resolver; on a page it sets the content, culture and status 404 on the `IPublishedRequestBuilder` and returns true; on null it delegates to Umbraco's `ContentFinderByConfigured404` (created through `ActivatorUtilities`) so default behaviour is unchanged. It always calls `INotFoundRecorder.RecordAsync` once (`IsFile` via `UmbracoOwnedPaths.IsFileRequest`, `PageResolved` true only when ours resolved). Resolver exceptions are logged and treated as "nothing resolved".

- [ ] **Step 1: Write failing integration tests** (create a 404 document type with alias from a test-set setting and published pages in test code; use the Umbraco web integration test base with `WebApplicationFactory`/`UmbracoTestServerTestBase` as available in `Umbraco.Cms.Tests.Integration`): missing page returns 404 with the 404 page's content in the body; response URL unchanged; alias unset returns Umbraco's default (same status and body as without the package); a stub resolver replacing the default (registered through DI) makes a different page show (the DI-replacement proof the issue requires); recorder invoked once with `IsFile=false`, `PageResolved=true`; resolver that throws still yields 404, not 500; requesting the 404 page's own URL directly returns 200 (no loop).
- [ ] **Step 2: Run** `dotnet test src --filter PageNotFoundTests`. Expected: FAIL.
- [ ] **Step 3: Implement** the finder and registration. Verify against Umbraco 17.8 that `SetContentLastChanceFinder` exists; if the finder collection works differently, pick the equivalent that guarantees ours runs first.
- [ ] **Step 4: Run** the filter. Expected: PASS.
- [ ] **Step 5: Commit** `feat: resolve the 404 page for missing pages`.

### Task 6: Middleware for files

**Files:**
- Create: `src/code/Umbraco.Community.PageNotFoundTracker.Infrastructure/NotFound/FileNotFoundMiddleware.cs`
- Modify: `src/code/Umbraco.Community.PageNotFoundTracker/Extensions/WebApplicationExtensions.cs`, `src/test/.../Infrastructure.Tests/WebApplicationExtensionsTests.cs`
- Test: `src/test/Umbraco.Community.PageNotFoundTracker.Integration.Tests/FileNotFoundTests.cs`

**Interfaces:**
- Consumes: `INotFoundPageResolver`, `IRequestDomainMatcher`, `INotFoundRecorder`, `UmbracoOwnedPaths`, `NotFoundRequestContext`.
- Produces: `internal sealed class FileNotFoundMiddleware` (constructor `RequestDelegate next`); `UsePageNotFoundTracker()` registers it once (guard with a flag in `app.Properties` so the existing idempotency test still holds and the middleware is not added twice). Position: the call sits before `UseUmbraco()`, so the middleware wraps the rest of the pipeline and acts after `next` when the response is a bodyless 404.

- [ ] **Step 1: Spike (30 minutes max), record the result in the XML doc of the middleware:** pick the render mechanism that renders the 404 page through Umbraco (templates, navigation, view components working) with status 404 and original URL. Candidates: re-execute the pipeline against the resolved page's URL while a request feature carries the 404 status and original URL, or a synthetic published request. A pre-rendered string is not acceptable. The mechanism must work for the page's culture variant and must not loop if the 404 page URL itself 404s (guard with a request item).
- [ ] **Step 2: Write failing integration tests**: GET and HEAD to `/missing.pdf`, `/media/missing.png?x=1`, `/a/missing.min.js` return 404 with the 404 page body (HEAD without body) and status 404; POST to `/missing.pdf` is untouched; `/umbraco/missing.js`, `/App_Plugins/missing.js`, `/.well-known/missing.txt` are not handled (plain 404); an existing file (`/robots.txt` or a static asset) is served unchanged; alias unset gives the plain 404; stub resolver replacement shows the stub's page; recorder invoked once with `IsFile=true`; resolver exception yields the plain 404; a request that Umbraco already routes as a page 404 (no extension) is not handled here (no double render, recorder called once total).
- [ ] **Step 3: Run** `dotnet test src --filter FileNotFoundTests`. Expected: FAIL.
- [ ] **Step 4: Implement** `FileNotFoundMiddleware.InvokeAsync(HttpContext)` and the `UsePageNotFoundTracker` change; build `NotFoundRequestContext` from `HttpContext.Request`, run only for GET/HEAD, extension present, not Umbraco-owned, not `/.well-known`, after `next` returned 404 with `!Response.HasStarted` and no content.
- [ ] **Step 5: Run** `dotnet test src`. Expected: all PASS, zero analyzer warnings.
- [ ] **Step 6: Commit** `feat: show the 404 page for missing files`.

### Task 7: Test site content and settings

**Files:**
- Modify: `test-sites/Website-V17/appsettings.json` (add `Umbraco:Community:PageNotFoundTracker:NotFoundPageDocumentTypeAlias` = `error`), the four `test-sites/Website-V17/umbraco/Data/*.sqlite.db` (deliberately, by exact path)

**Interfaces:**
- Consumes: Clean's `error` document type (view `Views/error.cshtml`, model `ContentModels.Error`, allowed under Home).

- [ ] **Step 1:** Start `single-site`, use `umbraco-single-site` MCP: look up the Error document type id and whether a published Error page already exists. If the type cannot be created under Home, update the type's allowed parents; if no Error type, add one (spec "Calls").
- [ ] **Step 2:** Create and publish one Error page per root: `single-site` (1), `multi-site-aliases` (Site A, Site B, each with its own title text identifying the site, e.g. "Page not found - Site A"), `multi-site-hostnames` (same), `multi-language` (1 page with en-US, nl-NL, fr-FR variants, titles "Page not found" / "Pagina niet gevonden" / "Page introuvable"). Follow "Rebuilding scenario databases" in `CLAUDE.md`, including the finish steps (checkpoint, vacuum, delete runtime rows).
- [ ] **Step 3: Verify per profile** with `curl -k -i`: single `/nothing-here` and `/nothing.pdf` give 404 with the page; aliases `/siteA/nothing` and `/siteB/nothing` give each site's page; hostnames via `--resolve`; language `/nl/nothing`, `/fr/nothing.pdf` give the Dutch and French variants; a host with no domain (aliases `/nothing`) gives the first root's 404 page; `/umbraco/` and the Management API behave unchanged.
- [ ] **Step 4:** `git checkout -- test-sites/Website-V17/appsettings.json` except the intended alias line (re-apply it), `git add` only the intended paths, commit `feat: add 404 pages to the test site databases`.

### Task 8: E2E on single-site

**Files:**
- Create: `src/test/Umbraco.Community.PageNotFoundTracker.E2E/tests/not-found.spec.ts`

- [ ] **Step 1: Write the tests**: a request to `/this-page-does-not-exist` and one to `/missing-file.pdf` (via Playwright `request`/`page.goto`) each return status 404 and a body containing the 404 page's heading (match against the Error page content in the `single-site` database); the URL stays as requested. Use `data-mark` selectors only if the page markup provides them; otherwise assert on text.
- [ ] **Step 2: Run** `npm run test:e2e` with `single-site` running. Expected: PASS together with the existing smoke test.
- [ ] **Step 3:** Confirm `pr-validation.yml` already runs E2E against `single-site` and needs no change; commit `test: e2e for 404 pages and files`.

### Task 9: Documentation and final verification

**Files:**
- Modify: `CLAUDE.md`

- [ ] **Step 1:** Update `CLAUDE.md`: spec 2 marked done and plan linked; the `NotFoundPageDocumentTypeAlias` setting and the test site value; `INotFoundPageResolver` and how to replace it; the two code paths (last-chance finder falling back to `ContentFinderByConfigured404`, and the file middleware with its render mechanism and exclusions); `INotFoundRecorder` as the spec 3 call point; Umbraco-owned path list; the test site now has Error pages per root and culture. Update the "no 404 handling" statements in the project overview and `UsePageNotFoundTracker` gotcha.
- [ ] **Step 2:** From a clean state run client build, `dotnet build` (zero warnings), `dotnet test`, `dotnet pack -c Release` plus `check-package.ps1`. Expected: all pass.
- [ ] **Step 3:** Manually verify `multi-site-aliases`, `multi-language` and `multi-site-hostnames` against Task 7's list; note the results in the PR description.
- [ ] **Step 4:** `git status` shows no incidental database or `appsettings.json` changes; commit `docs: document 404 handling`.
