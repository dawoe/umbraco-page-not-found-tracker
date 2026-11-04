import type { UmbControllerHost } from '@umbraco-cms/backoffice/controller-api';
import { UMB_ENTITY_CONTEXT } from '@umbraco-cms/backoffice/entity';
import type { UmbConditionConfigBase, UmbConditionControllerArguments, UmbExtensionCondition } from '@umbraco-cms/backoffice/extension-api';
import { UmbConditionBase } from '@umbraco-cms/backoffice/extension-registry';
import { PNFT_TRACKED_URL_COLLECTION_CONTEXT } from '../collection/collection.context-token.js';
import type { PnftTrackedUrlCollectionModel } from '../collection/types.js';
import { canCreateRedirectFor } from './can-create-redirect.js';
import type { PNFT_CAN_CREATE_REDIRECT_CONDITION_ALIAS } from './constants.js';

/** The configuration of the can create redirect condition (no options). */
export type PnftCanCreateRedirectConditionConfig = UmbConditionConfigBase<typeof PNFT_CAN_CREATE_REDIRECT_CONDITION_ALIAS>;

/**
 * Permits the create redirect action on a tracked URL row only when the server flagged that row
 * (`canCreateRedirect`, the active redirect provider's answer). It reads the row's `unique` from the row's entity
 * context and the row from the collection context's items, and follows both, so a reload re-evaluates it.
 */
export class PnftCanCreateRedirectCondition
  extends UmbConditionBase<PnftCanCreateRedirectConditionConfig>
  implements UmbExtensionCondition
{
  #unique?: string | null;

  #items?: Array<PnftTrackedUrlCollectionModel>;

  constructor(host: UmbControllerHost, args: UmbConditionControllerArguments<PnftCanCreateRedirectConditionConfig>) {
    super(host, args);

    this.consumeContext(UMB_ENTITY_CONTEXT, (context) => {
      this.observe(
        context?.unique,
        (unique) => {
          this.#unique = unique;
          this.#update();
        },
        'pnftUniqueObserver',
      );
    });

    this.consumeContext(PNFT_TRACKED_URL_COLLECTION_CONTEXT, (context) => {
      this.observe(
        context?.items,
        (items) => {
          this.#items = items as Array<PnftTrackedUrlCollectionModel> | undefined;
          this.#update();
        },
        'pnftItemsObserver',
      );
    });
  }

  #update() {
    this.permitted = canCreateRedirectFor(this.#items, this.#unique);
  }
}

export { PnftCanCreateRedirectCondition as api };
