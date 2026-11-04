import type { BlockRuleSortColumn } from '../../../../api/index.js';
import { columnAliasIn, sortColumnIn, type PnftTableColumnDefinition } from '../../../../collection/views/table/table-columns.js';
import { PNFT_BLOCK_SORT_MARKS } from '../../../data-marks.js';

// The columns of the block rule table and their sort mapping. Pure: no backoffice runtime imports.

/** The alias of a data column of the block rule table (also the key of its sort mark). */
export type PnftBlockRuleColumnAlias = keyof typeof PNFT_BLOCK_SORT_MARKS;

/** The data columns in display order (spec: pattern, status code, domain, culture, created). */
export const PNFT_BLOCK_RULE_COLUMNS: ReadonlyArray<PnftTableColumnDefinition<PnftBlockRuleColumnAlias, BlockRuleSortColumn>> = [
  { alias: 'pattern', labelKey: 'pageNotFoundTracker_columnPattern', sortMark: PNFT_BLOCK_SORT_MARKS.pattern, sortColumn: 'Pattern' },
  {
    alias: 'statusCode',
    labelKey: 'pageNotFoundTracker_columnStatusCode',
    sortMark: PNFT_BLOCK_SORT_MARKS.statusCode,
    sortColumn: 'StatusCode',
  },
  { alias: 'domain', labelKey: 'pageNotFoundTracker_columnDomain', sortMark: PNFT_BLOCK_SORT_MARKS.domain, sortColumn: 'Domain' },
  { alias: 'culture', labelKey: 'pageNotFoundTracker_columnCulture', sortMark: PNFT_BLOCK_SORT_MARKS.culture, sortColumn: 'Culture' },
  { alias: 'created', labelKey: 'pageNotFoundTracker_columnCreated', sortMark: PNFT_BLOCK_SORT_MARKS.created, sortColumn: 'Created' },
];

/** The default sort of the block rule table: newest first. */
export const PNFT_BLOCK_RULE_DEFAULT_SORT = { column: 'created', orderBy: 'Created', descending: true } as const;

/** The API sort column of a table column alias, undefined for a column that does not sort. */
export function blockRuleSortColumnOf(alias: string): BlockRuleSortColumn | undefined {
  return sortColumnIn(PNFT_BLOCK_RULE_COLUMNS, alias);
}

/** The table column alias of an API sort column. */
export function blockRuleColumnAliasOf(sortColumn: BlockRuleSortColumn | undefined): PnftBlockRuleColumnAlias | undefined {
  return columnAliasIn(PNFT_BLOCK_RULE_COLUMNS, sortColumn);
}
