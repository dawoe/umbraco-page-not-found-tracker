import { css, customElement, html } from '@umbraco-cms/backoffice/external/lit';
import { UmbLitElement } from '@umbraco-cms/backoffice/lit-element';
import { PNFT_BLOCK_RULE_COLLECTION_ALIAS } from '../constants.js';

/**
 * The blocked URLs dashboard: hosts the block rule collection (Umbraco 17.8 has no dashboard kind for a collection,
 * so it renders `umb-collection` itself, as the tracked URLs dashboard does).
 */
@customElement('pnft-blocked-urls-dashboard')
export class PnftBlockedUrlsDashboardElement extends UmbLitElement {
  override render() {
    return html`<umb-collection alias=${PNFT_BLOCK_RULE_COLLECTION_ALIAS}></umb-collection>`;
  }

  static override styles = css`
    :host {
      display: block;
      height: 100%;
    }
  `;
}

export default PnftBlockedUrlsDashboardElement;
export { PnftBlockedUrlsDashboardElement as element };

declare global {
  interface HTMLElementTagNameMap {
    'pnft-blocked-urls-dashboard': PnftBlockedUrlsDashboardElement;
  }
}
