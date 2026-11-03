import { customElement } from '@umbraco-cms/backoffice/external/lit';
import { PNFT_DATA_MARKS } from '../data-marks.js';
import { PnftStateMessageElementBase } from './state-message.base.js';

/** The "unavailable" state of the tracked URL collection. */
@customElement('pnft-unavailable-state')
export class PnftUnavailableStateElement extends PnftStateMessageElementBase {
  protected override readonly mark = PNFT_DATA_MARKS.stateUnavailable;
  protected override readonly headlineKey = 'pageNotFoundTracker_unavailableHeadline';
  protected override readonly textKey = 'pageNotFoundTracker_unavailableText';
  protected override readonly icon = 'icon-alert';
}

declare global {
  interface HTMLElementTagNameMap {
    'pnft-unavailable-state': PnftUnavailableStateElement;
  }
}
