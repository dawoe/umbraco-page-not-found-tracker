import type { UmbControllerHost } from '@umbraco-cms/backoffice/controller-api';
import { PNFT_TRACKED_URL_TABLE_VIEW_ALIAS } from './constants.js';
import { PnftPagedCollectionContextBase } from './paged-collection.context.base.js';
import type { PnftTrackedUrlCollectionFilterModel, PnftTrackedUrlCollectionModel } from './types.js';

/** The `pnftCollection` name of the tracked URL collection context. */
export const PNFT_TRACKED_URL_COLLECTION_KIND = 'tracked-url';

/**
 * The context of the tracked URL collection: the package's paged collection context (errors, page reset, page size,
 * filter choices) with the tracked URL defaults (hits descending).
 */
export class PnftTrackedUrlCollectionContext extends PnftPagedCollectionContextBase<
  PnftTrackedUrlCollectionModel,
  PnftTrackedUrlCollectionFilterModel
> {
  readonly pnftCollection = PNFT_TRACKED_URL_COLLECTION_KIND;

  constructor(host: UmbControllerHost) {
    super(host, PNFT_TRACKED_URL_TABLE_VIEW_ALIAS, { orderBy: 'Hits', orderDirection: 'Descending' });
  }
}

export { PnftTrackedUrlCollectionContext as api };
