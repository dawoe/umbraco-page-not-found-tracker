import { UmbContextToken } from '@umbraco-cms/backoffice/context-api';
import type { UmbDefaultCollectionContext } from '@umbraco-cms/backoffice/collection';
import type { PnftBlockRuleCollectionContext } from './block-rule-collection.context.js';

/**
 * The block rule collection context, under the framework's collection context alias. The discriminator (the context's
 * `pnftCollection` name) makes sure the pieces of this collection only bind to it, never to the tracked URL collection.
 */
export const PNFT_BLOCK_RULE_COLLECTION_CONTEXT = new UmbContextToken<UmbDefaultCollectionContext, PnftBlockRuleCollectionContext>(
  'UmbCollectionContext',
  undefined,
  // The literal (not the constant from the context file) keeps this file free of runtime imports of the context.
  (context): context is PnftBlockRuleCollectionContext => (context as { pnftCollection?: unknown }).pnftCollection === 'block-rule',
);
