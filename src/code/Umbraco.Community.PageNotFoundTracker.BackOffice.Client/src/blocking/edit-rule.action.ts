import { UmbEntityActionBase } from '@umbraco-cms/backoffice/entity-action';
import { PNFT_BLOCK_RULE_COLLECTION_CONTEXT } from './collection/block-rule-collection.context-token.js';
import { openRuleModalAndReload } from './open-rule-modal.js';

/**
 * The "Edit" action on a block rule row: opens the rule modal with the row's pattern (as displayed), domain, culture and
 * status code, saves the changes to that rule (`ruleId`) and reloads the blocked list. A collision with another rule is
 * shown inside the modal; the list also reloads on cancel (see `openRuleModalAndReload`).
 */
export class PnftEditBlockRuleEntityAction extends UmbEntityActionBase<never> {
  override async execute(): Promise<void> {
    const collection = await this.getContext(PNFT_BLOCK_RULE_COLLECTION_CONTEXT);
    const rule = collection?.getItems().find((item) => item.unique === this.args.unique);

    if (!collection || !rule) {
      return;
    }

    await openRuleModalAndReload(
      this,
      {
        pattern: rule.pattern,
        domain: rule.domain,
        culture: rule.culture,
        statusCode: rule.statusCode,
        title: '#pageNotFoundTracker_editRuleHeadline',
        ruleId: rule.id,
      },
      () => collection.loadCollection(),
    );
  }
}

export { PnftEditBlockRuleEntityAction as api };
