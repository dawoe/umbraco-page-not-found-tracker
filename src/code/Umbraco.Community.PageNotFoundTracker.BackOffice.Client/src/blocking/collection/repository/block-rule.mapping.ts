import type { BlockRuleResponseModel } from '../../../api/index.js';
import { PNFT_BLOCK_RULE_ENTITY_TYPE } from '../../constants.js';
import type { PnftBlockRuleCollectionModel } from '../types.js';

// Pure mapping of an API item to a collection row. No backoffice runtime imports, so it runs in Vitest.

/** Maps one API item to a row of the block rule collection (entity type, `unique` = the id as a string). */
export function mapBlockRule(item: BlockRuleResponseModel): PnftBlockRuleCollectionModel {
  return {
    entityType: PNFT_BLOCK_RULE_ENTITY_TYPE,
    unique: String(item.id),
    id: item.id,
    pattern: item.pattern,
    statusCode: item.statusCode,
    domain: item.domain,
    culture: item.culture,
    created: item.created,
  };
}
