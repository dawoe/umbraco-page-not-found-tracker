import { UmbEntityBulkActionBase } from '@umbraco-cms/backoffice/entity-bulk-action';
import { html } from '@umbraco-cms/backoffice/external/lit';
import { UmbLocalizationController } from '@umbraco-cms/backoffice/localization-api';
import { umbConfirmModal } from '@umbraco-cms/backoffice/modal';
import { UMB_NOTIFICATION_CONTEXT } from '@umbraco-cms/backoffice/notification';
import { tryExecute } from '@umbraco-cms/backoffice/resources';
import { deleteTrackedUrls } from '../api/index.js';
import { PNFT_TRACKED_URL_COLLECTION_CONTEXT } from '../collection/collection.context-token.js';
import { deleteTrackedUrls as sendDelete } from './delete.service.js';

/**
 * The "Delete selected" bulk action on the tracked URL list: asks for confirmation (Umbraco's confirm modal) naming the
 * count, deletes the selected rows and reloads the list. Selection is limited to the current page (the collection clears
 * it on every load), so at most one page (100 rows, the API's limit) is sent; only ids of rows on the current page are
 * sent, which keeps that true even if a selection outlived a reload.
 */
export class PnftDeleteSelectedBulkAction extends UmbEntityBulkActionBase<never> {
  override async execute(): Promise<void> {
    const collection = await this.getContext(PNFT_TRACKED_URL_COLLECTION_CONTEXT);

    if (!collection) {
      return;
    }

    const selected = new Set(this.selection);
    const ids = collection
      .getItems()
      .filter((item) => selected.has(item.unique))
      .map((item) => Number(item.unique));

    if (ids.length === 0) {
      return;
    }

    // Taken before the modal opens: the reload below clears the selection, which removes the footer that hosts this action.
    const notifications = await this.getContext(UMB_NOTIFICATION_CONTEXT);
    const localize = new UmbLocalizationController(this);

    try {
      await umbConfirmModal(this, {
        headline: localize.term('pageNotFoundTracker_deleteSelectedHeadline'),
        content: html`<p>${localize.term('pageNotFoundTracker_deleteSelectedConfirmText', ids.length)}</p>`,
        color: 'danger',
        confirmLabel: localize.term('pageNotFoundTracker_delete'),
      });
    } catch {
      // Cancelled.
      return;
    }

    const result = await sendDelete(
      (send) => tryExecute(this, deleteTrackedUrls({ body: { ids: send } }), { disableNotifications: true }),
      ids,
    );

    collection.loadCollection();

    switch (result.kind) {
      case 'deleted':
        notifications?.peek('positive', {
          data: {
            headline: localize.term('pageNotFoundTracker_deletedHeadline'),
            message: localize.term('pageNotFoundTracker_deletedMany', result.deleted),
          },
        });
        break;
      case 'notFound':
        notifications?.peek('warning', {
          data: { headline: localize.term('pageNotFoundTracker_deletedHeadline'), message: localize.term('pageNotFoundTracker_deleteNotFound') },
        });
        break;
      default:
        notifications?.peek('danger', {
          data: {
            headline: localize.term('pageNotFoundTracker_deleteFailedHeadline'),
            message:
              result.kind === 'invalid' && result.message
                ? result.message
                : localize.term(result.kind === 'unavailable' ? 'pageNotFoundTracker_deleteUnavailable' : 'pageNotFoundTracker_deleteError'),
          },
        });
    }
  }
}

export { PnftDeleteSelectedBulkAction as api };
