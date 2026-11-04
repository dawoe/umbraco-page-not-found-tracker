# Spec 6: Blocked URLs

Status: draft, build decisions added, awaiting builder check and approval
Depends on: spec 5 (redirects), spec 4 (dashboard), spec 3 (tracking), spec 2 (404 handling)
Series: see the table in `2026-11-01-solution-setup-design.md`

## Intent

Much of what a site's 404 list fills up with is noise: scanners asking for `/wp-login.php`,
`/wp-admin/...` and similar. This spec lets people block such URLs. A blocked URL is no
longer tracked, is answered immediately with a status code of the editor's choosing (for
example 410 or 403), and costs the site almost nothing. Rules can use wildcards and can apply
to all domains.

## Goals

1. Block rules: a URL pattern, a status code, and a scope of domain and culture (each either
   "all" or one value).
2. Wildcards in patterns, for example `/wp-admin/*`.
3. A request that matches a rule is answered at once with the rule's status code and is
   not tracked.
4. A "Block" action on the tracked URL rows from spec 4, and an "Add rule" action on the
   blocked URLs list for patterns typed by hand.
5. A second dashboard in the Content section, shown as a second tab next to spec 4's, that
   lists the rules and lets people unblock them.
6. Creating a rule deletes the tracked records it matches.
7. Matching is fast, from an in-memory rule set that every server keeps current.

## Non-goals

- No rule editing. A rule is changed by removing it and adding another.
- No import or export of rules, no bulk operations, no rule ordering or priorities.
- No response bodies or rendered pages for blocked requests.
- No automatic blocking based on request behaviour.
- No pluggable rule storage or pluggable matching. Not requested.
- No setting to turn blocking off. Removing the rules does that.

## Anti-goals

- A rule that takes the site or the backoffice down by accident. See "Safety".
- A database read on the request path.
- A rule change that only reaches some servers.
- Matching that behaves differently on SQLite and SQL Server when records are deleted.

## Constraints

- Umbraco 17.x and the layout from spec 1. Contracts and the rule model in `.Core`,
  matching, middleware, cache and persistence in `.Infrastructure`, endpoints in
  `.BackOffice`, dashboard and actions in `.BackOffice.Client`.
- Persistence uses the spec 3 `DbContext`. The new table gets migrations in both built-in
  providers, and works with custom providers from spec 3's contract. The migrations workflow
  and the pending-model-changes check from spec 3 apply.
- The dashboard follows spec 4's approach: the collection framework, the same access rule
  (the configured user groups, with `admin` always allowed), the same OpenAPI document and a
  regenerated TypeScript client.
- StyleCop and the other conventions from spec 1 apply.

## Approaches considered

What blocking does (decided with the user): stop tracking, respond with a configurable
status code, rules can be listed and removed, and wildcards can apply across all domains.

Where the rules live (decided with the user): the same EF Core `DbContext` as spec 3, in a
table of their own. Umbraco's own data and app settings were not considered because the
rules change at runtime and must be edited in the backoffice.

Keeping each server's copy current (decided with the user):

- **A. Umbraco's distributed cache with a cache refresher, holding a snapshot that is
  replaced as a whole. Chosen.** Changes reach every server within moments. This is the pattern `umbraco-pagespeed-optimization` uses for its
  media exception cache.
- B. A periodic reload from the database. Rejected: rules can take up to an interval to
  apply on other servers and the database is queried when nothing has changed.
- C. Loading at startup only. Rejected: it needs a restart for every change.

Blocks and redirects (decided with the user): when a URL matches both a block rule and an
Umbraco redirect from spec 5, the block wins.

## Behaviour

### Rules

| Field | Meaning |
|-------|---------|
| Pattern | URL path pattern, starting with `/`, with `*` as a wildcard |
| Status code | Response status for matching requests |
| Domain | An Umbraco domain name as configured, or empty for all domains |
| Culture | A culture, or empty for all cultures |
| Created | UTC timestamp, and who created it if cheaply available |

- `*` matches any run of characters, including slashes. So `/wp-admin/*` matches
  `/wp-admin/a/b/c` and `/wp-admin/`. Matching ignores case, decodes percent-encoding, and
  ignores a trailing slash and repeated slashes, the same normalisation as tracking in
  spec 3.
- Patterns match the path only. A query string never takes part in blocking, regardless of
  the `IncludeQueryString` setting from spec 3.
- Patterns are relative to the domain root. For a site told apart by an alias path, such as
  `localhost/siteA`, the domain's own path (`/siteA`) is removed from the request path
  before matching. So `/wp-admin/*` for all domains also blocks `/siteA/wp-admin/x` and
  `/siteB/wp-admin/x`. The row's "Block" action therefore preselects the record's `RelativePath` from spec 3, not the full
  stored path. The prefixes to strip come from Umbraco's configured domains, kept in the snapshot, so no database
  or domain lookup happens per request. The snapshot is also rebuilt whenever Umbraco's domains or
  languages change (through the matching Umbraco notifications), not only when a rule changes, so
  alias stripping and domain scoping never go stale.
- A culture-scoped rule only matches requests whose matched domain has that culture. A
  request with no matched domain never matches a culture-scoped rule, and matches a
  domain-scoped rule never. Only all-domains, all-cultures rules apply to it.
- The uniqueness key uses the normalised pattern, so `/WP-Admin/` and `/wp-admin` are the
  same rule. The normalised form is what is matched and indexed. The editor's own spelling
  of the first-created rule is kept for display, as with tracked URLs in spec 3.
  The same index-size limits as in spec 3 apply and are handled the same way.
- Domain and culture use the same representation as tracked records, with an empty string
  meaning "all". A rule with a domain applies only to requests that match that domain
  using the domain matching rules from spec 2. A rule with a culture applies only when the
  matched domain has that culture.
- A pattern, domain and culture combination is unique. Adding an existing combination
  updates its status code rather than failing.
- Status codes: the API accepts 400 to 499. The dialog offers 403, 404 and 410 and defaults
  to 410. (Writer's call.)

### Answering blocked requests

- Middleware that runs before Umbraco's routing and before the redirect handling from spec
  5, so a block wins over a redirect and a matching request never reaches the 404 handling
  or tracking from specs 2 and 3. It also runs ahead of the static file middleware, so
  rules apply to requests for real files as well. It applies to every request method.
- The response is the rule's status code with an empty body. No page is rendered.
- When several rules match, the most specific one wins: a rule with a domain beats one
  for all domains, then a rule with a culture beats one for all cultures, then the longer
  literal prefix wins. Remaining ties go to the lowest status code. (Writer's call.)
- The rule set is held in memory, so a request never reads the database. Domain matching
  is only done when at least one rule is scoped to a domain or culture.
- Blocking works regardless of `TrackingEnabled`.
- The rule set is a snapshot: an immutable object that is replaced as a whole, never edited
  in place. There is always a current snapshot, so a request never meets an empty or
  half-built cache. See "Cache refresh".

### Safety

Because the middleware runs before routing, a rule applies to any request, including ones
that would have found a real page or file. That is what makes it cheap, and it is also the
risk. Two safeguards:

- Umbraco-owned paths are never blocked and rules do not match them: the backoffice, the
  Management API, `/umbraco`, `/App_Plugins`, `/_framework` and the other paths that spec 2
  excludes. The list is the public shared code in `.Core` that spec 2 provides, used by both specs. `/.well-known`
  is not on it, so it can be blocked. A rule whose pattern overlaps one of
  these paths is rejected when it is created, with a message naming the path, rather than
  accepted and ignored.
- A pattern must contain at least one literal character besides slashes and wildcards, so
  `*`, `/*` and `/**` are rejected with a clear message.

Beyond that, a rule that matches real pages is the editor's responsibility. The dialog
states that rules apply to every matching request, not only to missing pages.

### Creating a rule

- From a tracked row: the "Block" action opens a dialog with that row's URL as the
  pattern, its domain and culture preselected (each changeable to "all"), and a status
  code. The pattern can be edited to add wildcards.
- From the blocked list: an "Add rule" action opens the same dialog, empty.
- On success: the rule is saved, the rule sets on all servers are refreshed, and tracked
  records matching the rule are deleted. For a rule with wildcards that is every record
  within the rule's domain and culture scope whose path matches. Queued hits that match are
  discarded, using the mechanism from spec 5 (extended to patterns) and serialised with the
  flush as in spec 5.
- Deleting matches the pattern against the `RelativePath` column from spec 3, so alias prefixes and
  query strings need no special handling. It must match the same records on SQLite and SQL Server.
  The builder decides how the pattern is translated to a database query, with literal `%`, `_` and
  `[` handled, as in spec 4's search.
- With `IncludeQueryString` on, tracked records carry a query string. The row's "Block"
  action preselects the path without it, creating a rule deletes every matching record
  whatever its query string, and the queued hit discard compares on the path only.
- The delete runs on the server that handles the request. The cache refresher message also
  tells every other server to discard its own queued hits that match the new rule, so a
  queued hit elsewhere does not bring a record back. That is best effort.
- The "Block" action is available on page rows and on file rows. A file row's pattern is
  the file's URL.
- A failure to delete records after saving the rule is logged and shown. The rule stays.
  Matching records that remain will no longer grow, because requests are blocked first.
- The tracked list refreshes after creating a rule from it.

### Removing a rule (unblocking)

- An "Unblock" action on each rule, with a confirmation. It deletes the rule and refreshes
  the rule sets on all servers. Matching URLs are tracked again from then on, from a count
  of one.

### The blocked URLs dashboard

- A second dashboard in the Content section, built on the collection framework like spec 4,
  with the same access rule and the same list capabilities: sortable columns, a search on
  the pattern, filters for domain and culture, paging with 20, 50 and 100 and the same
  states (loading, empty, empty after filtering, error).
- Columns: pattern, status code, domain, culture, created. Default order: newest first.
  An empty domain or culture shows a localized "all domains" or "all cultures" label.
- Rows are entities of a new entity type, with the "Unblock" entity action. The collection
  has the "Add rule" action.
- Endpoints: list, create and delete, plus one that lists the domains and cultures configured in Umbraco
  for the rule dialog (the values from spec 4's filter endpoint only cover URLs that already had a
  404, so a rule for a domain without any tracked 404 could not be chosen), in the same OpenAPI
  document as specs 4 and 5.
  Responses distinguish: created, invalid pattern or status with a message, not found.
  Exact routes and models are left to the builder.

### Cache refresh

- A cache refresher is registered, following `umbraco-pagespeed-optimization`. Saving or
  deleting a rule notifies all servers, which rebuild their rule sets.
- The cache is snapshot based (decided with the user). A snapshot holds all rules and the
  compiled matchers. A refresh builds a complete new snapshot in the background and then
  swaps it in with one atomic reference replacement. The old snapshot keeps serving requests
  until the new one is ready, so there is never a moment without data. If a rebuild fails,
  the old snapshot stays in use and the failure is logged.
- The first snapshot is filled at startup, once Umbraco's runtime level is `Run` (as in spec
  3), and the application does not serve blocked-URL decisions from an empty cache. A
  request that arrives before the first snapshot is ready waits for that one initial load,
  which all requests share, instead of passing through unblocked. If the initial load
  fails, the request passes through, a warning is logged, and the load is retried in the
  background. When the runtime level is below `Run`, for example during installation, the
  snapshot is an empty valid snapshot, and the first real load happens once the level
  reaches `Run`.
- Refreshing never happens inline on a request after that initial load.

## Testing

- **Unit tests:** pattern normalisation and matching (wildcards at the start, middle and
  end, case, encoding, trailing slash), most-specific-wins, scope rules for domain and
  culture, the safety rules (rejected patterns, Umbraco-owned paths), the status code
  limits, the rule set rebuild, and the delete and queue discard behaviour.
- **Integration tests:** on SQLite and SQL Server: migrations for the new table, creating a
  rule deletes matching tracked records and not others, a blocked request returns the status
  with no body and is not tracked, a block beats a redirect for the same URL, unblocking
  makes the URL tracked again, and Umbraco-owned paths are never blocked.
- **E2E tests:** on `single-site` in CI: block a tracked URL from its row and see it leave
  the tracked list and appear on the blocked list, add a wildcard rule, then unblock it.
- **Manual checks** on the multi-site and multi-language profiles: a rule for one domain does
  not affect the other, a rule for all domains affects both, and culture scoping works.

## Calls made in this spec

These are the writer's calls, not decisions the user made:

- A second dashboard in the Content section is the "second tab".
- Two ways to add a rule: from a tracked row, and by hand on the blocked list.
- `*` matches across slashes. Matching ignores case, encoding and trailing slashes.
- The path only is matched, never the query string.
- The status code range, the three codes offered and the default of 410.
- Precedence between several matching rules.
- Rules are not editable. Adding an existing rule updates its status code.
- Blocking applies before routing, so it also applies to real pages. Umbraco-owned paths
  and patterns without a literal character are excluded as safeguards.
- Blocking works independent of `TrackingEnabled`.
- The blocked list has the same capabilities as spec 4's list, including filters.
- The empty body for blocked responses is as proposed to the user and not objected to.
- Snapshot cache filled at startup, as decided with the user. The writer's detail: a request
  arriving during the initial load waits for it, and only a failed initial load lets
  requests pass through.
- Access uses the dashboard's user groups, with no extra permission.
- Patterns are relative to the domain root, so one `/wp-admin/*` rule covers alias sites too.
  This is a behavioural call, made because "on all domains" was the user's requirement.
  A culture-scoped rule never matches a request with no matched domain.
- Blocks apply to every method and to real files too, ahead of the static file middleware.
- The Umbraco-owned path list becomes shared code, and overlapping patterns are rejected at
  creation.
- Rule uniqueness is on the normalised pattern.
- Other servers discard their own matching queued hits when the refresher fires.
- Persistence availability (spec 3): when no database provider matched or migrations did not
  complete, blocking passes requests through with a logged reason, the dashboard shows an
  "unsupported or unavailable" state and the endpoints return a clear error.
- Strictly after spec 5: it reuses spec 5's discard mechanism, spec 3's `RelativePath` and the
  list API changes from spec 4. It is not built in parallel with spec 5.

## Acceptance

- Blocking a tracked URL from its row removes it from the tracked list, adds a rule to the
  blocked list, and requests for it now get the rule's status code with an empty body and
  are not tracked.
- A rule `/wp-admin/*` for all domains answers `/wp-admin/anything/below` on every domain
  with its status code. A rule scoped to one domain affects only that domain, and a rule
  scoped to a culture affects only that culture. Verified on the multi-site and
  multi-language profiles.
- Tracked records matching a new wildcard rule are deleted, and others are not.
- Unblocking a rule makes its URLs tracked again.
- A block wins when a redirect exists for the same URL.
- The backoffice, the Management API and the other Umbraco-owned paths are never blocked,
  and `*`, `/*` and `/**` are rejected.
- A rule change reaches all servers through the distributed cache, with a test for the
  refresher.
- The cache is snapshot based and filled at startup: tests prove that a request during
  startup never sees an empty cache, that a refresh swaps in a complete new snapshot while
  requests keep being served from the old one, and that a failed rebuild keeps the old
  snapshot.
- Both migration sets and the pending-model-changes check pass.
- `CLAUDE.md` documents the rule model, matching, the safeguards, the cache refresher and
  the new endpoints.

## Build decisions (agreed before planning)

- **Delivery:** one feature branch `feature/6-blocked-urls` off `develop`, one PR for the whole spec
  (backend, migrations, client, E2E, `CLAUDE.md`).
- **Entry point:** the block middleware is added by the existing `UsePageNotFoundTracker()` (before
  `UseUmbraco()`), together with the file 404 middleware. A site that never calls it gets no blocking; this is
  documented in `CLAUDE.md`.
- **Delete on create (decided with the user):** the pattern is translated to a SQL `LIKE` (`*` becomes `%`; literal
  `%`, `_`, `[` and `\` escaped with spec 4's `LikePattern`) and run as one `ExecuteDelete` against the normalised,
  lower-cased `RelativePath`, scoped by exact domain and culture (empty = all). A parity test runs the same patterns and
  URLs through this SQL and the in-memory matcher, on SQLite and (when configured) SQL Server.
- **Request matching (decided with the user):** each rule is compiled into literal segments split on `*`. The snapshot
  keeps rules pre-sorted by specificity (domain beats all domains, culture beats all cultures, longer literal prefix,
  then lowest status code), and a request scans them in order; the first match wins. No trie.
- **Row actions:** adding "Block" to the tracked URL rows turns the single inline "Create redirect" button into a
  dropdown; the selectors in `tests/redirect.spec.ts` are updated in this PR.
- **Queued hit discard:** `TrackingQueue` gets a pattern-based discard (path only, within domain and culture scope),
  used by the creating server (inside the flush lock, as spec 5) and by the refresher on every other server.

### Builder-check calls (writer's calls, not user decisions)

- **Domain matching in the middleware:** the domain-selection logic of spec 2 is extracted into a pure function over a
  list of domain entries. The snapshot holds that list (copied from `IDomainCache`, rebuilt on domain and language
  changes) and the middleware calls the same function, so "matched domain" means the same thing in both places and
  no domain lookup happens per request. A rule's domain is the exact lower-cased matched domain name as tracking
  stores it (including port and alias path); several domains with one name are told apart by culture as in spec 5.
- **Placement:** `UsePageNotFoundTracker()` already sits before `UseUmbraco()`, which is where Umbraco adds its static
  file handling, so the block middleware registers there. The integration tests use the existing startup filter and prove
  the order with a real file request and a redirect.
- **Decoding:** no second percent-decode at request time. The request path is already decoded by Kestrel, as in spec 3;
  patterns are normalised with the same normaliser, so the in-memory matcher and the `RelativePath` delete see the same
  text. A normalised pattern longer than 800 characters (the `RelativePath` column) is rejected with a message.
- **Queued hit discard:** compares the pattern against the hit's `RelativePath`, within the rule's domain and culture
  scope (the hit already carries all three), so no prefix stripping is repeated.
- **Created by:** not recorded; only the UTC timestamp.
- **Overlap with Umbraco-owned paths:** rejected when the pattern could match the owned path itself or anything under it
  (for example `/um*`, `*umbraco*`, `/*/umbraco`). The request-time exclusion of owned paths remains the real protection.
- **Initial load:** a request waits for the shared initial load for at most 5 seconds (and while persistence is
  `Pending`), then passes through with a warning; the load retries in the background every 30 seconds. With persistence
  `Unavailable` requests pass through immediately.
- **Blocked responses are never tracked,** including rules whose status is 404.

## Left to the builder

- Exact routes, models, entity type and dashboard names, icons and texts.
- How the pattern is translated for the delete query on each database.
- How the middleware is placed ahead of routing and the redirect handling.
- How domain matching is shared with spec 2's code.
- The cache refresher details and the in-memory structure for fast matching.
- Whether to record who created a rule.
