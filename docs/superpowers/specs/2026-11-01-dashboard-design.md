# Spec 4: Backoffice dashboard for tracked URLs

Status: draft, awaiting builder check and approval
Depends on: spec 1 (solution setup), spec 3 (tracking)
Series: see the table in `2026-11-01-solution-setup-design.md`

## Intent

Spec 3 records which missing URLs visitors request. This spec lets people in the backoffice
see that data: a dashboard in the Content section with a searchable, sortable, filterable,
paged list of tracked URLs, most requested first. It is built on Umbraco's collection
framework so that specs 5 and 6 can add row actions ("Create redirect", "Block") the way
Umbraco itself adds actions to lists.

## Goals

1. A dashboard in the Content section listing tracked URLs.
2. Columns: URL, first seen, last seen, hit count. Domain, culture and a page or file
   indicator are shown as well.
3. Default order is hit count, highest first. Every column is sortable.
4. Search by URL.
5. Filters for domain and for culture.
6. Paging with a user-chosen page size: 20 (default), 50 or 100.
7. Visible only to the user groups named in settings. Default: admin and editors.
8. A Management API endpoint that serves the list, with an OpenAPI document and a generated
   TypeScript client.
9. The list is built so spec 5 and 6 can add row actions without rewriting it.

## Non-goals

- No delete, clear or edit of tracked records. Row actions arrive with specs 5 and 6.
- No charts, trends or per-hit details.
- No live updating. The data is as fresh as the last background flush from spec 3, so up to
  one flush interval behind.
- No export.
- No other dashboard locations. Content section only.

## Anti-goals

- Hiding the dashboard in the UI while the API stays open. Access is enforced on the server
  as well.
- Loading all records into the browser and sorting, filtering or paging there. All of that
  happens in the database query.
- A dashboard that looks or behaves unlike the rest of the Umbraco backoffice.
- Different search or sort behaviour on SQLite and SQL Server.

## Constraints

- Umbraco 17.x. The management API and its OpenAPI document follow the Umbraco 17
  conventions, not the Umbraco 18 ones. The reference repositories mostly document Umbraco
  18 on their `develop` branches. Their `v17/develop` branches show the 17 approach.
- Layout from spec 1: controllers and models in `.BackOffice`, the Vite and Lit project in
  `.BackOffice.Client`, query logic behind an interface in `.Core` and implemented in
  `.Infrastructure` against the spec 3 `DbContext`.
- The generated client lives in the client project and is committed. The OpenAPI file
  itself is not committed, and the server's own base URL must not end up in the shipped
  bundle (the pagespeed `CLAUDE.md` describes this pitfall).
- Texts go through Umbraco's localization with keys prefixed for this package. English
  ships in the box.
- StyleCop and the other conventions from spec 1 apply.

## Approaches considered

How the list is built (decided with the user):

- A. A custom dashboard with a hand-built table, sorting, search, filters and paging.
  Rejected: all of that is built and tested by hand, and specs 5 and 6 would need their own
  row action mechanism.
- **B. Umbraco's collection framework hosted in a dashboard. Chosen.** It gives a
  repository, table view, paging, filters and entity actions on rows. Specs 5 and 6 become
  entity actions. The builder checks what the framework provides for free and builds custom
  pieces only where it does not, for example the page size choices, the domain and culture
  filters and the access condition.
- C. A dedicated workspace with a custom list. Rejected: more navigation than a single list
  needs.

Placement (decided with the user): the Content section, with the visible user groups set in
settings rather than fixed.

## Behaviour

### The list

| Column | Source | Notes |
|--------|--------|-------|
| URL | `DisplayUrl` | First-seen spelling |
| Domain | `Domain` | An empty value shows a localized "no domain" label |
| Culture | `Culture` | An empty value shows a localized "no culture" label |
| Type | `IsFile` | Page or file indicator |
| First seen | `FirstSeen` | Shown in the user's locale and timezone |
| Last seen | `LastSeen` | Same |
| Hits | `HitCount` | |

- Default order: hits descending. Clicking a column header sorts by that column and a second
  click reverses it. Ties are broken the same way every time (for example by URL), so paging
  is stable.
- Search: one box, case-insensitive "contains" match on the URL. It behaves the same on
  SQLite and SQL Server.
- Filters: domain and culture, each a choice of "all" or one of the values present in the
  data. The choices are served by the API so they reflect the data.
- Paging: page size 20, 50 or 100, default 20, chosen by the user in the dashboard. The
  total number of matching records is shown. Search, filters and sorting reset the list to
  the first page.
- States: loading, empty (no 404s tracked yet, with a short explanation that data appears
  after the next background flush), empty after filtering, and a readable error state when
  the API call fails.

### Management API

- A list endpoint taking: paging (skip and take, or page and size), sort column and
  direction, search text, domain filter and culture filter. It returns the matching
  records for that page plus the total count.
- An endpoint returning the distinct domains and cultures for the filters.
- Both are in a dedicated OpenAPI document with the Umbraco backoffice authentication
  scheme, versioned as Umbraco API controllers usually are. Operation IDs are stable so
  the generated client is stable.
- Page size is limited server side to the allowed values or a sensible maximum, so a
  caller cannot request unbounded pages.
- Exact routes, request and response models are left to the builder.

### Access

- A setting under `Umbraco:Community:PageNotFoundTracker` lists the user group aliases
  allowed to see the dashboard. Default: `admin` and `editor`.
- The dashboard is shown only to users in one of those groups, through a custom condition
  on the dashboard registration, as the pagespeed repo does with its custom condition.
- The API enforces the same rule with an authorization policy. A user outside the groups
  gets 403 from the API even if they call it directly.
- Settings are read at startup, as in spec 3.
- The dashboard is registered for the Content section, so a user in an allowed group who has
  no access to the Content section still does not see it. (Writer's call, see below.)

### Extension point for specs 5 and 6

- A tracked URL is registered as an entity type, and the collection's rows are entities of
  that type, so entity actions can target them.
- This spec registers no actions. It documents, in `CLAUDE.md`, how an action is added.

## Testing

- **Unit tests:** the controller (paging limits, sort whitelisting, access policy), the
  query service, and the client's repository where there is logic of our own.
- **Integration tests:** the list endpoint on SQLite and on SQL Server: default order, every
  sort column in both directions, search, each filter and their combination, paging and
  totals, stable order of ties, and a 403 for a user outside the allowed groups.
- **E2E tests:** in CI on `single-site`, with Playwright: log in as an allowed user, open
  the dashboard, see tracked URLs in the right default order, search, sort, filter and change
  the page size. The test must be deterministic, which needs tracked data. The builder
  decides how, for example by sending 404 requests during test setup and a short
  `FlushInterval` set for the E2E run through an environment variable, or by seeding.
- The other launch profiles are checked manually: the domain and culture filters on
  `multi-site-aliases`, `multi-site-hostnames` and `multi-language`.

## Calls made in this spec

These are the writer's calls, not decisions the user made:

- Domain, culture and type are shown as columns in addition to the four the user named.
- No delete or clear action in this version.
- User groups are identified by alias, with default `admin` and `editor`.
- The API enforces the groups as well as the UI.
- Membership in an allowed group does not override Content section access.
- Search is a case-insensitive "contains" on the URL only, not on domain or culture.
- Ties in sorting are broken by a fixed secondary key so paging is stable.
- The first version is not live. Users reload to see new data.
- Page size is limited server side.
- English only for now, but all texts go through localization.
- Entity for the rows: only the entity type and the collection item model are registered.
  No workspace and no menu entity. Specs 5 and 6 can open a modal from an entity action
  without either. If one of them needs a workspace, it adds that itself.
- Access safety: the `admin` group always passes, even if the setting omits it, so a bad
  setting cannot lock out the person who has to fix it. An empty or invalid setting falls
  back to the defaults (`admin` and `editor`) and logs a warning. Group matching is by
  alias, a user in any one allowed group passes, and the same check serves the condition
  and the API policy.
- Search runs on the normalised `Url` column from spec 3 using a lower-cased, decoded
  search term, so there is no collation difference between databases. `%`, `_` and `[` in
  the search text are escaped and treated as literal characters. If spec 3's URL length
  handling truncates `Url`, search covers the stored part, which the builder documents.
- Fallback for the collection framework: the dashboard stays on the collection framework and
  adds custom pieces for what it lacks (page size choices, domain and culture filters, the
  access condition). If a requirement turns out to be impossible inside it, the builder
  stops and asks before switching to a custom list. It does not switch on its own.
- Empty values are filterable: the domain and culture filters include a "no domain" and a
  "no culture" choice, distinct from "all". The distinct-values endpoint returns them, and
  the list endpoint takes an explicit marker for "empty" that cannot be confused with no
  filter.
- E2E: setting a short `FlushInterval` through an environment variable for the E2E job, and
  creating tracked data by sending real 404 requests, is allowed and preferred over seeding
  the database. Changing the spec 1 workflow for that is in scope.
- The API returns `FirstSeen` and `LastSeen` as UTC ISO 8601 timestamps. The backoffice
  formats them in the user's locale and timezone, and sorting uses the raw values.

## Acceptance

- On `single-site`, after some 404s and a flush, the dashboard in the Content section lists
  them with URL, first seen, last seen and hits, ordered by hits descending.
- Every column sorts in both directions. Search narrows by URL. Domain and culture filters
  narrow the list and are populated from the data. Page size can be 20, 50 or 100 and
  defaults to 20.
- On the multi-site and multi-language profiles, the filters show the right domains and
  cultures and narrow the list correctly (manual check).
- A user in a group not named in the setting does not see the dashboard and gets 403 from
  the API. Changing the setting changes who sees it after a restart.
- Search, sort and filter behave the same on SQLite and SQL Server, proven by the
  integration tests.
- The OpenAPI document and the generated client are in place and regenerating the client is
  documented in `CLAUDE.md`.
- `CLAUDE.md` documents the setting, the endpoints, the collection setup and how to add a
  row action.

## Left to the builder

- Exact API routes, models, query parameter names and API version.
- Which parts of the collection framework are used as is and which need custom pieces.
- How the page size choice and the filters are built inside the framework.
- How the access condition checks the user's groups.
- How the E2E test gets deterministic tracked data.
- Icon, dashboard name and the English texts.
