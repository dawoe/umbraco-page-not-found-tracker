import { customElement } from '@umbraco-cms/backoffice/external/lit';
import { PnftSearchElementBase } from '../../../collection/filters/search.base.js';
import { PNFT_BLOCK_DATA_MARKS } from '../../data-marks.js';

/** The pattern search box of the block rule collection. */
@customElement('pnft-block-rule-search')
export class PnftBlockRuleSearchElement extends PnftSearchElementBase {
  protected override readonly mark = PNFT_BLOCK_DATA_MARKS.search;
  protected override readonly labelKey = 'pageNotFoundTracker_searchPatterns';
  protected override readonly placeholderKey = 'pageNotFoundTracker_searchPatternsPlaceholder';
}

export { PnftBlockRuleSearchElement as element };

declare global {
  interface HTMLElementTagNameMap {
    'pnft-block-rule-search': PnftBlockRuleSearchElement;
  }
}
