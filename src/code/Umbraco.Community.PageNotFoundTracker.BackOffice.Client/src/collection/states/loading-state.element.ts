import { css, customElement, html } from '@umbraco-cms/backoffice/external/lit';
import { UmbLitElement } from '@umbraco-cms/backoffice/lit-element';
import { PNFT_DATA_MARKS } from '../data-marks.js';

/** The loading state of the tracked URL collection: a loader bar across the top while a list request runs. */
@customElement('pnft-loading-state')
export class PnftLoadingStateElement extends UmbLitElement {
  override render() {
    return html`<uui-loader-bar
      data-mark=${PNFT_DATA_MARKS.stateLoading}
      role="progressbar"
      aria-label=${this.localize.term('pageNotFoundTracker_loading')}></uui-loader-bar>`;
  }

  static override styles = css`
    :host {
      display: block;
      position: absolute;
      inset: 0 0 auto 0;
      z-index: 2;
    }
  `;
}

declare global {
  interface HTMLElementTagNameMap {
    'pnft-loading-state': PnftLoadingStateElement;
  }
}
