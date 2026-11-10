# Spec 10: Delete tracked URLs from the dashboard

Status: draft (issue #8, branch `feature/8-delete-tracked-urls`)
Depends on: spec 4 (dashboard), spec 5 (the remover), spec 6 (queue discard by predicate), spec 7 (documentation)
Series: see the table in `2026-11-01-solution-setup-design.md`

## Intent

Tracked URLs leave the list today only when an editor creates a redirect or a block rule for them. Irrelevant URLs (bot
noise, a typo that was fixed, a campaign that ended) stay forever, or until the retention job (spec 8) removes them after
`CleanupAfterDays`. This spec lets an editor remove tracked URLs from the dashboard directly: one row, the selected rows
of the page, or everything that matches the current search and filters.

## Goals

1. A **Delete** entity action on every tracked URL row (page and file rows), with a confirmation.
2. **Bulk delete**: rows can be selected in the table (current page only) and one confirmation deletes them.
3. **Delete all matching**: a collection action that deletes every row matching the current search, domain and culture
   filters (all rows when no filter is set), after a confirmation that states how many records are affected.
4. Deleting discards the hits still queued, pending, retrying or in flight for the deleted rows, so a flush cannot bring
   them back (the guarantee spec 5 gives for one row).
5. A deleted URL is tracked again from a count of one on its next 404.
6. The same access rule as the rest of the dashboard (`DashboardUserGroups`); deleting needs no stricter permission and no
   new setting (decided with the maintainer).
7. Three new Management API operations in the same OpenAPI document, with a regenerated client.
8. The public documentation (`docs/`) describes deleting in the same change.

## Non-goals

- No undo, no trash, no soft delete. A deleted row is gone; its counts and first-seen date are lost.
- No selection across pages. "Delete all matching" is the way to remove more than one page.
- No delete on the Blocked URLs dashboard (rules have Unblock).
- No new setting, no audit log, no new extension point. `ITrackedUrlRemover` is already replaceable; it gains methods.
- No exclusion list ("do not track this URL again"). That is what block rules are for.

## Anti-goals

- A delete that leaves a row behind because a queued hit was written back afterwards (on the server that handled the
  request).
- "Delete all matching" deleting rows the editor did not mean, for example because the server reads the filter differently
  from the list.
- A mass delete that holds the flush lock or a database transaction long enough to stall tracking.

## Constraints

- Umbraco 17.x and the layout from spec 1: contract in `.Core`, behaviour in `.Infrastructure`, endpoints in
  `.BackOffice`, actions and dialogs in `.BackOffice.Client`.
- No schema change and no migration. `check-pending-model-changes.ps1` must keep passing.
- The generated client is regenerated from the running site (`npm run generate-client`), committing only the real changes.
- Zero analyzer warnings; the docs, samples and the three documentation checks from spec 7 stay green; the docs change
  ships in the same pull request.
- SQL Server's 2100 parameter limit: deleting by id list stays at most 100 ids per call (the API's page size maximum).

## Approaches considered

- **A. Three operations over one remover. Chosen** (decided with the maintainer). `DELETE tracked-urls/{id}`,
  `POST tracked-urls/delete` with `{ ids }`, `POST tracked-urls/delete-matching` with the list's filter. All go through
  `ITrackedUrlRemover`, which takes the flush lock once per call, discards the queued hits and deletes in SQL. The
  matching delete reuses the list's filter code, so it cannot disagree with what the editor sees.
- B. One `POST tracked-urls/delete` with `{ ids }` or `{ filter }`. Rejected: a union type in the generated client and
  "both or neither" validation for no gain.
- C. The client loops over single deletes. Rejected: cannot cover "all matching" across pages without fetching every row,
  and takes the flush lock once per row.

The confirmation count for "delete all matching" is the list's current total. New 404s between opening and confirming can
add matching rows; the server deletes what matches at that moment and returns the real count (shown in the success toast).
A count check was rejected as more complexity than the race deserves.

## Behaviour

### Contract (Core)

`ITrackedUrlRemover` (existing `DeleteAsync(TrackedUrl)` stays) gains:

- `Task<int> DeleteAsync(IReadOnlyCollection<int> ids, CancellationToken ct)`: loads the identities of the rows, discards
  their queued hits, deletes by id; returns the number of rows deleted (ids already gone count 0).
- `Task<int> DeleteMatchingAsync(TrackedUrlFilter filter, CancellationToken ct)`: deletes every row matching the filter;
  returns the number of rows deleted.

`TrackedUrlFilter` (Core, public record): `Search`, `Domain` filter and `Culture` filter in the same shape as
`TrackedUrlQuery` uses (Any / Empty / Exactly). `TrackedUrlQuery` is refactored to carry or reuse it so the two cannot
drift.

### Infrastructure

- The list's `Filter(IQueryable<TrackedUrl>, ...)` is extracted into one shared internal helper used by
  `TrackedUrlQueryService` and the remover.
- Deleting by ids and by filter run inside `ITrackingFlusher.RunExclusiveAsync`. By ids: select the identities
  (`UrlHash`, `Domain`, `Culture`), `TrackingQueue.Discard` each, `ExecuteDelete` where id in the list.
- Delete matching: `TrackingQueue.DiscardMatching` with an in-memory predicate equal to the filter, then `ExecuteDelete` of
  the filtered query in one statement (no row loading). The predicate applies the same search rules as the list (the term
  percent-decoded once, lower-cased, slash rules, "contains" ordinal ignore case on the stored normalised `Url`; domain and
  culture compared as the list does). Parity between the SQL filter and the predicate is proven by a test as spec 6 does
  for block patterns.
- Cancellation after the delete statement has run is not propagated for the discard (as spec 6's post-commit steps).
- Hits for a deleted URL that arrive afterwards create a new row (legitimate). With several servers, another server's
  queued hits can recreate a row after the delete: accepted and documented, as in spec 5.

### Management API (BackOffice, base `/umbraco/pagenotfoundtracker/api/v1`, policy `PageNotFoundTracker.DashboardAccess`)

| Operation ID | Route | Body | Answers |
|---|---|---|---|
| `DeleteTrackedUrl` | `DELETE tracked-urls/{id:int}` | none | 200 `{ deleted: 1 }`; 404 problem when the row is gone; 503; 401/403 |
| `DeleteTrackedUrls` | `POST tracked-urls/delete` | `{ ids: int[] }` (1 to 100, distinct) | 200 `{ deleted }`; 400 for empty, more than 100 or invalid ids; 503; 401/403 |
| `DeleteMatchingTrackedUrls` | `POST tracked-urls/delete-matching` | `{ search?, domain?, noDomain?, culture?, noCulture? }` as the list's query | 200 `{ deleted }`; 400 (domain with noDomain, culture with noCulture); 503; 401/403 |

Validation follows the list endpoint. An empty filter body deletes everything. 503 when persistence is `Unavailable`;
`Pending` waits. Operation IDs are the action method names.

### Client

- **Row action** `Pnft.EntityAction.TrackedUrl.Delete`, kind `default`, weight **-20** (below Block -10), icon
  `icon-trash`, label `#pageNotFoundTracker_delete`, no condition (every row). It confirms with Umbraco's
  `umb-confirm-modal` (colour danger), calls `DeleteTrackedUrl`, reloads the collection and shows a success toast. A 404
  (already gone) reloads the list and shows the client's own text.
- **Selection**: the table's `allowSelection` becomes true for the tracked URL table only (the block rule table stays
  without). The collection toolbar shows a **Delete selected (n)** button when something is selected (`DeleteTrackedUrls`
  after a confirmation naming the count); selection clears after a delete and when the page, sort or filter changes.
- **Delete all matching**: a collection action next to the search box, label "Delete all matching", enabled when the
  list has at least one row. The confirmation names the total ("Delete 1,243 tracked URLs?") and, when a filter is set,
  says it applies to the current filter; with no filter it says it deletes **all** tracked URLs. Then
  `DeleteMatchingTrackedUrls` with the current filter; a success toast shows the real count.
- Texts in `localization/en.ts`, `data-mark` hooks in `collection/data-marks.ts` (the E2E tests keep a copy).
- The dashboard row layout changes again (three actions: Create redirect inline, Block and Delete in the dropdown; file
  and query-string rows show Block inline, Delete in the dropdown): the E2E selectors need rechecking.

## Testing

- Unit: remover (ids, filter, queue discard order, ids already gone), filter helper, controllers (200/400/404),
  parity test of SQL filter vs in-memory predicate.
- Integration: remover on SQLite and SQL Server (rows gone, queued hits not written back, count returned),
  `TrackedUrlsApiTests` for the three operations (200/400/401/403/404, SQLite), the OpenAPI document lists them.
- Vitest: the delete service mappings, the selection state, the confirmation texts, the manifests (Delete weight -20).
- E2E (`tests/delete.spec.ts`, single-site, run-scoped prefix): delete one row from the dropdown; select two and delete;
  delete all matching a search and only those; a deleted URL is tracked again from 1.
- Manual: multi-site-aliases filter by domain then delete matching; the other profiles as the maintainer asks.

## Left to the builder

Exact component split in the client, wording of the texts, the in-memory predicate's implementation, the order of the
toolbar controls, and how selection is wired into `PnftPagedTableElementBase`.

## Calls made without the maintainer (to check)

- Delete weight -20 and a danger-coloured confirmation.
- The selection is limited to the current page and the API caps `ids` at 100.
- No confirmation count check for "delete all matching".
- "Delete all matching" with no filter is allowed (deletes everything) rather than disabled.
