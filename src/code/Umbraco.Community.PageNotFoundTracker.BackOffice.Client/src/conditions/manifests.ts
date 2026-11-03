import { PNFT_DASHBOARD_ACCESS_CONDITION_ALIAS } from './constants.js';

/** The package's conditions. */
export const manifests: Array<UmbExtensionManifest> = [
  {
    type: 'condition',
    alias: PNFT_DASHBOARD_ACCESS_CONDITION_ALIAS,
    name: 'Page Not Found Tracker Dashboard Access Condition',
    api: () => import('./dashboard-access.condition.js'),
  },
];
