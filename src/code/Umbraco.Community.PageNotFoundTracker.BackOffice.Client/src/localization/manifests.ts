export const manifests: Array<UmbExtensionManifest> = [
  {
    type: 'localization',
    alias: 'Pnft.Localization.En',
    name: 'Page Not Found Tracker English Localization',
    meta: {
      culture: 'en',
    },
    js: () => import('./en.js'),
  },
];
