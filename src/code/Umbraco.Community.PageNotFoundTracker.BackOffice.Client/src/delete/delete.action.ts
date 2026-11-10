import { UmbEntityActionBase } from '@umbraco-cms/backoffice/entity-action';
import { html } from '@umbraco-cms/backoffice/external/lit';
import { UmbLocalizationController } from '@umbraco-cms/backoffice/localization-api';
import { umbConfirmModal } from '@umbraco-cms/backoffice/modal';
import { UMB_NOTIFICATION_CONTEXT } from '@umbraco-cms/backoffice/notification';
import { tryExecute } from '@umbraco-cms/backoffice/resources';
import { deleteTrackedUrl } from '../api/index.js';
import { PNFT_TRACKED_URL_COLLECTION_CONTEXT } from '../collection/collection.context-token.js';
import { deleteTrackedUrl as sendDelete } from './delete.service.js';

/**
 * The "Delete" action on a tracked URL row: asks for confirmation (Umbraco's confirm modal), deletes the row and reloads
 * the list. The URL is tracked again from 1 when it is requested again. A row that is already gone (404) gives the
 * package's own text, because Umbraco's error interceptor replaces 404 problem details.
 */
export class PnftDeleteEntityAction extends UmbEntityActionBase<never> {
  override async execute(): Promise<void> {
    const collection = await this.getContext(PNFT_TRACKED_URL_COLLECTION_CONTEXT);
    const row = collection?.getItems().find((item) => item.unique === this.args.unique);

    if (!collection || !row) {
      return;
    }

    // Taken before the modal opens: the reload below removes the row, and with it this action's host.
    const notifications = await this.getContext(UMB_NOTIFICATION_CONTEXT);
    const localize = new UmbLocalizationController(this);

    try {
      await umbConfirmModal(this, {
        headline: localize.term('pageNotFoundTracker_deleteHeadline'),
        // A template, so the URL is escaped (a string is rendered as HTML).
        content: html`<p>${localize.term('pageNotFoundTracker_deleteConfirmText', row.url)}</p>`,
        color: 'danger',
        confirmLabel: localize.term('pageNotFoundTracker_delete'),
      });
    } catch {
      // Cancelled.
      return;
    }

    const result = await sendDelete(
      (id) => tryExecute(this, deleteTrackedUrl({ path: { id } }), { disableNotifications: true }),
      row.id,
    );

    collection.loadCollection();

    switch (result.kind) {
      case 'deleted':
        notifications?.peek('positive', {
          data: {
            headline: localize.term('pageNotFoundTracker_deletedHeadline'),
            message: localize.term('pageNotFoundTracker_deleted', row.url),
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
            message: localize.term(result.kind === 'unavailable' ? 'pageNotFoundTracker_deleteUnavailable' : 'pageNotFoundTracker_deleteError'),
          },
        });
    }
  }
}

export { PnftDeleteEntityAction as api };
