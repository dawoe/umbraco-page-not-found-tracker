# Spec 2: 404 handling for pages and files

Status: draft, awaiting builder check and approval
Depends on: spec 1 (solution setup)
Series: see the table in `2026-11-01-solution-setup-design.md`

## Intent

When a visitor requests something that does not exist, the site should answer with a proper
404 page that belongs to the right site and language. Umbraco's built-in answer is one error
page per configured culture or domain, picked by content ID in config
(`ContentFinderByConfigured404`). That does not scale to several sites and languages and it
does not cover missing files at all.

This spec makes the package resolve the 404 page itself, for missing pages and for missing
files, and lets package users replace how the page is chosen. It does not store anything.
Spec 3 builds tracking on top of it.

## Goals

1. A missing page request is answered with the resolved 404 page and status code 404.
2. A missing file request (any extension) is answered with the resolved 404 page and status
   code 404.
3. The 404 page is chosen by a replaceable service. A default implementation ships.
4. The default implementation finds the page by document type alias, from the domain of the
   request, using the domain's culture for the language variant.
5. When nothing resolves, Umbraco's default 404 behaviour runs unchanged.

## Non-goals

- No storage or tracking of 404 URLs (spec 3).
- No dashboard, redirects or blocking (specs 4 to 6).
- No notification based extension point for resolving the page. The interface is the
  extension point (see Approaches).
- No file extension allow or deny list. Every missing file gets the 404 page.
- No handling of 404s for backoffice and Management API paths. Umbraco handles those.

## Anti-goals

- Changing the status code. The response is always 404, never 200 or a redirect.
- Breaking the Umbraco default when the package finds nothing.
- A page path and a file path that behave differently for the same site and language.
  Pages and files are separate code paths, but both use the same resolver service, so the
  chosen 404 page is identical.

## Constraints

- Umbraco 17.x on .NET 10, in the solution layout from spec 1: contracts and settings in
  `.Core`, middleware, finder and default resolver in `.Infrastructure`, registration in the
  composer, `UsePageNotFoundTracker()` on `WebApplication` as the pipeline hook.
- Settings under `Umbraco:Community:PageNotFoundTracker` in `appsettings.json`, bound to a
  settings class in `.Core`. This spec adds the section for the 404 page alias. Names of
  further settings are the builder's call but follow the same section.
- StyleCop and the other conventions from spec 1 apply.

## Approaches considered

Extension point for resolving the page (decided with the user):

- **A. Interface only**: `INotFoundPageResolver` with a default implementation; users
  replace it through DI. **Chosen.** Same pattern as Umbraco's `IContentLastChanceFinder`
  and the simplest to document and test.
- B. Notification only. Rejected: ordering between handlers and replacing the default are
  harder to reason about.
- C. Interface plus a notification before it. Rejected as more surface than needed now.

Pipeline integration (decided with the user): **separate paths for pages and files.**

- Pages: hook into Umbraco's routing at the last-chance step, so the resolved page is
  rendered through the normal Umbraco pipeline.
- Files: middleware that handles a 404 for a file request.
- The alternative of one middleware that inspects the final response for both was
  considered and not chosen.

## Behaviour

### Resolving the 404 page

The resolver service takes the request context and returns a published page and culture, or
nothing. Exact signature is left to the builder.

The default implementation:

1. Takes the domain from the current published request. For file requests there is no
   published request, so it matches the request host and path against Umbraco's domains.
2. Uses that domain's root node. If no domain matches, uses the first root node in the
   content tree.
3. Looks under that root node for a page of the configured document type alias. It checks
   direct children first, then deeper descendants. The first match wins. (Interpretation of
   the user's answer, which named both direct children and descendants without choosing.)
4. Uses the domain's culture to select the language variant of the page. With no domain,
   Umbraco's default culture applies.
5. Returns nothing if the alias is not configured or no page is found. Unpublished pages
   and pages without a published variant in the culture count as not found.

### Pages

When Umbraco routing finds no published page for a request, the package's last-chance
step asks the resolver. If it returns a page, that page is rendered with status 404. If it
returns nothing, Umbraco's default logic runs unchanged. The built-in
`ContentFinderByConfigured404` must not run ahead of ours, or the package must otherwise make
sure the resolver gets the first chance. How that is arranged is the builder's call.

### Files

For a request that does not match any file and that has a file extension, middleware
asks the same resolver. If it returns a page, the response is that page's rendered output
with status 404. If it returns nothing, the original 404 passes through. Every extension
is covered. Backoffice, Management API and other Umbraco-owned paths are excluded.
How the page is rendered from middleware is left to the builder.

### Multi-site and multi-language

The four scenarios from spec 1 are the acceptance environment:

- Same path on two sites resolves to each site's own 404 page.
- Alias-based sites (`localhost/siteA`) and hostname-based sites resolve by domain.
- Multi-language resolves the variant for the domain's culture (`localhost/nl` gives the
  Dutch variant).
- A request to a host with no matching domain falls back to the first root node.

## Settings

A section under `Umbraco:Community:PageNotFoundTracker` with at least:

- `NotFoundPageDocumentTypeAlias`: alias of the 404 page document type.

The default value is decided by the builder after checking what the Clean starter kit
provides. If no default is sensible, the setting is empty and the default resolver returns
nothing, so Umbraco's default applies.

## Testing

- **Unit tests:** the default resolver against mocked domain, content and culture services.
  Cover: domain match, no domain, direct child before descendant, culture variant, no alias
  configured, no match, unpublished page.
- **Integration tests:** with the Umbraco test base, a missing page and a missing file each
  return 404 with the configured page, and the fallback case returns Umbraco's default.
- **E2E tests:** in CI against `single-site`: missing page and missing file both show the
  404 page content with status 404.
- **Test site content:** the committed SQLite databases from spec 1 need a 404 page
  document type and a published 404 page for each root and culture in every scenario,
  added through MCP. The three non-trivial scenarios are verified manually, as in spec 1.

## Calls made in this spec

These are the writer's calls, not decisions the user made:

- Direct children first, then descendants, first match wins.
- No match returns nothing, and Umbraco's default behaviour applies.
- Backoffice and Management API paths are excluded from file handling.
- Unpublished pages do not count as a match.
- Settings live in a section named after the package, as the reference repos do.
- Rendering fidelity: the 404 page is rendered by Umbraco with the 404 page as the current
  content, so templates, navigation and view components work as on a normal page, for pages
  and for files. A best-effort or pre-rendered string is not acceptable. The builder picks
  the mechanism (re-execute, synthetic published request or similar). The status code is
  404 and the visitor's URL stays the original one.
- Culture is always the matched domain's culture, falling back to Umbraco's default
  language when there is no domain or the domain has no culture. It is not inferred from
  the path. Culture-per-path scenarios work through the domain, as in `localhost/nl`.
- Domain matching for file requests reuses Umbraco's own domain matching where it is public
  API. If it is not, the builder implements the same longest-match rules (most specific
  path first, host before bare path) and tests them with the `localhost/siteA`,
  `localhost/siteB` and `localhost` cases.
- Only the first root node is tried in the no-domain fallback. If it has no page of the
  alias, the resolver returns nothing and Umbraco's default applies. It does not try the
  next root.
- File handling applies to GET and HEAD requests only. Every extension is covered, including
  images and scripts (decided with the user), and the `Accept` header is not used to
  narrow it. Missing media files under `/media` get the 404 page too. Umbraco-owned paths
  (backoffice, Management API, `/umbraco`, `/App_Plugins`, `/_framework`) and
  `/.well-known` are excluded. The builder finalises the list and documents it.
- Order against the built-in finder: the package's last-chance step runs first. If it
  returns nothing, Umbraco's built-in logic (including `ContentFinderByConfigured404` and any
  site-level status code middleware) runs as before. The package does not remove the built-in
  finder.
- Signal for spec 3: each of the two code paths ends in one internal call point that
  receives the original request URL (path and query), the matched domain (or none), the
  culture, whether the request was for a page or a file, and whether a 404 page was
  resolved. Spec 3 decides what to do with it. This spec only guarantees the call point
  exists and carries that context.
- The package ships no document type. The test site provides the 404 page document type
  and pages. If the Clean starter kit lacks a suitable type, the builder adds one to the
  test site and to the committed databases of all four profiles. Integration tests create
  their own 404 type and pages in test code.

## Acceptance

- Missing page and missing file requests show the resolved 404 page with status 404 on
  `single-site`, locally and in CI.
- On the other three profiles, the resolution rules in "Multi-site and multi-language"
  hold, verified manually.
- Replacing `INotFoundPageResolver` through DI changes which page is shown, proven by a test.
- With the alias unset or no page found, Umbraco's default 404 behaviour is unchanged.
- Backoffice and Management API requests are unaffected.
- `CLAUDE.md` documents the setting, the resolver interface and the two code paths.

## Left to the builder

- The exact `INotFoundPageResolver` signature and the request context type.
- How the built-in configured-404 finder is taken out of the way.
- How a published page is rendered from middleware for files.
- The default alias, after inspecting the Clean starter kit.
- Which paths count as Umbraco-owned for the exclusion.
