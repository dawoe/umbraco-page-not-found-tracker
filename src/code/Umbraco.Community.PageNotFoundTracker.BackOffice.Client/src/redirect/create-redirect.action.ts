import { UmbEntityActionBase } from '@umbraco-cms/backoffice/entity-action';
import { umbOpenModal } from '@umbraco-cms/backoffice/modal';
import { UmbLocalizationController } from '@umbraco-cms/backoffice/localization-api';
import { UMB_NOTIFICATION_CONTEXT } from '@umbraco-cms/backoffice/notification';
import { PNFT_TRACKED_URL_COLLECTION_CONTEXT } from '../collection/collection.context-token.js';
import { PNFT_CREATE_REDIRECT_MODAL, type PnftCreateRedirectModalValue } from './redirect-modal.token.js';

/**
 * The create redirect action on a tracked URL row. The action's args carry only the entity type and the `unique`
 * (the id as a string), so it reads the row from the collection context, opens the create redirect modal for it and,
 * once the redirect exists, reloads the list and notifies (a warning with the server's message when the tracked
 * record could not be removed). Cancelling the modal does nothing.
 */
export class PnftCreateRedirectEntityAction extends UmbEntityActionBase<never> {
  override async execute(): Promise<void> {
    const collection = await this.getContext(PNFT_TRACKED_URL_COLLECTION_CONTEXT);
    const trackedUrl = collection?.getItems().find((item) => item.unique === this.args.unique);

    if (!collection || !trackedUrl) {
      return;
    }

    // Taken before the modal opens: the reload below removes the row, and with it this action's host.
    const notifications = await this.getContext(UMB_NOTIFICATION_CONTEXT);
    const localize = new UmbLocalizationController(this);

    let value: PnftCreateRedirectModalValue;

    try {
      value = await umbOpenModal(this, PNFT_CREATE_REDIRECT_MODAL, { data: { trackedUrl } });
    } catch {
      // Cancelled.
      return;
    }

    collection.loadCollection();

    if (value.removed) {
      notifications?.peek('positive', {
        data: {
          headline: localize.term('pageNotFoundTracker_redirectCreatedHeadline'),
          message: localize.term('pageNotFoundTracker_redirectCreated', trackedUrl.url),
        },
      });
    } else {
      notifications?.peek('warning', {
        data: {
          headline: localize.term('pageNotFoundTracker_redirectCreatedHeadline'),
          message: value.message ?? localize.term('pageNotFoundTracker_redirectCreatedRecordKept'),
        },
      });
    }
  }
}

export { PnftCreateRedirectEntityAction as api };
