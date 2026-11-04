/**
 * The `data-mark` hooks of the tracked URL collection. The E2E tests keep their own copy of these values:
 * change both together.
 */
export const PNFT_DATA_MARKS = {
  /** The collection element (host). */
  collection: 'pnft-collection',
  /** The URL search box. */
  search: 'pnft-search',
  /** The domain filter select. */
  filterDomain: 'pnft-filter-domain',
  /** The culture filter select. */
  filterCulture: 'pnft-filter-culture',
  /** The page size select. */
  pageSize: 'pnft-page-size',
  /** Every table row (`uui-table-row`); the row also carries `data-unique` (the tracked URL id). */
  row: 'pnft-row',
  /** No URLs tracked yet. */
  stateEmpty: 'pnft-state-empty',
  /** URLs are tracked, but none match the search or filters. */
  stateEmptyFiltered: 'pnft-state-empty-filtered',
  /** The list request failed. */
  stateError: 'pnft-state-error',
  /** The API answered 503: the tracking database is unavailable. */
  stateUnavailable: 'pnft-state-unavailable',
  /** A list request is in flight. */
  stateLoading: 'pnft-state-loading',
  /** The total number of matching records. */
  total: 'pnft-total',
} as const;

/** The sortable column header hooks, `pnft-sort-<column>`. */
export const PNFT_SORT_MARKS = {
  url: 'pnft-sort-url',
  domain: 'pnft-sort-domain',
  culture: 'pnft-sort-culture',
  type: 'pnft-sort-type',
  firstSeen: 'pnft-sort-first-seen',
  lastSeen: 'pnft-sort-last-seen',
  hits: 'pnft-sort-hits',
} as const;
