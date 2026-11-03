import { customElement } from '@umbraco-cms/backoffice/external/lit';
import { PNFT_DATA_MARKS } from '../data-marks.js';
import { PnftStateMessageElementBase } from './state-message.base.js';

/** The "empty" state of the tracked URL collection. */
@customElement('pnft-empty-state')
export class PnftEmptyStateElement extends PnftStateMessageElementBase {
  protected override readonly mark = PNFT_DATA_MARKS.stateEmpty;
  protected override readonly headlineKey = 'pageNotFoundTracker_emptyHeadline';
  protected override readonly textKey = 'pageNotFoundTracker_emptyText';
  protected override readonly icon = 'icon-document';
}

declare global {
  interface HTMLElementTagNameMap {
    'pnft-empty-state': PnftEmptyStateElement;
  }
}
