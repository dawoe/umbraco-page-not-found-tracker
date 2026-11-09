import { css, customElement, html, property } from '@umbraco-cms/backoffice/external/lit';
import { UmbLitElement } from '@umbraco-cms/backoffice/lit-element';
import { PNFT_DATA_MARKS } from '../data-marks.js';

/** The loading state of a package collection: a loader bar across the top while a list request runs. */
@customElement('pnft-loading-state')
export class PnftLoadingStateElement extends UmbLitElement {
  /** The `data-mark` of the loader bar (default: the tracked URL collection's). */
  @property({ attribute: false })
  mark: string = PNFT_DATA_MARKS.stateLoading;

  /** The localization key of the loader's accessible label. */
  @property({ attribute: false })
  labelKey = 'pageNotFoundTracker_loading';

  override render() {
    return html`<uui-loader-bar
      data-mark=${this.mark}
      role="progressbar"
      aria-label=${this.localize.term(this.labelKey)}></uui-loader-bar>`;
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
