import { PNFT_TRACKED_URL_COLLECTION_ALIAS } from '../collection/constants.js';
import { PNFT_EXPORT_ACTION_ALIAS } from './constants.js';

// The condition alias as a literal (Umbraco's `UMB_COLLECTION_ALIAS_CONDITION`), so this file has no backoffice runtime
// imports and its test runs in Vitest.
const COLLECTION_ALIAS_CONDITION = 'Umb.Condition.CollectionAlias';

/**
 * The export of the tracked URLs: an "Export" button in the collection's toolbar that downloads everything the current
 * search, filters and sort match as a CSV file. Only on the tracked URL collection, so it shows to the same people as the
 * dashboard (the collection is only reachable through it).
 */
export const manifests: Array<UmbExtensionManifest> = [
  {
    type: 'collectionAction',
    kind: 'button',
    alias: PNFT_EXPORT_ACTION_ALIAS,
    name: 'Page Not Found Tracker Export Tracked URLs',
    api: () => import('./export.collection-action.js'),
    meta: {
      label: '#pageNotFoundTracker_exportAction',
    },
    conditions: [{ alias: COLLECTION_ALIAS_CONDITION, match: PNFT_TRACKED_URL_COLLECTION_ALIAS }],
  },
];
