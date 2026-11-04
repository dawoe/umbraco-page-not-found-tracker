import type { TrackedUrlSortColumn } from '../../../api/index.js';
import { PNFT_SORT_MARKS } from '../../data-marks.js';

// The columns of the tracked URL table and their sort mapping. Pure: no backoffice runtime imports.

/** The alias of a data column of the table (also the key of its sort mark). */
export type PnftColumnAlias = keyof typeof PNFT_SORT_MARKS;

/** One data column of the table. */
export interface PnftColumnDefinition {
  alias: PnftColumnAlias;
  /** The localization key of the header. */
  labelKey: string;
  /** The `data-mark` of the header. */
  sortMark: string;
  /** The API sort column. */
  sortColumn: TrackedUrlSortColumn;
  align?: 'left' | 'center' | 'right';
}

/** The alias of the entity actions column (no header, no sorting). */
export const PNFT_ENTITY_ACTIONS_COLUMN = 'entityActions';

/** The data columns in display order (spec: URL, domain, culture, type, first seen, last seen, hits). */
export const PNFT_COLUMNS: ReadonlyArray<PnftColumnDefinition> = [
  { alias: 'url', labelKey: 'pageNotFoundTracker_columnUrl', sortMark: PNFT_SORT_MARKS.url, sortColumn: 'Url' },
  { alias: 'domain', labelKey: 'pageNotFoundTracker_columnDomain', sortMark: PNFT_SORT_MARKS.domain, sortColumn: 'Domain' },
  { alias: 'culture', labelKey: 'pageNotFoundTracker_columnCulture', sortMark: PNFT_SORT_MARKS.culture, sortColumn: 'Culture' },
  { alias: 'type', labelKey: 'pageNotFoundTracker_columnType', sortMark: PNFT_SORT_MARKS.type, sortColumn: 'IsFile' },
  {
    alias: 'firstSeen',
    labelKey: 'pageNotFoundTracker_columnFirstSeen',
    sortMark: PNFT_SORT_MARKS.firstSeen,
    sortColumn: 'FirstSeen',
  },
  {
    alias: 'lastSeen',
    labelKey: 'pageNotFoundTracker_columnLastSeen',
    sortMark: PNFT_SORT_MARKS.lastSeen,
    sortColumn: 'LastSeen',
  },
  { alias: 'hits', labelKey: 'pageNotFoundTracker_columnHits', sortMark: PNFT_SORT_MARKS.hits, sortColumn: 'Hits', align: 'right' },
];

/** The API sort column of a table column alias, undefined for a column that does not sort. */
export function sortColumnOf(alias: string): TrackedUrlSortColumn | undefined {
  return PNFT_COLUMNS.find((column) => column.alias === alias)?.sortColumn;
}

/** The table column alias of an API sort column. */
export function columnAliasOf(sortColumn: TrackedUrlSortColumn | undefined): PnftColumnAlias | undefined {
  return PNFT_COLUMNS.find((column) => column.sortColumn === sortColumn)?.alias;
}

/** Renders a UTC ISO 8601 timestamp in the user's locale and time zone; invalid or missing gives ''. */
export function formatLocalDateTime(iso: string | undefined): string {
  if (!iso) {
    return '';
  }

  const date = new Date(iso);

  return Number.isNaN(date.getTime()) ? '' : date.toLocaleString();
}
