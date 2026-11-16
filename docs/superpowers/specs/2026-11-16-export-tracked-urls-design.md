# Export tracked URLs: design (spec 11, issue #9)

## Intent and why

Teams want to review 404s in a spreadsheet, hand them to SEO or content colleagues, or compare them with a redirect
list. The dashboard only shows 20 to 100 rows at a time. An "Export" action on the tracked URLs dashboard downloads
the **whole** result of the current search, filters and sort as a CSV file.

The people who open the file use **Excel**, double-clicking the download. That drives the format: Excel reads a CSV as
UTF-8 only with a byte order mark, and splits columns on the Windows regional list separator.

## Goals

- One click on the dashboard downloads every row matching the current search, domain filter, culture filter and sort
  (not only the visible page).
- The file opens correctly in Excel by double-click: UTF-8 with BOM, `;` as separator, dates Excel recognises as dates.
- The server streams the rows from the database to the response, so a large table is never held in memory.
- Same access rule as the dashboard (`PageNotFoundTracker.DashboardAccess`).
- No tracked URL can run a formula in Excel (formula injection).

## Non-goals

- Other formats (XLSX, JSON), a configurable separator or encoding, selecting columns or rows, an export of the blocked
  URLs list, scheduled or emailed exports.
- A row limit. Excel itself stops at 1,048,576 rows; the documentation says so.
- Import, or an export that guarantees a snapshot (see Constraints).

## Anti-goals (a failure even if it works)

- A truncated file that looks complete after a failure half-way.
- Loading the table into memory, or a request that holds a database connection open without the client reading.
- A dashboard export that reaches data the list does not (different access, different filters).
- Text that Excel evaluates as a formula.

## Constraints

- The backoffice client authenticates with a bearer token, so the browser cannot follow a plain link. The client calls
  the endpoint with the token and saves the response as a file.
- Rows can change while the stream runs (a flush adds hits). The order always ends on ascending `Id`, so no row is
  repeated or skipped by ordering, but the file is not a point-in-time snapshot. Accepted and documented.
- Once the response has started, the status is fixed. A failure mid-stream aborts the connection, so the client sees a
  failed download, never a short file reported as success.
- Layering as in the rest of the package: contracts in Core, behaviour in Infrastructure, endpoint in BackOffice.
  Sibling projects are `PrivateAssets=all`: a new package reference must be mirrored in the entry csproj (none is
  expected).
- A change to behaviour that a docs page describes updates the page in the same change (CLAUDE.md, Documentation).

## Approaches considered

**Download mechanism.**
- A. The client calls a new authenticated endpoint with the bearer token and saves the response as a file. No new state
  or auth. The browser holds the finished file before saving, which is fine for CSV. **Chosen.**
- B. A one-time download ticket and a plain link, so the browser streams natively. Needs a ticket store that works across
  servers. Rejected: too much for CSV.

**Separator and encoding** (picked in the conversation).
1. Fixed `;`, UTF-8 BOM. **Chosen:** the Excel users are in `;` regions.
2. Fixed `,`, UTF-8 BOM. Rejected: one long column in a `;` locale.
3. The editor's backoffice culture decides, with a setting to override. Rejected by the maintainer as more than needed.

## Decisions

**Endpoint** (BackOffice, base `/umbraco/pagenotfoundtracker/api/v1`, policy `PageNotFoundTracker.DashboardAccess`,
tag `Tracked URL`, same OpenAPI document): `GET tracked-urls/export`, operation ID `ExportTrackedUrls`
(`ExportTrackedUrlsController`, derives `TrackedUrlsControllerBase`).

- Query: `orderBy`, `direction`, `search`, `domain`, `noDomain`, `culture`, `noCulture`, defaults and validation exactly
  as `GetTrackedUrls` (400 for an unknown column, `domain` with `noDomain`, `culture` with `noCulture`). No `skip` and
  `take`. The validation and the mapping to a filter are shared with the list controller, not copied.
- Answers: 200 `text/csv; charset=utf-8` with `Content-Disposition: attachment; filename="page-not-found-<yyyyMMdd-HHmmss>.csv"`
  (UTC); 400 problem details; 401/403 as the other endpoints; 503 problem details when persistence is `Unavailable`
  (while `Pending` the request waits, as the list does). 400 and 503 are decided **before** the first byte is written.
- Streaming: the controller writes to the response body as rows arrive and flushes in chunks; the request's cancellation
  token ends the query when the client goes away. A database error after the first byte is logged and aborts the
  connection (`HttpContext.Abort()`).

**Reading the rows.** A streaming read over the same filter and order as `GetPageAsync`: `AsNoTracking`, the existing
`Filter`/`Order` logic reused (extracted to a shared internal helper), enumerated asynchronously with one database
command and no `Skip`/`Take`, the `DbContext` held for the duration of the enumeration. It takes a new record
`TrackedUrlExportQuery(SortColumn, Descending, Search, Domain, Culture)`. **My call, to confirm in the plan:** the
existing public `ITrackedUrlQueryService` is replaceable, so adding a member would break an implementer. The builder
adds a separate seam instead (`ITrackedUrlExporter`, `StreamAsync(TrackedUrlExportQuery, ct)` returning
`IAsyncEnumerable<TrackedUrl>`, `AddUnique`). It stays internal if BackOffice can see Infrastructure's internals
(`InternalsVisibleTo`); if not, it is public in Core and then is an extension point and gets a documentation page.

**CSV format** (`TrackedUrlCsvWriter`, Infrastructure or BackOffice, pure and unit-tested):
- UTF-8 with BOM (`EF BB BF`), written once at the start.
- Separator `;`, line ending `\r\n`, a header row, one row per tracked URL.
- Quoting: a field containing `;`, `"`, CR or LF is wrapped in double quotes, with `"` doubled.
- Columns, in this order, header text fixed in English (the server has no backoffice language): `Url` (the
  `DisplayUrl`), `Relative path`, `Domain`, `Culture`, `Type` (`Page` or `File`), `First seen (UTC)`, `Last seen (UTC)`,
  `Hits`. `Relative path` is an addition to the list's columns, for comparing with redirect lists.
- Timestamps `yyyy-MM-dd HH:mm:ss` in UTC, invariant culture. This is a deliberate choice over ISO 8601 with `T` and `Z`:
  Excel reads the former as a date and the latter as text.
- `Hits` is a plain integer (invariant, no grouping).
- Formula injection: a text field whose first character is `=`, `+`, `-`, `@`, tab or CR gets a leading `'`. Tracked
  URLs, domains and cultures are request-derived input, so every text column is covered, not only `Url`.
- Empty domain and culture are empty cells.

**Client** (`src/export/`):
- A collection action `Pnft.CollectionAction.TrackedUrl.Export` (kind `button`, as Add rule), registered for the tracked
  URL collection only (`UMB_COLLECTION_ALIAS_CONDITION` on `Pnft.Collection.TrackedUrl`), label
  `#pageNotFoundTracker_exportAction`, data-mark per `collection-action:<alias>`.
- On execute it reads the collection context's current filter, maps it with the existing `buildListQuery` minus paging,
  calls the generated client (`exportTrackedUrls`, response parsed as a blob; regenerate the client, commit only the real
  changes), and saves the blob under the server's file name (fallback `page-not-found.csv`) with an object URL and an
  anchor, revoking the URL afterwards.
- While running, a second click does nothing (guard in the action) and a "Preparing the export" toast is not needed; on
  success a success toast; on failure (503, 403, network) a warning toast with a text for the case, never the raw body.
- Texts in `localization/en.ts`, `pageNotFoundTracker_*` keys.

**Access.** The policy is the same as the dashboard's; the action is registered under the same condition as the
collection, so nobody who cannot see the dashboard sees the button.

## Documentation

- `docs/dashboards.md`: an "Export" section: what is exported (everything matching search, filters and sort), the file
  format (Excel by double-click, `;`, UTF-8 with BOM, UTC dates, the column list), the 1,048,576-row Excel limit, that
  the file is not a snapshot, and that it needs the dashboard access.
- `docs/troubleshooting.md`: one entry if Excel shows one column (a regional list separator of `,`; Excel's "From
  Text/CSV" import).
- If the exporter seam turns out public: a page under `docs/extending/` and a link from its README and `docs/README.md`.
- `CLAUDE.md`: add spec 11 and its plan to the lists, an "Export (spec 11)" section (endpoint, format, tests, manual
  checks), the new files in the solution layout, and the new data-marks.
- Any quoted sample is updated with its compiled copy; `check-doc-samples.ps1` and `check-doc-links.ps1` pass.

## Tests

- Unit: `TrackedUrlCsvWriterTests` (BOM once, header, separator and quoting for each special character, line endings,
  formula prefixes on every text column, date and number format under a non-invariant current culture, empty domain and
  culture, a file row); `ExportTrackedUrlsControllerTests` (400 cases, 503, filter mapping, headers); client Vitest for
  the query mapping without paging and the file name parsing.
- Integration: the streaming read returns exactly the rows `GetPageAsync` would return across all pages, for every sort
  column in both directions and with search and filters, on SQLite and (with `PNFT_SQLSERVER_CONNECTION`) SQL Server;
  more than one internal batch (a few thousand rows) to prove nothing is buffered into a fixed page; `TrackedUrlsApiTests`-style
  API tests with real tokens (200 with content type, disposition and BOM, 400, 401, 403) on SQLite.
- E2E (`tests/export.spec.ts` or inside `dashboard.spec.ts`, single-site, run-scoped prefix, `FlushInterval=00:00:01`):
  with a search on the run prefix, clicking Export triggers a download whose content has the BOM, the header and exactly
  the matching rows, and not rows outside the search.
- Manual: the sizeable table on `single-site` (about 100,000 rows inserted with a script) exports without the process
  memory growing with the size; open the result in Excel with a `;` and with a `,` regional setting and note the result
  in CLAUDE.md. Not verifiable here: a mid-stream failure (covered by a unit test of the abort path).

## Left to the builder

Exact chunk size and flush interval, the shared-helper shape for the filter and order logic, whether the exporter seam is
internal or public (see Decisions), the generated client's blob handling, wording of the toasts and the troubleshooting
entry, and the file layout within the existing structure.
