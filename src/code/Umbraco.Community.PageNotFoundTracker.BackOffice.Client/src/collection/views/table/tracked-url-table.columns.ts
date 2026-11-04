import type { TrackedUrlSortColumn } from '../../../api/index.js';
import { PNFT_SORT_MARKS } from '../../data-marks.js';
import { columnAliasIn, sortColumnIn, type PnftTableColumnDefinition } from './table-columns.js';

// The columns of the tracked URL table and their sort mapping. Pure: no backoffice runtime imports.

export { PNFT_ENTITY_ACTIONS_COLUMN, formatLocalDateTime } from './table-columns.js';

/** The alias of a data column of the table (also the key of its sort mark). */
export type PnftColumnAlias = keyof typeof PNFT_SORT_MARKS;

/** One data column of the table. */
export type PnftColumnDefinition = PnftTableColumnDefinition<PnftColumnAlias, TrackedUrlSortColumn>;

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
  return sortColumnIn(PNFT_COLUMNS, alias);
}

/** The table column alias of an API sort column. */
export function columnAliasOf(sortColumn: TrackedUrlSortColumn | undefined): PnftColumnAlias | undefined {
  return columnAliasIn(PNFT_COLUMNS, sortColumn);
}
