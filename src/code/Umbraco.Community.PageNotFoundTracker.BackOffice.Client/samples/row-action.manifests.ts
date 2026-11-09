import { PNFT_BLOCK_RULE_ENTITY_TYPE } from '../src/blocking/constants.js';
import { PNFT_TRACKED_URL_ENTITY_TYPE } from '../src/entity.js';

// docs:begin row-action-manifest
export const manifests: Array<UmbExtensionManifest> = [
  {
    type: 'entityAction',
    kind: 'default',
    alias: 'My.EntityAction.Report',
    name: 'My report action',
    weight: -20, // below the package's own actions
    // Tracked URL rows and block rule rows of the two dashboards.
    forEntityTypes: [PNFT_TRACKED_URL_ENTITY_TYPE, PNFT_BLOCK_RULE_ENTITY_TYPE],
    api: () => import('./row-action.js'),
    meta: {
      icon: 'icon-info',
      label: 'Report',
    },
  },
];
// docs:end row-action-manifest
