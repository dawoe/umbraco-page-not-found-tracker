import { customElement } from '@umbraco-cms/backoffice/external/lit';
import { PNFT_DATA_MARKS } from '../data-marks.js';
import { PnftStateMessageElementBase } from './state-message.base.js';

/** The "error" state of the tracked URL collection. */
@customElement('pnft-error-state')
export class PnftErrorStateElement extends PnftStateMessageElementBase {
  protected override readonly mark = PNFT_DATA_MARKS.stateError;
  protected override readonly headlineKey = 'pageNotFoundTracker_errorHeadline';
  protected override readonly textKey = 'pageNotFoundTracker_errorText';
  protected override readonly icon = 'icon-alert';
}

declare global {
  interface HTMLElementTagNameMap {
    'pnft-error-state': PnftErrorStateElement;
  }
}
