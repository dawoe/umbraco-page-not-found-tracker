import { customElement } from '@umbraco-cms/backoffice/external/lit';
import { PNFT_DATA_MARKS } from './data-marks.js';
import { PNFT_TRACKED_URL_FILTER_SETTINGS } from './filters/domain-culture-filter.element.js';
import { PnftPagedCollectionElementBase, type PnftPagedCollectionSettings } from './paged-collection.element.base.js';

/** The `data-mark`s, texts and states of the tracked URL collection. */
const SETTINGS: PnftPagedCollectionSettings = {
  collectionMark: PNFT_DATA_MARKS.collection,
  totalMark: PNFT_DATA_MARKS.total,
  totalKey: 'pageNotFoundTracker_total',
  pageSizeMark: PNFT_DATA_MARKS.pageSize,
  loading: { mark: PNFT_DATA_MARKS.stateLoading, labelKey: 'pageNotFoundTracker_loading' },
  filter: PNFT_TRACKED_URL_FILTER_SETTINGS,
  states: {
    empty: {
      mark: PNFT_DATA_MARKS.stateEmpty,
      headlineKey: 'pageNotFoundTracker_emptyHeadline',
      textKey: 'pageNotFoundTracker_emptyText',
      icon: 'icon-document',
    },
    'empty-filtered': {
      mark: PNFT_DATA_MARKS.stateEmptyFiltered,
      headlineKey: 'pageNotFoundTracker_emptyFilteredHeadline',
      textKey: 'pageNotFoundTracker_emptyFilteredText',
      icon: 'icon-search',
    },
    error: {
      mark: PNFT_DATA_MARKS.stateError,
      headlineKey: 'pageNotFoundTracker_errorHeadline',
      textKey: 'pageNotFoundTracker_errorText',
      icon: 'icon-alert',
    },
    unavailable: {
      mark: PNFT_DATA_MARKS.stateUnavailable,
      headlineKey: 'pageNotFoundTracker_unavailableHeadline',
      textKey: 'pageNotFoundTracker_unavailableText',
      icon: 'icon-alert',
    },
  },
};

/** The tracked URL collection element: the package's paged collection element with the tracked URL texts and marks. */
@customElement('pnft-tracked-url-collection')
export class PnftTrackedUrlCollectionElement extends PnftPagedCollectionElementBase {
  protected override readonly settings = SETTINGS;
}

export default PnftTrackedUrlCollectionElement;
export { PnftTrackedUrlCollectionElement as element };

declare global {
  interface HTMLElementTagNameMap {
    'pnft-tracked-url-collection': PnftTrackedUrlCollectionElement;
  }
}
