import { UmbEntityActionBase } from '@umbraco-cms/backoffice/entity-action';
import { PNFT_TRACKED_URL_COLLECTION_CONTEXT } from '../collection/collection.context-token.js';
import { openRuleModalAndReload } from './open-rule-modal.js';
import { buildRuleDefaults } from './rule-form.js';

/**
 * The "Block" action on a tracked URL row (page and file rows alike). The action's args carry only the entity type and
 * the `unique` (the id as a string), so it reads the row from the collection context (as the create redirect action
 * does), opens the rule modal with the row's relative path, domain and culture preselected and, once the rule is saved,
 * reloads the tracked list (the row is gone: the rule deleted it) and notifies.
 */
export class PnftBlockEntityAction extends UmbEntityActionBase<never> {
  override async execute(): Promise<void> {
    const collection = await this.getContext(PNFT_TRACKED_URL_COLLECTION_CONTEXT);
    const trackedUrl = collection?.getItems().find((item) => item.unique === this.args.unique);

    if (!collection || !trackedUrl) {
      return;
    }

    await openRuleModalAndReload(this, { ...buildRuleDefaults(trackedUrl), title: '#pageNotFoundTracker_blockHeadline' }, () =>
      collection.loadCollection(),
    );
  }
}

export { PnftBlockEntityAction as api };
