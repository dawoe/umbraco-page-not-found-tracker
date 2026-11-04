import type { ManifestElement, ManifestWithDynamicConditions } from '@umbraco-cms/backoffice/extension-api';
import type { PnftTrackedUrlCollectionModel } from '../collection/types.js';

/**
 * The contract of a redirect form element. The create redirect modal sets `trackedUrl` and calls `getPayload()` when
 * the editor confirms; the form never calls the API itself. The payload is sent as-is to the server, where the active
 * redirect provider reads it (the default provider expects `{ targetKey: '<document key>' }`).
 */
export interface PnftRedirectFormElement extends HTMLElement {
  /** The tracked URL (the dashboard row) the redirect is created for. */
  trackedUrl?: PnftTrackedUrlCollectionModel;

  /**
   * Returns the payload for the redirect provider, or `undefined` when the form is not valid. On `undefined` the form
   * shows its own validation messages and the modal sends nothing.
   */
  getPayload(): Promise<unknown | undefined>;
}

/**
 * A redirect form: the form the create redirect modal hosts. If several are registered (and their conditions allow),
 * the one with the highest `weight` wins. The package's default form has weight -100, so any form with a weight of 0
 * or more replaces it.
 */
export interface ManifestPnftRedirectForm
  extends ManifestElement<PnftRedirectFormElement>,
    ManifestWithDynamicConditions<UmbExtensionConditionConfig> {
  type: 'pnftRedirectForm';
}

declare global {
  interface UmbExtensionManifestMap {
    pnftRedirectForm: ManifestPnftRedirectForm;
  }
}
