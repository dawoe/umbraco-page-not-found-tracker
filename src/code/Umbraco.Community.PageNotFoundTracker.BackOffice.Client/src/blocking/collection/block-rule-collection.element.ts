import { customElement } from '@umbraco-cms/backoffice/external/lit';
import { PnftPagedCollectionElementBase, type PnftPagedCollectionSettings } from '../../collection/paged-collection.element.base.js';
import { PNFT_BLOCK_DATA_MARKS } from '../data-marks.js';

/** The `data-mark`s, texts and states of the block rule collection. */
const SETTINGS: PnftPagedCollectionSettings = {
  collectionMark: PNFT_BLOCK_DATA_MARKS.collection,
  totalMark: PNFT_BLOCK_DATA_MARKS.total,
  totalKey: 'pageNotFoundTracker_blockTotal',
  pageSizeMark: PNFT_BLOCK_DATA_MARKS.pageSize,
  loading: { mark: PNFT_BLOCK_DATA_MARKS.stateLoading, labelKey: 'pageNotFoundTracker_blockLoading' },
  filter: {
    domainMark: PNFT_BLOCK_DATA_MARKS.filterDomain,
    cultureMark: PNFT_BLOCK_DATA_MARKS.filterCulture,
    allDomainsKey: 'pageNotFoundTracker_anyDomain',
    emptyDomainKey: 'pageNotFoundTracker_rulesForAllDomains',
    allCulturesKey: 'pageNotFoundTracker_anyCulture',
    emptyCultureKey: 'pageNotFoundTracker_rulesForAllCultures',
  },
  states: {
    empty: {
      mark: PNFT_BLOCK_DATA_MARKS.stateEmpty,
      headlineKey: 'pageNotFoundTracker_blockEmptyHeadline',
      textKey: 'pageNotFoundTracker_blockEmptyText',
      icon: 'icon-block',
    },
    'empty-filtered': {
      mark: PNFT_BLOCK_DATA_MARKS.stateEmptyFiltered,
      headlineKey: 'pageNotFoundTracker_blockEmptyFilteredHeadline',
      textKey: 'pageNotFoundTracker_blockEmptyFilteredText',
      icon: 'icon-search',
    },
    error: {
      mark: PNFT_BLOCK_DATA_MARKS.stateError,
      headlineKey: 'pageNotFoundTracker_blockErrorHeadline',
      textKey: 'pageNotFoundTracker_errorText',
      icon: 'icon-alert',
    },
    unavailable: {
      mark: PNFT_BLOCK_DATA_MARKS.stateUnavailable,
      headlineKey: 'pageNotFoundTracker_unavailableHeadline',
      textKey: 'pageNotFoundTracker_unavailableText',
      icon: 'icon-alert',
    },
  },
};

/**
 * The block rule collection element: the package's paged collection element with the block rule texts and marks. The
 * toolbar also holds the "Add rule" collection action (Umbraco's collection action bundle).
 */
@customElement('pnft-block-rule-collection')
export class PnftBlockRuleCollectionElement extends PnftPagedCollectionElementBase {
  protected override readonly settings = SETTINGS;
}

export default PnftBlockRuleCollectionElement;
export { PnftBlockRuleCollectionElement as element };

declare global {
  interface HTMLElementTagNameMap {
    'pnft-block-rule-collection': PnftBlockRuleCollectionElement;
  }
}
