import { UmbContextToken } from '@umbraco-cms/backoffice/context-api';
import type { UmbDefaultCollectionContext } from '@umbraco-cms/backoffice/collection';
import type { PnftTrackedUrlCollectionContext } from './collection.context.js';
import type { PnftPagedCollectionContextBase } from './paged-collection.context.base.js';
import type { PnftPagedCollectionFilterModel } from './types.js';

/** Any of the package's paged collection contexts (tracked URLs, block rules). */
export type PnftAnyPagedCollectionContext = PnftPagedCollectionContextBase<{ entityType: string; unique: string }, PnftPagedCollectionFilterModel>;

/**
 * Any of the package's paged collection contexts, under the framework's collection context alias. The shared toolbar
 * pieces (domain and culture filters, page size) consume it, so they bind to the nearest package collection.
 */
export const PNFT_PAGED_COLLECTION_CONTEXT = new UmbContextToken<UmbDefaultCollectionContext, PnftAnyPagedCollectionContext>(
  'UmbCollectionContext',
  undefined,
  (context): context is PnftAnyPagedCollectionContext => typeof (context as { pnftCollection?: unknown }).pnftCollection === 'string',
);

/**
 * The tracked URL collection context, under the framework's collection context alias. The discriminator makes
 * sure the pieces of this collection only bind to it, not to another collection they might be nested in (the block
 * rule collection has the same shape, so the check is on the `pnftCollection` name).
 */
export const PNFT_TRACKED_URL_COLLECTION_CONTEXT = new UmbContextToken<
  UmbDefaultCollectionContext,
  PnftTrackedUrlCollectionContext
>(
  'UmbCollectionContext',
  undefined,
  // The literal (not the constant from collection.context.ts) keeps this file free of runtime imports of the context.
  (context): context is PnftTrackedUrlCollectionContext => (context as { pnftCollection?: unknown }).pnftCollection === 'tracked-url',
);
