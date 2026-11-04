import { UmbContextToken } from '@umbraco-cms/backoffice/context-api';
import type { UmbDefaultCollectionContext } from '@umbraco-cms/backoffice/collection';
import type { PnftTrackedUrlCollectionContext } from './collection.context.js';

/**
 * The tracked URL collection context, under the framework's collection context alias. The discriminator makes
 * sure the pieces of this collection only bind to it, not to another collection they might be nested in.
 */
export const PNFT_TRACKED_URL_COLLECTION_CONTEXT = new UmbContextToken<
  UmbDefaultCollectionContext,
  PnftTrackedUrlCollectionContext
>('UmbCollectionContext', undefined, (context): context is PnftTrackedUrlCollectionContext => 'setPageSize' in context);
