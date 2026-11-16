# Export tracked URLs Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** An "Export" action on the tracked URLs dashboard downloads every row matching the current search, filters and sort as an Excel-friendly CSV, streamed from the database through a new Management API endpoint.

**Architecture:** A new public Core seam `ITrackedUrlExporter` streams `TrackedUrl` rows (`IAsyncEnumerable`) over the same filter and order logic as the paged query (extracted to a shared internal helper). A new BackOffice controller validates like the list, writes the CSV (pure `TrackedUrlCsvWriter`) straight to the response and aborts the connection on a mid-stream failure. The client calls the generated `exportTrackedUrls` as a blob and saves it.

**Tech Stack:** .NET 10, EF Core (SQLite and SQL Server), Umbraco 17.8 Management API, NUnit + Moq, Lit/TypeScript, Vitest, Playwright, `@hey-api/openapi-ts` generated client.

**Spec:** `docs/superpowers/specs/2026-11-16-export-tracked-urls-design.md` (read it first; this plan only records the decisions the spec leaves to the implementer).

## Global Constraints

- Zero analyzer warnings; every C# file starts with the single-line StyleCop copyright header from `src/stylecop.json`; usings outside the namespace, file-scoped namespaces, braces always, XML docs on public and internal members.
- Layering: contracts in Core, behaviour in Infrastructure, endpoint and CSV in BackOffice. Replaceable services are registered with `AddUnique`, never `TryAdd`.
- Endpoint: `GET /umbraco/pagenotfoundtracker/api/v1/tracked-urls/export`, operation ID `ExportTrackedUrls`, policy `Constants.DashboardAccessPolicy`, tag `Tracked URL`.
- CSV: UTF-8 with BOM (`EF BB BF`, written once), separator `;`, line ending `\r\n`, header row; columns in order `Url`, `Relative path`, `Domain`, `Culture`, `Type` (`Page`/`File`), `First seen (UTC)`, `Last seen (UTC)`, `Hits`; timestamps `yyyy-MM-dd HH:mm:ss` UTC, invariant culture; `Hits` a plain invariant integer; quote a field containing `;`, `"`, CR or LF with `"` doubled; a text field starting with `=`, `+`, `-`, `@`, tab or CR gets a leading `'`; empty domain and culture are empty cells.
- Response: `text/csv; charset=utf-8`, `Content-Disposition: attachment; filename="page-not-found-<yyyyMMdd-HHmmss>.csv"` (UTC). 400 and 503 are decided before the first byte; after the first byte a failure aborts the connection.
- No row cap. No new package reference (none expected; a new one must be mirrored in the entry csproj).
- Client: no hard-coded URLs; interactive elements carry `data-mark`; texts in `localization/en.ts` as `pageNotFoundTracker_*`.
- Public docs never link to `CLAUDE.md` or `docs/superpowers/`; a `csharp`/`typescript` block in a docs page is preceded by `<!-- sample: <repo path>#<id> -->` and equals the compiled sample region.
- Run from PowerShell for SQL Server cases: `$env:PNFT_SQLSERVER_CONNECTION='Server=(localdb)\pnft-test;Integrated Security=True;TrustServerCertificate=True'` (Git Bash cannot reach LocalDB).

## Review Focus

- A tracked URL, domain or culture starting with `=`, `+`, `-` or `@` must not run as an Excel formula: covered in Task 2 for every text column, not only `Url`.
- A URL containing `;`, `"`, a newline or non-ASCII characters must stay in one cell and survive UTF-8: Task 2.
- Formatting must not depend on the server's current culture (an `nl-NL` thread must still write `2026-11-01 08:00:00` and `1234`, not `1.234`): Task 2.
- An empty result is a valid file (BOM and header only), not an error: Task 3.
- A client that disconnects mid-stream must stop the database read, and a failure after the first byte must abort rather than finish as a short "successful" file: Task 3.

---

### Task 1: Streaming read (contract, shared filter/order, implementation)

**Files:**
- Create: `src/code/Umbraco.Community.PageNotFoundTracker.Core/Tracking/TrackedUrlExportQuery.cs`, `src/code/Umbraco.Community.PageNotFoundTracker.Core/Tracking/ITrackedUrlExporter.cs`, `src/code/Umbraco.Community.PageNotFoundTracker.Infrastructure/Tracking/TrackedUrlQueryComposition.cs`, `src/code/Umbraco.Community.PageNotFoundTracker.Infrastructure/Tracking/TrackedUrlExporter.cs`
- Modify: `src/code/Umbraco.Community.PageNotFoundTracker.Infrastructure/Tracking/TrackedUrlQueryService.cs` (use the shared helper), `src/code/Umbraco.Community.PageNotFoundTracker.Infrastructure/Extensions/UmbracoBuilderExtensions.cs` (register)
- Test: `src/test/Umbraco.Community.PageNotFoundTracker.Integration.Tests/Tracking/TrackedUrlExporterTests.cs`

**Why public:** Infrastructure grants `InternalsVisibleTo` only to the Integration tests and the entry project, so BackOffice cannot see an internal seam; per the spec the seam is therefore public in Core and a documented extension point (Task 6).

**Interfaces:**
- Produces: `public sealed record TrackedUrlExportQuery(TrackedUrlSortColumn SortColumn, bool Descending, string? Search, TrackedUrlFilter Domain, TrackedUrlFilter Culture)` (Core, XML-documented like `TrackedUrlQuery`).
- Produces: `public interface ITrackedUrlExporter { IAsyncEnumerable<TrackedUrl> StreamAsync(TrackedUrlExportQuery query, CancellationToken cancellationToken); }` (Core). Contract in the XML doc: replaceable (`AddUnique` after the anchor composer), must read lazily and must not buffer the table, order must end in ascending `Id`.
- Produces: `internal static class TrackedUrlQueryComposition` with `Filter(IQueryable<TrackedUrl>, string? search, TrackedUrlFilter domain, TrackedUrlFilter culture)` and `Order(IQueryable<TrackedUrl>, TrackedUrlSortColumn, bool descending)`, moved verbatim from `TrackedUrlQueryService`. `TrackedUrlQueryService.GetPageAsync` calls them; its behaviour does not change.
- Produces: `internal sealed class TrackedUrlExporter(IServiceProvider serviceProvider) : ITrackedUrlExporter`, registered `AddUnique<ITrackedUrlExporter, TrackedUrlExporter>()` next to `ITrackedUrlQueryService`. Like the query service it resolves `IDbContextFactory<PageNotFoundTrackerDbContext>` per call, never in the constructor.

- [ ] **Step 1: Write the failing tests** in `TrackedUrlExporterTests` (derive from `TrackingDatabaseFixture`, same seeding and `TestContextFactory` as `TrackedUrlQueryServiceTests`; SQLite and SQL Server):
  - `StreamAsync_EverySortColumn_BothDirections_MatchesAllPagesOfGetPage([Values] TrackedUrlSortColumn column, [Values] bool descending)`: the streamed ids equal the ids from paging `GetPageAsync` (take 10) to the end.
  - `StreamAsync_SearchAndFilters_MatchGetPage`: for search `about`, `Domain = Exactly(...)`, `Culture = Empty`, the streamed ids equal the paged ids.
  - `StreamAsync_MoreRowsThanOneBatch_ReturnsEveryRow`: seed 3,000 rows, stream them all, count 3,000 and ids strictly in the requested order.
  - `StreamAsync_Cancelled_StopsWithOperationCanceled`: cancel after the first row, assert `OperationCanceledException`.
  - `StreamAsync_NoRows_ReturnsEmpty`.

- [ ] **Step 2: Run to verify it fails**

Run (from `src/`): `dotnet test test/Umbraco.Community.PageNotFoundTracker.Integration.Tests --filter FullyQualifiedName~TrackedUrlExporterTests`
Expected: FAIL to compile (`ITrackedUrlExporter` not defined).

- [ ] **Step 3: Implement** the Core types, extract the helper (move `Filter`/`Order`/`OrderBy` out of `TrackedUrlQueryService`, call them from it), implement `StreamAsync` as `Filter` then `Order` then `AsNoTracking().AsAsyncEnumerable()` inside an `await using` context held for the whole enumeration (an `async IAsyncEnumerable` iterator method), no `Skip`/`Take`. Register in `UmbracoBuilderExtensions`.

- [ ] **Step 4: Run to verify it passes**

Run: the Step 2 command, then `dotnet test test/Umbraco.Community.PageNotFoundTracker.Integration.Tests --filter FullyQualifiedName~TrackedUrlQueryServiceTests` (the extraction broke nothing); then the Step 2 command from PowerShell with `PNFT_SQLSERVER_CONNECTION` set.
Expected: PASS on SQLite and SQL Server; `dotnet build` shows zero warnings.

- [ ] **Step 5: Commit**

```bash
git add src/code src/test
git commit -m "feat: stream tracked URLs for export"
```

---

### Task 2: CSV writer

**Files:**
- Create: `src/code/Umbraco.Community.PageNotFoundTracker.BackOffice/Export/TrackedUrlCsvWriter.cs`
- Test: `src/test/Umbraco.Community.PageNotFoundTracker.BackOffice.Tests/TrackedUrlCsvWriterTests.cs`

**Interfaces:**
- Consumes: `TrackedUrl` (Core; `DisplayUrl`, `RelativePath`, `Domain`, `Culture`, `IsFile`, `FirstSeen`, `LastSeen`, `HitCount`).
- Produces: `internal static class TrackedUrlCsvWriter` with
  `static ReadOnlyMemory<byte> Preamble` (the UTF-8 BOM bytes), `static string HeaderRow` (header line including the trailing `\r\n`), `static string FormatRow(TrackedUrl row)` (one line including the trailing `\r\n`), and `static string EscapeField(string value)` (quoting plus formula prefix; used by `FormatRow`). Strings, not a stream: the controller encodes (UTF-8 without BOM) and writes, so the writer is pure.
- Date formatting uses `DateTime.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)` after `DateTime.SpecifyKind(value, DateTimeKind.Utc)`; `HitCount.ToString(CultureInfo.InvariantCulture)`.

- [ ] **Step 1: Write the failing tests** (NUnit, `TestCase`s where useful):
  - `HeaderRow_IsTheEightColumnsSeparatedBySemicolon`: equals `"Url;Relative path;Domain;Culture;Type;First seen (UTC);Last seen (UTC);Hits\r\n"`.
  - `Preamble_IsTheUtf8Bom`: bytes `EF BB BF`.
  - `FormatRow_PageRow`: `/Old-Page`, `/old-page`, `localhost:44402/sitea`, `en-US`, page, 2026-11-01 08:00:00, 2026-11-02 09:30:05, 1234 gives `"/Old-Page;/old-page;localhost:44402/sitea;en-US;Page;2026-11-01 08:00:00;2026-11-02 09:30:05;1234\r\n"`.
  - `FormatRow_FileRow_IsTypeFile`, `FormatRow_EmptyDomainAndCulture_AreEmptyCells` (`...;;;Page;...`).
  - `EscapeField_Quotes_WhenItContainsSemicolonQuoteCrOrLf` (`TestCase`s: `a;b` gives `"a;b"`, `a"b` gives `"a""b"`, a value with `\n` and one with `\r` gives quoted) and `EscapeField_PlainValue_IsUnchanged`.
  - `EscapeField_FormulaStart_GetsLeadingApostrophe` (`TestCase`s `=1+1`, `+1`, `-1`, `@SUM(A1)`, `\tx`, `\rx` give a leading `'`; the `\r` and `\t` cases are also quoted where the quoting rule applies) and `FormatRow_FormulaInEveryTextColumn_IsPrefixed` (a row whose `DisplayUrl`, `RelativePath`, `Domain` and `Culture` all start with `=`).
  - `FormatRow_UnderDutchCulture_StillUsesInvariantFormats`: set `CultureInfo.CurrentCulture = new CultureInfo("nl-NL")` in the test (restore in `finally`), `HitCount = 1234567`, assert `1234567` and the date format unchanged.
  - `FormatRow_NonAscii_IsKeptAsIs` (`/café-ü`).
  - `FormatRow_LastSeenKindUnspecified_IsWrittenAsUtc` (a SQLite-style `DateTimeKind.Unspecified` value).

- [ ] **Step 2: Run to verify it fails**

Run (from `src/`): `dotnet test test/Umbraco.Community.PageNotFoundTracker.BackOffice.Tests --filter FullyQualifiedName~TrackedUrlCsvWriterTests`
Expected: FAIL to compile (`TrackedUrlCsvWriter` not defined).

- [ ] **Step 3: Implement** `TrackedUrlCsvWriter` per the Interfaces block and Global Constraints. Apply the formula prefix before the quoting check, so the quoting sees the final text.

- [ ] **Step 4: Run to verify it passes**

Run: the Step 2 command.
Expected: PASS, zero build warnings.

- [ ] **Step 5: Commit**

```bash
git add src/code src/test
git commit -m "feat: add the tracked URL CSV writer"
```

---

### Task 3: Export endpoint

**Files:**
- Create: `src/code/Umbraco.Community.PageNotFoundTracker.BackOffice/Controllers/ExportTrackedUrlsController.cs`
- Modify: `src/code/Umbraco.Community.PageNotFoundTracker.BackOffice/Controllers/GetTrackedUrlsController.cs` and/or `TrackedUrlsControllerBase.cs` (move the shared validation and `ToFilter` so both controllers use one copy; the list's behaviour and tests do not change)
- Test: `src/test/Umbraco.Community.PageNotFoundTracker.BackOffice.Tests/ExportTrackedUrlsControllerTests.cs`, `src/test/Umbraco.Community.PageNotFoundTracker.Integration.Tests/ManagementApi/TrackedUrlsApiTests.cs`

**Interfaces:**
- Consumes: `ITrackedUrlExporter.StreamAsync` and `TrackedUrlExportQuery` (Task 1); `TrackedUrlCsvWriter` (Task 2); `ITrackingPersistenceState`; `EnsurePersistenceAvailableAsync`, `InvalidRequest` from the base.
- Produces: `public sealed class ExportTrackedUrlsController(ITrackedUrlExporter exporter, ITrackingPersistenceState persistenceState, TimeProvider timeProvider, ILogger<ExportTrackedUrlsController> logger) : TrackedUrlsControllerBase` with
  `[HttpGet("tracked-urls/export")] [MapToApiVersion("1.0")] [Authorize(Policy = Constants.DashboardAccessPolicy)] public async Task<IActionResult> ExportTrackedUrls(CancellationToken cancellationToken, TrackedUrlSortColumn orderBy = TrackedUrlSortColumn.Hits, Direction direction = Direction.Descending, string? search = null, string? domain = null, bool noDomain = false, string? culture = null, bool noCulture = false)`. `[ProducesResponseType(StatusCodes.Status200OK, "text/csv")]`, plus 400 and 503 `ProblemDetails`. The generated client function becomes `exportTrackedUrls`: keep the action method name.
- The 200 path: set `Content-Type`, `Content-Disposition` (filename from `timeProvider.GetUtcNow()`), write `Preamble` and `HeaderRow` and flush **before** the first read, then for each row write `FormatRow` (UTF-8 without BOM, `Response.Body` async writes) and flush every 500 rows and at the end; return `new EmptyResult()`. `OperationCanceledException` (client gone) ends quietly; any other exception after the first byte is logged as an error and calls `HttpContext.Abort()`.

- [ ] **Step 1: Write the failing unit tests** (`ExportTrackedUrlsControllerTests`, controller with a mock `ITrackedUrlExporter`, a `DefaultHttpContext` whose `Response.Body` is a `MemoryStream`, a fake `TimeProvider`):
  - `Export_UnknownSortOrContradictingFilter_Returns400` (`domain` with `noDomain`, `culture` with `noCulture`) and the exporter is never called.
  - `Export_PersistenceUnavailable_Returns503BeforeAnyByte` (body length 0).
  - `Export_Rows_WritesBomHeaderAndRows`: body starts with `EF BB BF`, then `HeaderRow`, then one `FormatRow` per streamed row; content type `text/csv; charset=utf-8`; `Content-Disposition` equals `attachment; filename="page-not-found-20261116-103000.csv"` for a fixed clock.
  - `Export_NoRows_WritesBomAndHeaderOnly`.
  - `Export_PassesTheFilterToTheExporter` (search, `Exactly` domain, `Empty` culture, sort and direction captured from `TrackedUrlExportQuery`).
  - `Export_ExporterThrowsAfterFirstRow_LogsAndAbortsTheConnection`: a throwing async enumerable after one row; assert the error was logged and `HttpContext` abort was requested (use a `IHttpRequestLifetimeFeature` fake and assert `Abort` was called).
  - `Export_CancelledByClient_StopsReadingAndDoesNotAbort`: the token is cancelled after the first row; the enumerable observes it and stops.

- [ ] **Step 2: Run to verify it fails**

Run (from `src/`): `dotnet test test/Umbraco.Community.PageNotFoundTracker.BackOffice.Tests --filter FullyQualifiedName~ExportTrackedUrlsControllerTests`
Expected: FAIL to compile.

- [ ] **Step 3: Implement** the controller and the shared validation extraction. Run the existing `TrackedUrlsControllerTests` to confirm the list is unchanged.

- [ ] **Step 4: Write the failing API tests** in `TrackedUrlsApiTests` (real backoffice tokens, SQLite, existing helpers): `Export_AllowedGroup_Returns200WithBomHeaderAndSeededRow` (content type, disposition prefix, first three bytes, the header line, a seeded row), `Export_FollowsSearchAndOrder` (a search narrows rows; `orderBy=Url&direction=Ascending` orders), `Export_GroupOutsideSetting_Returns403`, `Export_Anonymous_Returns401`, `Export_ContradictingFilter_Returns400`. Extend `OpenApiDocument_ContainsTheOperationIds` with `(ApiBase + "/tracked-urls/export", "ExportTrackedUrls")` in the expected list.

- [ ] **Step 5: Run everything for this task**

Run: `dotnet test test/Umbraco.Community.PageNotFoundTracker.BackOffice.Tests`, then `dotnet test test/Umbraco.Community.PageNotFoundTracker.Integration.Tests --filter FullyQualifiedName~TrackedUrlsApiTests`.
Expected: PASS; zero build warnings.

- [ ] **Step 6: Commit**

```bash
git add src/code src/test
git commit -m "feat: add the tracked URL export endpoint"
```

---

### Task 4: Client export action

**Files:**
- Create: `src/code/Umbraco.Community.PageNotFoundTracker.BackOffice.Client/src/export/export.service.ts` (pure), `export.service.test.ts`, `export.collection-action.ts`, `manifests.ts`, `constants.ts`, `manifests.test.ts`
- Modify: `src/api/` (regenerate: commit only `index.ts`, `sdk.gen.ts`, `types.gen.ts` real changes; revert line-ending-only diffs), `src/collection/query.ts` (+ `query.test.ts`), `src/collection/paged-collection.context.base.ts` (public `getCurrentFilter()`), `src/collection/collection.context-token.ts` if the action needs the token, `src/localization/en.ts`, the client's manifest aggregation (where `blocking/manifests.ts` is registered), `src/collection/data-marks.ts` only if a mark is added
- Test: the Vitest files above

**Interfaces:**
- Consumes: generated `exportTrackedUrls` (after regeneration, the document must contain the new operation: start the single-site profile first, see CLAUDE.md "Regenerate after an API change"); `PNFT_TRACKED_URL_COLLECTION_CONTEXT`; `fromCollectionFilter`.
- Produces in `query.ts`: `buildExportQuery(filter: Partial<PnftCollectionFilter>): PnftExportQuery`, where `PnftExportQuery = NonNullable<ExportTrackedUrlsData['query']>`: the same mapping as `buildListQuery` without `skip` and `take` (same default order `Hits`/`Descending`, blank search dropped, `PNFT_EMPTY` to `noDomain`/`noCulture`).
- Produces in `export.service.ts`: `fileNameFromDisposition(header: string | null | undefined): string` (the quoted `filename` or `page-not-found.csv` when absent or unparseable) and `classifyExportError(error: unknown): 'unavailable' | 'forbidden' | 'failed'` (503, 401/403, anything else; reuse `classifyError` from `collection/state.ts` where it already maps these).
- Produces: `PnftExportCollectionAction extends UmbCollectionActionBase` (`export { PnftExportCollectionAction as api }`), manifest `{ type: 'collectionAction', kind: 'button', alias: 'Pnft.CollectionAction.TrackedUrl.Export', name: ..., api: () => import('./export.collection-action.js'), meta: { label: '#pageNotFoundTracker_exportAction' }, conditions: [{ alias: 'Umb.Condition.CollectionAlias', match: PNFT_TRACKED_URL_COLLECTION_ALIAS }] }` (alias literal, as `blocking/manifests.ts`, so the test runs in Vitest). `PnftPagedCollectionContextBase.getCurrentFilter(): TFilter` returns `this._filter.getValue()`.
- Texts: `pageNotFoundTracker_exportAction` ("Export"), `pageNotFoundTracker_exportSucceeded`, `pageNotFoundTracker_exportUnavailable`, `pageNotFoundTracker_exportForbidden`, `pageNotFoundTracker_exportFailed`.

- [ ] **Step 1: Write the failing Vitest tests**: `buildExportQuery` (no `skip`/`take`; default `orderBy` `Hits` and `Descending`; blank search dropped; `PNFT_EMPTY` domain gives `noDomain: true`; a value named `empty` is sent as a value); `fileNameFromDisposition` (`attachment; filename="page-not-found-20261116-103000.csv"` gives that name; null, empty and garbage give `page-not-found.csv`); `classifyExportError` (503, 403, 401, a network error); `manifests` (the action is a `collectionAction` of kind `button`, its only condition is the tracked collection alias, the label key exists in `en.ts`).

- [ ] **Step 2: Run to verify it fails**

Run (from the client project): `npm test -- export query`
Expected: FAIL (modules not found / `buildExportQuery` missing).

- [ ] **Step 3: Implement** the pure pieces, `getCurrentFilter()`, the localization keys and the manifest registration.

- [ ] **Step 4: Regenerate the client** with the single-site profile running on 44401 (`NODE_TLS_REJECT_UNAUTHORIZED=0 npm run generate-client`), then make the generated `exportTrackedUrls` return a blob: pass `parseAs: 'blob'` per call, not in the config. Revert line-ending-only diffs.

- [ ] **Step 5: Implement the action** `execute()`: if a previous export is running return at once (instance field guard); capture the notification context and localization first; read `getCurrentFilter()` from the collection context, map it with `fromCollectionFilter` then `buildExportQuery`; call `tryExecute(this, exportTrackedUrls({ query, parseAs: 'blob' }), { disableNotifications: true })`; on success save the blob under `fileNameFromDisposition(...)` with `URL.createObjectURL` and a temporary anchor, always revoking the URL; success toast, or a warning toast with the text for `classifyExportError`. If the response headers are not reachable through the generated client's result, read `Content-Disposition` from the raw `response` the client returns alongside `data`.

- [ ] **Step 6: Run and build**

Run (from the client project): `npm test`, `npm run build`, `npm run check-samples`.
Expected: PASS, the build succeeds, no `localhost` in the bundle.

- [ ] **Step 7: Commit**

```bash
git add src/code/Umbraco.Community.PageNotFoundTracker.BackOffice.Client
git commit -m "feat: add the export action to the tracked URLs dashboard"
```

---

### Task 5: E2E test

**Files:**
- Create: `src/test/Umbraco.Community.PageNotFoundTracker.E2E/tests/export.spec.ts`

**Interfaces:**
- Consumes: the dashboard helpers pattern of `tests/dashboard.spec.ts` (run-scoped prefix, `FlushInterval=00:00:01`, `openAndSearch`); the action's data-mark `collection-action:Pnft.CollectionAction.TrackedUrl.Export`. Keep a copy of any client data-mark used, as the other specs do.

- [ ] **Step 1: Write the test** `Export_downloads_the_rows_matching_the_search`: send three 404s under `/pnft-e2e-export-<run>-a|b` and one outside the prefix, open the dashboard, search the prefix, wait for 2 rows, click the export button inside `page.waitForEvent('download')`, read the file: starts with the BOM, the header line equals the spec header, contains exactly the two prefixed rows (and not the outside row), suggested file name matches `page-not-found-\d{8}-\d{6}\.csv`. A second test, `Export_follows_the_sort`: sorting by URL ascending gives the rows in that order.

- [ ] **Step 2: Run it**

Run (single-site running with `Umbraco__Community__PageNotFoundTracker__FlushInterval=00:00:01`, client bundle built, from the E2E project): `npm run test:e2e -- export.spec.ts`
Expected: PASS. Then run the whole suite once (`npm run test:e2e`) to confirm no neighbour broke.

- [ ] **Step 3: Commit**

```bash
git add src/test/Umbraco.Community.PageNotFoundTracker.E2E
git commit -m "test: cover the tracked URL export end to end"
```

---

### Task 6: Documentation

**Files:**
- Modify: `docs/dashboards.md`, `docs/troubleshooting.md`, `docs/README.md`, `docs/extending/README.md`, `CLAUDE.md`
- Create: `docs/extending/tracked-url-export.md`, `src/samples/Umbraco.Community.PageNotFoundTracker.Samples/<one type per file>` (a custom `ITrackedUrlExporter` and its composer, with `docs:begin`/`docs:end` regions)

**Interfaces:**
- Consumes: the shipped behaviour of Tasks 1 to 5 (write from the code, not from the spec).

- [ ] **Step 1: `docs/dashboards.md`**: add an "Export" section under "Page not found": what is exported (everything matching the current search, domain, culture and sort, not only the page shown), the file (UTF-8 with BOM and `;` so Excel opens it by double-click, the eight columns, UTC dates `yyyy-MM-dd HH:mm:ss`, `Type` is Page or File), who can use it (the dashboard access rule), Excel's 1,048,576-row limit, that the file is not a snapshot if hits arrive meanwhile, and that a download that breaks off fails instead of ending short.
- [ ] **Step 2: `docs/troubleshooting.md`**: add "The export shows one column in Excel": cause (a `,` regional list separator), fix (Excel Data > From Text/CSV with the delimiter set to semicolon, or change the regional setting).
- [ ] **Step 3: Extension page** `docs/extending/tracked-url-export.md`: the `ITrackedUrlExporter` contract (lazy streaming, no buffering, order ends in ascending `Id`, replace with `AddUnique` in a composer after `PageNotFoundTrackerComposer`) with a quoted compiled sample; link it from `docs/extending/README.md` and `docs/README.md`. Note that the CSV format itself is fixed.
- [ ] **Step 4: `CLAUDE.md`**: add spec 11 (done) and its plan to the lists, an "Export (spec 11)" section (endpoint, format, seam and why it is public, client files, data-marks, tests, manual checks from Task 7), the new files in the solution layout, and the new E2E spec in the Tests section.
- [ ] **Step 5: Run the checks and commit**

Run (repo root): `pwsh .github/scripts/check-doc-samples.ps1`, `pwsh .github/scripts/check-doc-links.ps1`, `dotnet test src/test/Umbraco.Community.PageNotFoundTracker.Infrastructure.Tests --filter ConfigurationDocsTests`, `dotnet build src` (the sample compiles, zero warnings).
Expected: all pass.

```bash
git add docs src/samples CLAUDE.md
git commit -m "docs: document the tracked URL export"
```

---

### Task 7: Whole-branch verification

**Files:** none (results are recorded in `CLAUDE.md` under "Export (spec 11)").

- [ ] **Step 1: Full automated run.** Client: `npm ci`, `npm test`, `npm run build`. From `src/`: `dotnet build` (zero warnings), `dotnet test` (PowerShell with `PNFT_SQLSERVER_CONNECTION` set so the SQL Server cases run), `pwsh ../scripts/check-pending-model-changes.ps1`, pack and `pwsh .github/scripts/check-package.ps1` (no model change expected, so no migration).
- [ ] **Step 2: Large export check on `single-site`.** Insert about 100,000 tracked rows with a `node:sqlite` script while the site is stopped, start the site, call the endpoint with a client-credentials token and `curl -k --output`, and watch the process working set: it must not grow with the file size. Then restore the database and `appsettings.json` (`git checkout --`, see CLAUDE.md).
- [ ] **Step 3: Excel check.** Open an exported file by double-click in Excel with a `;` regional setting (columns split, dates are dates, `Ü` and `é` intact) and once with `,` (note the one-column result and that Data > From Text/CSV fixes it). Record both in `CLAUDE.md`.
- [ ] **Step 4: Commit the recorded results**

```bash
git add CLAUDE.md
git commit -m "docs: record the export manual checks"
```

---

## Self-review notes

- Spec coverage: endpoint and validation (Task 3), streaming and shared order (Task 1), CSV format, quoting, formula prefix, dates, BOM (Task 2), headers and abort path (Task 3), client action and states (Task 4), access via policy and collection condition (Tasks 3 and 4), docs and CLAUDE.md (Task 6), tests in every layer and the manual checks (Tasks 1 to 5, 7).
- The spec left the seam's visibility open; the plan resolves it as public because BackOffice has no `InternalsVisibleTo` from Infrastructure, which is why Task 6 includes an extension page and a sample.
- Names used across tasks: `ITrackedUrlExporter.StreamAsync`, `TrackedUrlExportQuery`, `TrackedUrlCsvWriter.{Preamble,HeaderRow,FormatRow,EscapeField}`, `ExportTrackedUrls`, `buildExportQuery`, `fileNameFromDisposition`, `classifyExportError`, `getCurrentFilter`.
