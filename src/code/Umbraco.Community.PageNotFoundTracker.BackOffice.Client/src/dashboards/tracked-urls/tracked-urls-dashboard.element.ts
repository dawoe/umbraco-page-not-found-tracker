import { css, customElement, html } from '@umbraco-cms/backoffice/external/lit';
import { UmbLitElement } from '@umbraco-cms/backoffice/lit-element';
import { PNFT_TRACKED_URL_COLLECTION_ALIAS } from '../../collection/constants.js';

/**
 * The tracked URLs dashboard: hosts the tracked URL collection. Umbraco 17.8 has no dashboard kind for a
 * collection (the `collection` kind exists for workspace views only), so the dashboard renders
 * `umb-collection` itself, as Umbraco's collection workspace view does.
 */
@customElement('pnft-tracked-urls-dashboard')
export class PnftTrackedUrlsDashboardElement extends UmbLitElement {
  override render() {
    return html`<umb-collection alias=${PNFT_TRACKED_URL_COLLECTION_ALIAS}></umb-collection>`;
  }

  static override styles = css`
    :host {
      display: block;
      height: 100%;
    }
  `;
}

export default PnftTrackedUrlsDashboardElement;
export { PnftTrackedUrlsDashboardElement as element };

declare global {
  interface HTMLElementTagNameMap {
    'pnft-tracked-urls-dashboard': PnftTrackedUrlsDashboardElement;
  }
}
