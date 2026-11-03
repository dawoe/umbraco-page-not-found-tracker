import type { ManifestDashboard } from '@umbraco-cms/backoffice/dashboard';
import { UMB_SECTION_ALIAS_CONDITION_ALIAS } from '@umbraco-cms/backoffice/section';
import { PNFT_DASHBOARD_ACCESS_CONDITION_ALIAS } from '../../conditions/constants.js';

/** The alias of the tracked URLs dashboard. */
export const PNFT_TRACKED_URLS_DASHBOARD_ALIAS = 'Umbraco.Community.PageNotFoundTracker.Dashboard.TrackedUrls';

/**
 * The tracked URLs dashboard in the Content section, shown only to users the server allows (the access
 * condition). No icon: Umbraco 17.8 dashboards have none (`MetaDashboard` is label and pathname only and the
 * section tab renders only the label). Weight 5 puts it after Umbraco's own Content dashboards.
 */
export const manifests: Array<ManifestDashboard> = [
  {
    type: 'dashboard',
    alias: PNFT_TRACKED_URLS_DASHBOARD_ALIAS,
    name: 'Page Not Found Tracker Tracked URLs Dashboard',
    element: () => import('./tracked-urls-dashboard.element.js'),
    weight: 5,
    meta: {
      label: '#pageNotFoundTracker_dashboardLabel',
      pathname: 'page-not-found-tracker',
    },
    conditions: [
      { alias: UMB_SECTION_ALIAS_CONDITION_ALIAS, match: 'Umb.Section.Content' },
      { alias: PNFT_DASHBOARD_ACCESS_CONDITION_ALIAS },
    ],
  },
];
