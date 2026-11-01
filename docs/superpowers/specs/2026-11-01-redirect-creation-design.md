# Spec 5: Redirect creation

Status: draft, awaiting builder check and approval
Depends on: spec 4 (dashboard), spec 3 (tracking), spec 2 (404 handling)
Series: see the table in `2026-11-01-solution-setup-design.md`

## Intent

A tracked 404 URL is only useful if someone can fix it. This spec adds a "Create redirect"
action to the rows of the dashboard from spec 4. The editor picks where the URL should go,
the redirect is created, and the URL leaves the dashboard.

How a redirect is created and what it can point to is replaceable. The default uses
Umbraco's own redirect management, so redirects show up in Umbraco's "Redirect URL
Management" dashboard and need no new request handling. Implementers can replace both the
backend part and the form in the backoffice, for example to redirect to external URLs or to
choose a status code.

## Goals

1. A "Create redirect" entity action on dashboard rows.
2. A backend contract for creating a redirect, replaceable through DI, with a default
   implementation on Umbraco's `IRedirectUrlService`.
3. A package-defined backoffice extension type for the redirect form, with a default form
   that picks a content page. Implementers register their own form to replace it.
4. After a redirect is created successfully, the tracked record is deleted and the dashboard
   list refreshes.
5. Rows the active provider cannot handle do not offer the action.

## Non-goals

- No blocking of URLs (spec 6).
- No redirects for file URLs in the default provider. Umbraco applies its built-in
  redirects during page routing, and requests for missing files never reach it, so such a
  redirect would be stored and never applied. Spec 2 is not amended. A custom provider can
  support files itself.
- No management of existing redirects. Umbraco's own dashboard does that.
- No bulk creation.
- No redirect to external URLs or custom status codes in the default provider.
- No undo of the deletion of the tracked record.

## Anti-goals

- Silent failure: a redirect that is stored but can never be applied, or a tracked URL that
  is deleted when no redirect was created.
- A custom provider that has to re-implement the plumbing: the action, the endpoint,
  permissions, deleting the record and refreshing the list stay with the package.
- Fixed request fields that only make sense for the default provider.

## Constraints

- Umbraco 17.x and the layout from spec 1: contracts in `.Core`, default provider in
  `.Infrastructure`, endpoint in `.BackOffice`, extension type, default form and entity
  action in `.BackOffice.Client`.
- Builds on spec 4's tracked URL entity type, the dashboard collection and its OpenAPI
  document and generated client (regenerated here for the new endpoint).
- StyleCop and the other conventions from spec 1 apply.

## Approaches considered

Storage and handling of redirects (decided with the user): configurable, with Umbraco's
redirect management as the default.

Pluggable UI and backend (decided with the user):

- **A. Package-defined extension type plus an opaque payload. Chosen.** The package keeps
  the entity action and API. The form returns a JSON payload, the API passes it to the
  backend contract, and on success the package deletes the tracked record.
- B. Implementers override the action and bring their own API. Rejected: every implementer
  would redo the plumbing, including deleting the record.

Files (decided with the user): the default provider handles pages only. Extending spec 2's
file middleware to apply redirects was considered and not chosen for this version.

## Behaviour

### Flow

1. The editor opens the action on a row. A modal shows the registered redirect form for the
   row's URL.
2. The editor completes the form and confirms.
3. The package's endpoint calls the redirect contract with the tracked URL (URL, domain,
   culture, page or file) and the form's payload.
4. If the provider reports success, the package deletes the tracked record, discards any
   queued hits for that URL (see below), and the dashboard refreshes. The editor sees a
   success message.
5. If the provider reports failure, nothing is deleted. The editor sees the reason.

### Backend contract

A contract in `.Core` with, at least:

- whether this provider can create a redirect for a given tracked URL (used to hide the
  action on rows that are not supported),
- creating the redirect from a tracked URL and an opaque payload, returning success or a
  failure with a message the editor can read.

Exact members are left to the builder. A custom implementation replaces the default through
DI, as with the resolver in spec 2.

### Default provider

- Supports pages only. Tracked URLs flagged as files are not supported.
- Payload: the key of the target content page.
- Creates the redirect through `IRedirectUrlService`, so Umbraco applies and lists it.
- Mapping from the tracked record to Umbraco's redirect format. Umbraco looks a redirect up by
  route. For a request that matched a domain, the route is the ID of that domain's root node
  followed by the path relative to the domain. For a request that matched no domain, the route is
  the bare path. The builder verifies both forms against Umbraco 17, in particular the no-domain
  form, because `single-site` may have no configured domain. The record carries `Domain`,
  `Culture` and the `RelativePath` from spec 3. With a non-empty `Domain` the provider finds the
  domain by name, takes its root node and builds the route from the root ID and `RelativePath`.
  With an empty `Domain` it builds the bare route from `RelativePath`. If a domain or root cannot
  be found, the provider fails with a clear message and nothing changes.
- Culture is taken from the record. A record with no culture uses the culture-invariant
  form.
- If a redirect already exists for the same route and culture, the provider replaces its
  target and reports success. Creating the same redirect twice is safe.
- Query strings: with `IncludeQueryString` on (spec 3), a tracked URL can carry a query
  string. The builder verifies whether Umbraco's redirect lookup honours it. If it does
  not, the provider must fail with a clear message for such rows rather than create a
  redirect that drops or ignores the query.

### Backoffice extension type

- The package registers a new extension type for the redirect form. An implementer adds
  a form of that type to replace the default. If several are registered, the one with the
  highest weight wins. (Writer's call.)
- A form receives the tracked URL's data and returns the payload when the user confirms.
  It does not call the API itself. The package's modal does.
- The default form shows a content picker for the target page, with validation, and a
  summary of the tracked URL and what will happen.

### The action on rows

- It is an entity action on spec 4's tracked URL entity type.
- The list API from spec 4 gains a per-row flag saying whether the active provider supports
  creating a redirect for that row, so unsupported rows do not show the action.
- Visibility follows the same user groups as the dashboard. Creating a redirect needs no
  further permission in this version.

### Endpoint

- One endpoint to create a redirect from a tracked record identifier and the payload,
  in the same OpenAPI document as spec 4, with the same access rule as the dashboard.
- Responses distinguish: created, provider failure with a message, record not found, and
  not supported.
- Exact route and models are left to the builder.

### Deleting the record and the queue

- Deleting removes the tracked record from the database.
- Spec 3 queues hits in memory before they are written. To stop a queued hit from writing
  the record back at the next flush, the tracker gets a way to discard queued entries for a
  URL identity. This is best effort within the current server. On other servers, a hit already queued can still bring the record back once. This is accepted
  in this spec. Spec 6 adds cross-server discarding through its cache refresher, and that
  mechanism may be applied to this action as well.
- If the provider succeeded but deleting the record fails, the editor is told the redirect
  was created and the record could not be removed. The failure is logged. The record stays,
  and since the URL now redirects, its count stops growing.
- If the redirect is removed later in Umbraco and the URL returns 404 again, it is tracked
  as a new record from a count of one.

## Testing

- **Unit tests:** the default provider's mapping (domain with and without path, no domain,
  culture, unknown domain, existing redirect replaced, file rows unsupported, query string
  cases), the endpoint's response cases, and the delete and queue discard behaviour with a
  fake provider.
- **Integration tests:** with a real Umbraco instance and database, creating a redirect for a
  tracked page URL results in Umbraco redirect data for the right root and culture, the
  tracked record is gone, and a request for that URL redirects. On SQLite and SQL Server.
  A fake provider registered by an implementer is used instead of the default.
- **E2E tests:** on `single-site` in CI: create a redirect from a dashboard row through
  the default form and see the row disappear.
- Manual check on the multi-site and multi-language profiles that redirects land on the right
  site and culture, including the alias profile where the stored path has a prefix.

## Calls made in this spec

These are the writer's calls, not decisions the user made:

- The highest weight wins when several redirect forms are registered.
- The action uses the dashboard's access rule, with no extra permission.
- The default provider replaces the target of an existing redirect instead of failing.
- Failed or unsupported cases surface as readable messages, never as a silent success.
- If creating succeeded and deleting failed, the user is told both facts.
- Hits queued on other servers can bring a deleted record back once. Not handled.
- A failing mapping or a query string Umbraco cannot honour fails the creation.
- The row flag for "supported" comes from the provider, not from the client.
- Order against spec 2: Umbraco's redirect finder is one of the normal content finders and
  runs before the last-chance step that spec 2 uses, so a redirect applies before any 404
  page is resolved. The builder confirms this with an integration test and treats a failure
  as a blocker for the default provider. The default provider also fails with a clear
  message when Umbraco's redirect handling is switched off in configuration (the builder
  finds the setting name), because a redirect created then would never be applied.
- Same-server race: the flush from spec 3 and the delete plus queue discard are serialised
  on one server, so a flush in progress cannot write the record back. The identity is the
  same as in spec 3 (normalised URL, domain and culture).
- Domain mapping: the provider reuses spec 2's domain resolution code where it can, which
  may move shared code into `.Core`. When several Umbraco domains share a name, the
  record's culture picks one. No match means failure with a clear message. The path used is `RelativePath` from spec 3, so no prefix stripping happens here.
- The "supported" flag depends only on the record itself (page or file, and whether a
  query string is present and honoured by Umbraco), never on a per-row database or domain
  lookup, so a page of 100 rows stays cheap. A mapping problem shows up as a readable error
  when the editor confirms. Rows with a query string that Umbraco does not honour are
  flagged unsupported and show no action.
- Validation: the default provider checks that the target page exists and is published, and
  fails with a message otherwise. "Provider cannot handle" and "provider failed" share one
  response shape with a message.
- Picker scope: the default form's picker is not restricted to the record's site or
  culture. Any content the user can access can be chosen.
- Access: the same user groups as the dashboard, with no extra permission, as a first
  version. This is security relevant, so it is listed here on purpose.
- Refreshing the list after success is part of this spec. Touching spec 4's collection code
  and repository for that, and for the supported flag, is in scope.

## Acceptance

- On `single-site`, a tracked page URL gets a "Create redirect" action. Picking a content
  page creates a redirect that Umbraco applies and lists in its own dashboard, the row
  disappears, and the URL now redirects to that page.
- Tracked file URLs do not show the action with the default provider.
- On the multi-site and multi-language profiles, the redirect belongs to the right site and
  culture, including sites told apart by an alias path.
- An error from the provider is shown and the tracked record stays.
- A custom provider and a custom form, registered by an implementer, replace the
  defaults, proven by tests, and `CLAUDE.md` shows a minimal example of each.
- Regenerating the TypeScript client for the new endpoint is documented.

## Left to the builder

- Exact contract members, endpoint route and models, and payload shape of the default form.
- How the redirect form extension type and its modal are built and registered.
- How the supported flag is added to the list API and read by the action.
- Whether Umbraco honours query strings in redirects, and the resulting behaviour.
- How queued entries are discarded in the tracker from spec 3.
