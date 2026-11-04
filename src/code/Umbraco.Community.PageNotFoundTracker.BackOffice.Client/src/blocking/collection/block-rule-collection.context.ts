import type { UmbControllerHost } from '@umbraco-cms/backoffice/controller-api';
import { PnftPagedCollectionContextBase } from '../../collection/paged-collection.context.base.js';
import { PNFT_BLOCK_RULE_TABLE_VIEW_ALIAS } from '../constants.js';
import type { PnftBlockRuleCollectionFilterModel, PnftBlockRuleCollectionModel } from './types.js';

/** The `pnftCollection` name of the block rule collection context. */
export const PNFT_BLOCK_RULE_COLLECTION_KIND = 'block-rule';

/**
 * The context of the block rule collection: the package's paged collection context (errors, page reset, page size,
 * filter choices) with the block rule defaults (created descending, newest first).
 */
export class PnftBlockRuleCollectionContext extends PnftPagedCollectionContextBase<
  PnftBlockRuleCollectionModel,
  PnftBlockRuleCollectionFilterModel
> {
  readonly pnftCollection = PNFT_BLOCK_RULE_COLLECTION_KIND;

  constructor(host: UmbControllerHost) {
    super(host, PNFT_BLOCK_RULE_TABLE_VIEW_ALIAS, { orderBy: 'Created', orderDirection: 'Descending' });
  }
}

export { PnftBlockRuleCollectionContext as api };
