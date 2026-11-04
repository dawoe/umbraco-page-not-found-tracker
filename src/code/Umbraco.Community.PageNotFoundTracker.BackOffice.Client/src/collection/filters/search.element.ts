import { customElement } from '@umbraco-cms/backoffice/external/lit';
import { PNFT_DATA_MARKS } from '../data-marks.js';
import { PnftSearchElementBase } from './search.base.js';

/** The URL search box of the tracked URL collection. */
@customElement('pnft-tracked-url-search')
export class PnftTrackedUrlSearchElement extends PnftSearchElementBase {
  protected override readonly mark = PNFT_DATA_MARKS.search;
  protected override readonly labelKey = 'pageNotFoundTracker_search';
  protected override readonly placeholderKey = 'pageNotFoundTracker_searchPlaceholder';
}

export { PnftTrackedUrlSearchElement as element };

declare global {
  interface HTMLElementTagNameMap {
    'pnft-tracked-url-search': PnftTrackedUrlSearchElement;
  }
}
