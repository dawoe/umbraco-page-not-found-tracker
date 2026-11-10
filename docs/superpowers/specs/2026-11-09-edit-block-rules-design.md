# Spec 9: Edit block rules

Status: implemented (issue #11, branch `feature/11-edit-block-rules`)
Depends on: spec 6 (blocked URLs), spec 7 (documentation)
Series: see the table in `2026-11-01-solution-setup-design.md`

## Intent

Spec 6 only adds and removes block rules. Changing a rule's status code, widening `/wp-login.php` to `/wp-*.php` or
scoping a rule to one domain means unblocking it and adding a new one: two confirmations, a gap in which the URL is
answered normally again, and the rule's creation date is lost. This spec lets an editor change a rule in place: pattern,
status code, domain and culture, in the dialog they already know from "Block" and "Add rule".

## Goals

1. An **Edit** action on every row of the Blocked URLs dashboard that opens the block rule dialog prefilled with the
   rule's values and saves the change to the same rule.
2. The same validation and safeguards as creating a rule: the Umbraco-owned path check, the "no character besides `/`
   and `*`" check, the status code range, the length limits, the culture canonicalisation.
3. A change that makes the rule identical to another rule (same normalised pattern, domain and culture) is refused with
   a message in the dialog; nothing is saved (decided with the maintainer).
4. After a save the rule is enforced on every server, the tracked URLs the changed rule covers are deleted and the
   queued hits it covers are discarded, exactly as after creating a rule.
5. One new Management API endpoint for the update, in the same OpenAPI document, with a regenerated client.
6. The public documentation (`docs/`) describes editing in the same change.

## Non-goals

- No change to what a rule is: a new field, a rule history or an "updated" timestamp are not added. `Created` keeps its
  meaning (when the rule was first saved) and is not touched by an edit.
- No merging of rules: a change that collides with another rule is refused, never folded into that rule.
- No change to "Block" and "Add rule": creating a rule whose identity already exists still only updates that rule's
  status code (spec 6). Only the edit path refuses a collision, because there the editor is holding a different rule.
- No bulk edit, no reordering, no import or export (issues #9 and later).
- No new extension point. `IBlockRuleService` gains a method; it is already replaceable as a whole.

## Anti-goals

- An edit that leaves two rules with the same identity in the table, or that leaves the stored rule and the enforced
  rule out of step on any server.
- A failed save that loses the rule. The rule the editor started from must still exist after any failure before the
  database write.
- A rule silently disappearing because the editor changed another one.

## Constraints

- Umbraco 17.x and the layout from spec 1: the contract change in `.Core`, the service in `.Infrastructure`, the
  endpoint in `.BackOffice`, the action and the dialog change in `.BackOffice.Client`.
- No schema change: the `pageNotFoundTrackerBlockRule` table, its unique index on (`PatternHash`, `Domain`, `Culture`)
  and the migrations stay as they are. `check-pending-model-changes.ps1` must keep passing without a new migration.
- The generated API client is regenerated from the running site (spec 4's `npm run generate-client`), committing only the
  real changes (`index.ts`, `sdk.gen.ts`, `types.gen.ts`).
- The StyleCop, zero-warning and documentation-check conventions from specs 1 and 7 apply. The docs change ships in the
  same pull request.

## Approaches considered

- **A. A real update path: `UpdateAsync` on `IBlockRuleService` and `PUT block-rules/{id}`. Chosen** (decided with the
  maintainer). The service loads the rule by id, validates the new values as create does, refuses a collision with
  another rule, updates the row in place and then runs the tail create already has (refresh, notify, discard, delete).
  The dialog gets an optional rule id and calls the update endpoint. Keeps the identity and the creation date, refuses
  the collision before anything is written, reuses the create tail without bending it.
- B. Client-side delete then create. Rejected: a failed create after the delete loses the rule, `Created` resets, the
  collision cannot be refused before the old rule is gone, and other servers get two refreshes.
- C. Make create an upsert by id (`POST block-rules` with an optional id). Rejected: the POST would have two different
  "existing rule" meanings (same identity updates the status, given id updates everything), a 404 on a create is odd,
  and the generated client function would still be called create.

What a collision does (decided with the maintainer): refused with an inline message, the dialog stays open. Folding the
edited rule into the existing one was the alternative; it was rejected because it silently removes a rule the editor
did not touch.

Row layout (decided with the maintainer): **Edit** is the inline button, **Unblock** moves into the "..." menu. This
mirrors the tracked list, where the constructive action (Create redirect) is the button and Block is in the menu, and
puts the destructive action one click further away.

## Behaviour

### The service

`IBlockRuleService` (Core, public) gets:

```text
Task<CreateBlockRuleOutcome> UpdateAsync(int id, string pattern, int statusCode, string domain, string culture, CancellationToken ct)
```

`CreateBlockRuleStatus` gets two values, `NotFound` and `Conflict`; `CreateAsync` never returns them (writer's call: the
outcome type and the enum keep their names, the XML documentation says which statuses each method can return; renaming
them would touch `CLAUDE.md`, the tests and the controller for no behavioural gain).

Steps of `UpdateAsync`, in order:

1. Normalise and canonicalise the input exactly as `CreateAsync` does: trimmed display pattern, `BlockPatternNormalizer`,
   lower-cased trimmed domain, canonical culture (the Umbraco language ISO code, else the .NET culture name, else
   lower-case).
2. Validate with `BlockPatternValidator.Validate` and the scope length checks. A failure gives `Invalid` with the
   message; nothing is read or written.
3. Load the rule by id. Missing gives `NotFound`.
4. Collision check: another rule (`Id != id`) with the new `PatternHash`, `Domain` and `Culture` gives `Conflict` with
   the message `BlockingConstants.RuleExistsMessage`, a new constant: "A rule with this pattern, domain and culture
   already exists." The rule itself is not a collision: changing only the status code, or only the spelling of the
   pattern (`/WP-Admin/*` to `/wp-admin/*`), is allowed and updates the row.
5. Update the row in place with one `ExecuteUpdate` by id: `Pattern`, `PatternHash`, `DisplayPattern` (the new trimmed
   input, truncated to 800 as create does), `StatusCode`, `Domain`, `Culture`. `Id` and `Created` do not change. A
   unique-index violation here (another server created or edited a rule into the same identity between step 4 and
   step 5) is answered as `Conflict`, with nothing changed. `ExecuteUpdate` bypasses the change tracker, so the
   violation arrives as the provider's own exception (`SqliteException`, `SqlException`), not as `DbUpdateException`,
   and Infrastructure references no provider package: the service catches `DbException`, re-runs the collision query
   of step 4, and answers `Conflict` when a colliding row exists, else rethrows (a connection failure is never reported
   as a conflict). A zero-row update (the rule was deleted between step 3 and step 5) gives `NotFound`.
6. From here the request token is ignored (`CancellationToken.None`), as in create: the change is committed and a client
   abort must not leave the stored and the enforced rules out of step. `RefreshAsync` on this server's snapshot, then
   notify every server with **one** `BlockRuleCachePayload(newPattern, newDomain, newCulture, Saved)`. The refresher
   rebuilds the whole snapshot from the table, so the old pattern stops being enforced everywhere without a payload of
   its own; the `Saved` payload makes every server discard the queued hits the **new** pattern covers. A failing
   notifier is a warning, as in create.
7. Inside `ITrackingFlusher.RunExclusiveAsync`: `TrackingQueue.DiscardMatching` for the new pattern and scope, then the
   `WhereMatches` `ExecuteDelete` of the tracked URLs the new pattern covers. A failure keeps the rule and returns
   `Saved` with `RecordsRemoved: false` and `BlockingConstants.RecordsNotRemovedMessage`, as in create.

What the editor sees afterwards: requests matching the new pattern get the new status code with an empty body;
requests the old pattern blocked but the new one does not are answered normally again and tracked again from one, as
after an unblock. Tracked rows the old pattern covered were already deleted when that rule was saved, so narrowing a
pattern deletes nothing new; widening deletes the rows the wider pattern now covers.

The create tail (steps 6 and 7) is shared code between `CreateAsync` and `UpdateAsync` (one private method). Create's
behaviour does not change.

### The endpoint

BackOffice, base `/umbraco/pagenotfoundtracker/api/v1`, policy `PageNotFoundTracker.DashboardAccess`, OpenAPI tag
`Tracked URL`, same document:

| Operation ID | Route | Body | Answers |
|---|---|---|---|
| `UpdateBlockRule` | `PUT block-rules/{id:int}` | `{ pattern, statusCode, domain?, culture? }` (the create body) | 200 `{ recordsRemoved, message }` (the create answer); 400 (blank pattern, malformed body, or the validator's message as problem `detail`); 404 problem (no rule with that id); 409 problem (`detail` = `RuleExistsMessage`); 503 unavailable; 401 anonymous; 403 outside the groups |

The request and response models are the existing `CreateBlockRuleRequestModel` and `CreateBlockRuleResponseModel`
(writer's call: one body shape for create and update keeps the client's `toCreateRuleRequest` and the generated types
as they are; the OpenAPI document shows both operations with the same schemas). Validation order as in the create
controller: the blank-pattern check, then the persistence gate (503), then the service.

### The client

- A new entity action `Pnft.EntityAction.BlockRule.Edit` (`src/blocking/edit-rule.action.ts`, manifest in
  `src/blocking/manifests.ts`), `forEntityTypes: [PNFT_BLOCK_RULE_ENTITY_TYPE]`, **weight 0**, icon `icon-edit`, label
  `#pageNotFoundTracker_editRule`. The Unblock action's weight becomes **-10**. With two actions Umbraco renders the
  highest weight inline and the rest in the `umb-entity-actions-dropdown`, so Edit is the row's button and Unblock sits
  in the "..." menu (see spec 6, Row actions, for the 17.8 behaviour).
- The action reads the rule from `PNFT_BLOCK_RULE_COLLECTION_CONTEXT` by `unique` (as Unblock does) and opens the rule
  modal through `openRuleModalAndReload` with the rule's `pattern` (the display spelling), `domain`, `culture` and
  `statusCode`, the title `#pageNotFoundTracker_editRuleHeadline` and the new `ruleId`, then reloads the blocked list.
- `PnftBlockRuleModalData` gets an optional `ruleId?: number`. With it the modal is in edit mode: the Confirm button's
  label is `#pageNotFoundTracker_saveRule` ("Save") instead of "Block", and Confirm calls `updateBlockRule({ path: { id },
  body })` instead of `createBlockRule`. Everything else (fields, the scope options, the required check, the static
  warning, the inline error, Confirm disabled while sending, the close value `{ saved, recordsRemoved, message }`) is
  unchanged. The status select offers 403, 404 and 410; a rule whose status was set to another 4xx code through the API
  is shown with that code added as a fourth option (its bare number as the text, since there is no
  `pageNotFoundTracker_status<code>` term for it), so the preselection exists and saving without touching the status
  keeps it (writer's call, mirrors `buildScopeChoices` for a removed domain; a pure `buildStatusChoices(code)` in
  `rule-form.ts`, Vitest-covered).
- `block-rule.service.ts`: `updateRule(send, id, request)` next to `createRule`, mapping the answer as create does, plus a
  new result kind `conflict` with the server message for a 409 (shown inline, the modal stays open); 404 (`notFound`)
  in edit mode shows the client's own text inline (Umbraco's error interceptor replaces 404 problem details). The modal
  rejects on Cancel without a value, so `openRuleModalAndReload` cannot tell a plain cancel from a cancel after a 404:
  in edit mode (`data.ruleId` set) it reloads the list on **any** cancel, so a gone row disappears; in create mode
  cancelling keeps doing nothing (writer's call).
- The success toast after an edit uses the existing `pageNotFoundTracker_blockSavedHeadline` ("Rule saved") with the
  message `#pageNotFoundTracker_ruleUpdated` ("The rule has been updated. Matching requests are blocked and no longer
  tracked."); `openRuleModalAndReload` picks the text from `data.ruleId`, the modal's close value stays as it is. The
  records-not-removed warning is the same as for create.
- Texts in `localization/en.ts`: `editRule` ("Edit"), `editRuleHeadline` ("Edit block rule"), `saveRule` ("Save"),
  `ruleUpdated`, `blockRuleExists` (fallback when a 409 has no readable detail). Exact wording is left to the builder.
- Data marks: Umbraco's own `entity-action:Pnft.EntityAction.BlockRule.Edit`; the modal keeps its marks. The E2E copy
  in `tests/blocked.spec.ts` changes with the Unblock move (the helper must open the row's dropdown first, like
  `clickBlock` does on page rows).

### Multi-server behaviour

As spec 6: the saving server refreshes its own snapshot and discards its own queued hits before answering; every other
server gets the `Saved` payload through the distributed cache, rebuilds its snapshot from the table and discards the
queued hits the new pattern covers. The accepted limits of spec 6 stay: a hit that passed the middleware on another
server just before its refresh can be written back as a count-1 row; the discard on other servers is best effort.

## Documentation

Updated in the same change, following spec 7's ownership rules:

- `docs/blocking.md`: an "Edit a rule" part under "Add a rule" (what can be changed, that `Created` stays, that the
  URLs the old pattern covered are answered and tracked again, that the tracked URLs the new pattern covers are
  removed), and the collision message under "Safeguards".
- `docs/dashboards.md`: the Blocked URLs row actions: **Edit** (button) and **Unblock** (in the "..." menu).
- `docs/extending/dashboard-actions.md`: the package's actions and weights on block rule rows (Edit 0, Unblock -10),
  and what a custom action's weight does on those rows now.
- `docs/how-it-works.md`, "How redirects and blocks relate": one sentence that a changed block applies at once and the
  URLs it no longer covers are tracked again.
- `CLAUDE.md`, Blocked URLs: the update method, the endpoint, the two new statuses, the row layout, the E2E change.
- No new setting, so `configuration.md` and `ConfigurationDocsTests` are untouched; no sample is quoted, so
  `check-doc-samples.ps1` is untouched.

## Testing

- **Unit** (`Infrastructure.Tests/Blocking/BlockRuleServiceTests`): update saves the new values and keeps `Id` and
  `Created`; update of an unknown id is `NotFound`; an invalid pattern or status is `Invalid` and changes nothing;
  a collision with another rule is `Conflict` and changes nothing; changing only the status code or only the spelling
  of the same rule is `Saved`; the culture is canonicalised; the tail runs in order (refresh, notify one `Saved` payload
  with the new values, discard, delete) and the delete covers the new pattern; a delete failure keeps the rule and
  returns `RecordsRemoved: false`; a cancelled request after the write still refreshes, notifies and removes records;
  a unique violation on the write is `Conflict`. `BackOffice.Tests/BlockRuleControllersTests`: the PUT maps 200, 400
  (blank pattern, invalid), 404, 409 and 503.
- **Integration**, SQLite and SQL Server (`Integration.Tests/Blocking/BlockRuleServiceTests`): update changes the row,
  the `LIKE` delete removes the tracked rows the new pattern covers and keeps the others, a collision is refused and the
  table is unchanged. `Blocking/BlockMiddlewareIntegrationTests`: after an edit the old path is answered normally (and
  tracked) and the new path gets the new status with no body. `ManagementApi/BlockRulesApiTests`: PUT 200, 400, 404,
  409, 401, 403 with real backoffice tokens.
- **Vitest**: `block-rule.service` (`updateRule`, the 409 mapping), `manifests` (new assertions in the existing test:
  Edit weight 0 above Unblock -10, both on the block rule entity type), `rule-form` (`buildStatusChoices` when the
  rule's code is not one of the three).
- **E2E** (`tests/blocked.spec.ts`, single-site, `FlushInterval=00:00:01`, run-scoped prefix): add a rule, edit its
  pattern and status through the row button, the blocked list shows the new pattern, the old path answers 404 and the
  new path the new status with an empty body; editing a rule into another rule's identity shows the message inline and
  the dialog stays open; the existing tests keep passing with Unblock in the dropdown.
- **Manual check** on `multi-site-aliases`: edit an all-domains rule to one domain and see the other domain answered
  normally again; recorded in `CLAUDE.md` as the spec 6 checks are.

## Acceptance

- A rule's pattern, status code, domain and culture can be changed from its row in the Blocked URLs dashboard; the row
  keeps its id and creation date.
- After the save, the new pattern is enforced on every server with the new status code, the URLs it covers are gone
  from the tracked list, and the URLs the old pattern covered are answered and tracked again.
- Changing a rule into another rule's pattern and scope is refused with a message in the dialog, and nothing changes.
- The same invalid patterns and status codes are refused as when creating a rule.
- `PUT block-rules/{id}` answers 200, 400, 404, 409, 503, 401 and 403 as specified, and the generated client has
  `updateBlockRule`.
- Edit is the row's button, Unblock is in the "..." menu; the E2E tests pass.
- No new migration; `check-pending-model-changes.ps1` passes.
- The documentation pages above describe editing, and the three documentation checks pass.

## Left to the builder

- Exact texts, the icon, the shape of the shared create tail in the service, and the order of the new tests within the
  existing fixtures.
- Whether the modal's edit mode is a flag read from `data.ruleId` or a small separate submit function; both are fine.
- How the integration test provokes the unique violation on the write (a fake notifier or a direct insert between the
  check and the write), or whether that case stays unit-tested only.
