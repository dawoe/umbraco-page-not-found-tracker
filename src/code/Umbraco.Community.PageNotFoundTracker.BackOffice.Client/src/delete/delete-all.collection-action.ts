import { UmbCollectionActionBase } from '@umbraco-cms/backoffice/collection';
import { firstValueFrom } from '@umbraco-cms/backoffice/external/rxjs';
import { UmbLocalizationController } from '@umbraco-cms/backoffice/localization-api';
import { umbOpenModal } from '@umbraco-cms/backoffice/modal';
import { UMB_NOTIFICATION_CONTEXT } from '@umbraco-cms/backoffice/notification';
import { tryExecute } from '@umbraco-cms/backoffice/resources';
import { deleteMatchingTrackedUrls as sendDeleteMatching } from '../api/index.js';
import { PNFT_TRACKED_URL_COLLECTION_CONTEXT } from '../collection/collection.context-token.js';
import type { PnftPagedCollectionFilterModel } from '../collection/types.js';
import { PNFT_DELETE_ALL_MODAL } from './delete-all-modal.token.js';
import { criteriaFromFilter, deleteMatchingTrackedUrls } from './delete.service.js';

/**
 * The "Delete all matching" action of the tracked URL list: opens the delete all dialog with the list's total and
 * whether a search or filter is active, then deletes everything the current filter matches (not only the page on screen)
 * and reloads. It does nothing when the list is empty.
 */
export class PnftDeleteAllCollectionAction extends UmbCollectionActionBase {
  override async execute(): Promise<void> {
    const collection = await this.getContext(PNFT_TRACKED_URL_COLLECTION_CONTEXT);

    if (!collection) {
      return;
    }

    const total = await firstValueFrom(collection.totalItems);

    if (total <= 0) {
      return;
    }

    const filter = collection.getFilter() as PnftPagedCollectionFilterModel;
    // One source for both the typed-word requirement and the request, so they cannot diverge.
    const criteria = criteriaFromFilter(filter);

    // Taken before the modal opens: the reload below can remove the host of this action.
    const notifications = await this.getContext(UMB_NOTIFICATION_CONTEXT);
    const localize = new UmbLocalizationController(this);

    try {
      await umbOpenModal(this, PNFT_DELETE_ALL_MODAL, { data: { total, filtered: !criteria.all } });
    } catch {
      // Cancelled.
      return;
    }

    const result = await deleteMatchingTrackedUrls(
      (criteria) => tryExecute(this, sendDeleteMatching({ body: { noDomain: false, noCulture: false, all: false, ...criteria } }), { disableNotifications: true }),
      criteria,
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

export { PnftDeleteAllCollectionAction as api };
