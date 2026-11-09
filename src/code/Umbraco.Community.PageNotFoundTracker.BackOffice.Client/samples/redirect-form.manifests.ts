// docs:begin redirect-form-manifest
export const manifests: Array<UmbExtensionManifest> = [
  {
    type: 'pnftRedirectForm',
    alias: 'My.RedirectForm',
    name: 'My redirect form',
    weight: 10, // anything above -100 beats the package's default form
    element: () => import('./redirect-form.element.js'),
  },
];
// docs:end redirect-form-manifest
