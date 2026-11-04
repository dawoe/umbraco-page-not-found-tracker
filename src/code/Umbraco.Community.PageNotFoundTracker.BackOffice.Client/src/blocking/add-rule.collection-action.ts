import { UmbCollectionActionBase } from '@umbraco-cms/backoffice/collection';
import { PNFT_BLOCK_RULE_COLLECTION_CONTEXT } from './collection/block-rule-collection.context-token.js';
import { openRuleModalAndReload } from './open-rule-modal.js';
import { buildRuleDefaults } from './rule-form.js';

/**
 * The "Add rule" action of the blocked URLs collection: opens the rule modal empty (no pattern, all domains, all
 * cultures, 410) and, once the rule is saved, reloads the blocked list and notifies.
 */
export class PnftAddBlockRuleCollectionAction extends UmbCollectionActionBase {
  override async execute(): Promise<void> {
    const collection = await this.getContext(PNFT_BLOCK_RULE_COLLECTION_CONTEXT);

    await openRuleModalAndReload(this, { ...buildRuleDefaults(undefined), title: '#pageNotFoundTracker_addRuleHeadline' }, () =>
      collection?.loadCollection(),
    );
  }
}

export { PnftAddBlockRuleCollectionAction as api };
