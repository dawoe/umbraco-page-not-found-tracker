// docs:begin row-action-manifest
export const manifests: Array<UmbExtensionManifest> = [
  {
    type: 'entityAction',
    kind: 'default',
    alias: 'My.EntityAction.Report',
    name: 'My report action',
    weight: -20, // below the package's own actions
    // The rows of the two dashboards: tracked URLs and block rules.
    forEntityTypes: ['page-not-found-tracker-url', 'page-not-found-tracker-block-rule'],
    api: () => import('./row-action.js'),
    meta: {
      icon: 'icon-info',
      label: 'Report',
    },
  },
];
// docs:end row-action-manifest
