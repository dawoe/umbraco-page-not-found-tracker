import { UmbCollectionActionBase } from '@umbraco-cms/backoffice/collection';
import { UmbLocalizationController } from '@umbraco-cms/backoffice/localization-api';
import { UMB_NOTIFICATION_CONTEXT } from '@umbraco-cms/backoffice/notification';
import { tryExecute } from '@umbraco-cms/backoffice/resources';
import { exportTrackedUrls } from '../api/index.js';
import { PNFT_TRACKED_URL_COLLECTION_CONTEXT } from '../collection/collection.context-token.js';
import { buildExportQuery, fromCollectionFilter } from '../collection/query.js';
import { classifyExportError, fileNameFromDisposition, saveBlobAsFile } from './export.service.js';

/**
 * The "Export" action of the tracked URLs collection: downloads every tracked URL the current search, filters and sort
 * match (not only the visible page) as a CSV file. The server streams the rows; the file is saved once it is complete.
 * A second click while an export runs does nothing.
 */
export class PnftExportCollectionAction extends UmbCollectionActionBase {
  #running = false;

  override async execute(): Promise<void> {
    if (this.#running) {
      return;
    }

    this.#running = true;

    try {
      const collection = await this.getContext(PNFT_TRACKED_URL_COLLECTION_CONTEXT);
      const notifications = await this.getContext(UMB_NOTIFICATION_CONTEXT);
      const localize = new UmbLocalizationController(this);
      const filter = collection ? fromCollectionFilter(collection.getCurrentFilter()) : {};

      // `tryExecute` hands back the whole result of the generated client call, so the response headers are there too.
      const result = (await tryExecute(this, exportTrackedUrls({ query: buildExportQuery(filter), parseAs: 'blob' }), {
        disableNotifications: true,
      })) as { data?: Blob; response?: Response; error?: unknown };

      if (result.error || !result.data) {
        const kind = classifyExportError(result.error);
        notifications?.peek('warning', {
          data: {
            headline: localize.term('pageNotFoundTracker_exportHeadline'),
            message: localize.term(`pageNotFoundTracker_export${kind[0].toUpperCase()}${kind.slice(1)}`),
          },
        });
        return;
      }

      saveBlobAsFile(result.data, fileNameFromDisposition(result.response?.headers.get('Content-Disposition')));
      notifications?.peek('positive', {
        data: {
          headline: localize.term('pageNotFoundTracker_exportHeadline'),
          message: localize.term('pageNotFoundTracker_exportSucceeded'),
        },
      });
    } finally {
      this.#running = false;
    }
  }
}

export { PnftExportCollectionAction as api };
