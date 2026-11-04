/**
 * The `data-mark` hooks of the blocked URLs dashboard and the block rule flow. The E2E tests keep their own copy of
 * these values: change both together. Umbraco marks the actions itself: the row buttons and dropdown items are
 * `entity-action:Pnft.EntityAction.TrackedUrl.Block` and `entity-action:Pnft.EntityAction.BlockRule.Unblock`, the
 * collection button is `collection-action:Pnft.CollectionAction.BlockRule.Add`.
 */
export const PNFT_BLOCK_DATA_MARKS = {
  /** The block rule collection element (host). */
  collection: 'pnft-block-collection',
  /** The pattern search box. */
  search: 'pnft-block-search',
  /** The domain filter select. */
  filterDomain: 'pnft-block-filter-domain',
  /** The culture filter select. */
  filterCulture: 'pnft-block-filter-culture',
  /** The page size select. */
  pageSize: 'pnft-block-page-size',
  /** Every table row (`uui-table-row`); the row also carries `data-unique` (the rule id). */
  row: 'pnft-block-row',
  /** No rules yet. */
  stateEmpty: 'pnft-block-state-empty',
  /** Rules exist, but none match the search or filters. */
  stateEmptyFiltered: 'pnft-block-state-empty-filtered',
  /** The list request failed. */
  stateError: 'pnft-block-state-error',
  /** The API answered 503: the tracking database is unavailable. */
  stateUnavailable: 'pnft-block-state-unavailable',
  /** A list request is in flight. */
  stateLoading: 'pnft-block-state-loading',
  /** The total number of matching rules. */
  total: 'pnft-block-total',
  /** The rule modal (its body layout). */
  modal: 'pnft-block-rule-modal',
  /** The modal's pattern input. */
  pattern: 'pnft-block-pattern',
  /** The modal's "enter a pattern" validation message. */
  patternRequired: 'pnft-block-pattern-required',
  /** The modal's domain select. */
  domain: 'pnft-block-domain',
  /** The modal's culture select. */
  culture: 'pnft-block-culture',
  /** The modal's status code select. */
  statusCode: 'pnft-block-status',
  /** The modal's warning that rules apply to every matching request. */
  warning: 'pnft-block-warning',
  /** The modal's inline error (the server's validation message or a generic failure). */
  error: 'pnft-block-error',
  /** The modal's Confirm button. */
  confirm: 'pnft-block-confirm',
  /** The modal's Cancel button. */
  cancel: 'pnft-block-cancel',
} as const;

/** The sortable column header hooks of the block rule table, `pnft-block-sort-<column>`. */
export const PNFT_BLOCK_SORT_MARKS = {
  pattern: 'pnft-block-sort-pattern',
  statusCode: 'pnft-block-sort-status',
  domain: 'pnft-block-sort-domain',
  culture: 'pnft-block-sort-culture',
  created: 'pnft-block-sort-created',
} as const;
