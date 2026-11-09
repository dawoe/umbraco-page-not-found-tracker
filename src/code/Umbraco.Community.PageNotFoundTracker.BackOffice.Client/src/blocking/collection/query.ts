import type { GetBlockRulesData } from '../../api/index.js';
import { buildPagedQuery, fromCollectionFilter } from '../../collection/query.js';
import type { PnftBlockRuleCollectionFilterModel, PnftBlockRuleFilter } from './types.js';

/** The list query parameters of the block rule endpoint. */
export type PnftBlockRuleListQuery = NonNullable<GetBlockRulesData['query']>;

/**
 * Maps the block rule collection filter to the list query parameters. Pure. Same rules as the tracked URL list
 * (`buildPagedQuery`), but the default order is created descending (newest first). The server has the same default;
 * the client sends it explicitly anyway.
 */
export function buildBlockRuleListQuery(filter: Partial<PnftBlockRuleFilter>): PnftBlockRuleListQuery {
  return buildPagedQuery(filter, 'Created');
}

/** Maps the filter the collection context holds to the repository filter (`filter` becomes `search`). Pure. */
export function fromBlockRuleCollectionFilter(filter: PnftBlockRuleCollectionFilterModel): Partial<PnftBlockRuleFilter> {
  return fromCollectionFilter(filter);
}
