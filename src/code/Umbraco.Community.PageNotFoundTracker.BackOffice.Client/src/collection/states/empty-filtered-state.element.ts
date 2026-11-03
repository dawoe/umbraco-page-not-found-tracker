import { customElement } from '@umbraco-cms/backoffice/external/lit';
import { PNFT_DATA_MARKS } from '../data-marks.js';
import { PnftStateMessageElementBase } from './state-message.base.js';

/** The "empty-filtered" state of the tracked URL collection. */
@customElement('pnft-empty-filtered-state')
export class PnftEmptyFilteredStateElement extends PnftStateMessageElementBase {
  protected override readonly mark = PNFT_DATA_MARKS.stateEmptyFiltered;
  protected override readonly headlineKey = 'pageNotFoundTracker_emptyFilteredHeadline';
  protected override readonly textKey = 'pageNotFoundTracker_emptyFilteredText';
  protected override readonly icon = 'icon-search';
}

declare global {
  interface HTMLElementTagNameMap {
    'pnft-empty-filtered-state': PnftEmptyFilteredStateElement;
  }
}
