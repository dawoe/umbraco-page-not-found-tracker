import { PNFT_TRACKED_URL_ENTITY_TYPE } from '../entity.js';
import { PNFT_DELETE_ACTION_ALIAS } from './constants.js';

/**
 * Deleting tracked URLs in the backoffice: the "Delete" action on tracked URL rows. Weight -20 sorts it below block
 * (-10), so it is always reached through the row's "..." dropdown.
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
];
