import { PNFT_TRACKED_URL_ENTITY_TYPE } from '../entity.js';
import { PNFT_TRACKED_URL_COLLECTION_ALIAS } from '../collection/constants.js';
import {
  PNFT_DELETE_ACTION_ALIAS,
  PNFT_DELETE_ALL_ACTION_ALIAS,
  PNFT_DELETE_ALL_MODAL_ALIAS,
  PNFT_DELETE_SELECTED_BULK_ACTION_ALIAS,
} from './constants.js';

/** Umbraco's collection alias condition (a literal, so this file stays free of backoffice runtime imports for Vitest). */
const COLLECTION_ALIAS_CONDITION = 'Umb.Condition.CollectionAlias';

/**
 * Deleting tracked URLs in the backoffice: the "Delete" action on tracked URL rows (weight -20 sorts it below block,
 * -10, so it is always reached through the row's "..." dropdown) and the "Delete selected" bulk action.
 *
 * Umbraco 17.8 does not filter bulk actions by `forEntityTypes`: the collection footer renders every permitted
 * `entityBulkAction`, and the collection makes its rows selectable as soon as any is permitted. The collection alias
 * condition keeps both to the tracked URL list (the block rule list stays without selection).
 */
export const manifests: Array<UmbExtensionManifest> = [
  {
    type: 'entityAction',
    kind: 'default',
    alias: PNFT_DELETE_ACTION_ALIAS,
    name: 'Page Not Found Tracker Delete Entity Action',
    weight: -20,
    forEntityTypes: [PNFT_TRACKED_URL_ENTITY_TYPE],
    api: () => import('./delete.action.js'),
    meta: {
      icon: 'icon-trash',
      label: '#pageNotFoundTracker_delete',
      additionalOptions: true,
    },
  },
  {
    type: 'entityBulkAction',
    kind: 'default',
    alias: PNFT_DELETE_SELECTED_BULK_ACTION_ALIAS,
    name: 'Page Not Found Tracker Delete Selected Entity Bulk Action',
    weight: 0,
    forEntityTypes: [PNFT_TRACKED_URL_ENTITY_TYPE],
    api: () => import('./delete-selected.bulk-action.js'),
    meta: {
      icon: 'icon-trash',
      label: '#pageNotFoundTracker_deleteSelected',
    },
    conditions: [{ alias: COLLECTION_ALIAS_CONDITION, match: PNFT_TRACKED_URL_COLLECTION_ALIAS }],
  },
  {
    // 17.8 ships the `button` kind for collection actions (there is no `default` kind). It is not filtered by entity
    // type, so the collection alias condition keeps it to the tracked URL list.
    type: 'collectionAction',
    kind: 'button',
    alias: PNFT_DELETE_ALL_ACTION_ALIAS,
    name: 'Page Not Found Tracker Delete All Collection Action',
    api: () => import('./delete-all.collection-action.js'),
    meta: {
      label: '#pageNotFoundTracker_deleteAll',
      additionalOptions: false,
    },
    conditions: [{ alias: COLLECTION_ALIAS_CONDITION, match: PNFT_TRACKED_URL_COLLECTION_ALIAS }],
  },
  {
    type: 'modal',
    alias: PNFT_DELETE_ALL_MODAL_ALIAS,
    name: 'Page Not Found Tracker Delete All Modal',
    element: () => import('./delete-all-modal.element.js'),
  },
];
