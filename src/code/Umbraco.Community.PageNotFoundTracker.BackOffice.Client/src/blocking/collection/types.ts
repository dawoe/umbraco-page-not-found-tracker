import type { BlockRuleSortColumn } from '../../api/index.js';
import type { PnftPagedCollectionFilterModel, PnftPagedFilter } from '../../collection/types.js';
import type { PNFT_BLOCK_RULE_ENTITY_TYPE } from '../constants.js';

/** The filter the block rule collection passes to the repository. */
export type PnftBlockRuleFilter = PnftPagedFilter<BlockRuleSortColumn>;

/** The filter of the block rule collection as the collection context holds it (`filter` is the pattern search). */
export type PnftBlockRuleCollectionFilterModel = PnftPagedCollectionFilterModel<BlockRuleSortColumn>;

/** One row of the block rule collection. */
export interface PnftBlockRuleCollectionModel {
  entityType: typeof PNFT_BLOCK_RULE_ENTITY_TYPE;
  unique: string;
  id: number;
  /** The pattern as the editor first typed it. */
  pattern: string;
  statusCode: number;
  /** The domain, or an empty string for all domains. */
  domain: string;
  /** The culture, or an empty string for all cultures. */
  culture: string;
  /** UTC, ISO 8601. */
  created: string;
}
