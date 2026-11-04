import { customElement, html } from '@umbraco-cms/backoffice/external/lit';
import { PNFT_DATA_MARKS } from '../../data-marks.js';
import type { PnftTrackedUrlCollectionModel } from '../../types.js';
import { PnftPagedTableElementBase, type PnftTableRowData } from './paged-table.element.base.js';
import { PNFT_COLUMNS, formatLocalDateTime } from './tracked-url-table.columns.js';

/**
 * Cell styles are inline on purpose: `umb-table` renders the cell templates inside its own shadow root, so rules in
 * this element's `static styles` never reach them (the type icon and label stacked instead of centring).
 */
const CELL_STYLES = {
  url: 'word-break: break-all;',
  type: 'display: inline-flex; align-items: center; gap: var(--uui-size-space-2);',
} as const;

/**
 * The table view of the tracked URL collection: the package's table base with the tracked URL columns. Each row is an
 * entity of `PNFT_TRACKED_URL_ENTITY_TYPE`, so the create redirect and block actions appear per row.
 */
@customElement('pnft-tracked-url-table')
export class PnftTrackedUrlTableElement extends PnftPagedTableElementBase<PnftTrackedUrlCollectionModel> {
  protected override readonly columns = PNFT_COLUMNS;

  protected override readonly rowMark = PNFT_DATA_MARKS.row;

  protected override entityName(item: PnftTrackedUrlCollectionModel): string {
    return item.url;
  }

  protected override renderRowData(item: PnftTrackedUrlCollectionModel): PnftTableRowData {
    return [
      { columnAlias: 'url', value: html`<span class="url" style=${CELL_STYLES.url} title=${item.url}>${item.url}</span>` },
      { columnAlias: 'domain', value: item.domain || this.renderMuted('pageNotFoundTracker_noDomain') },
      { columnAlias: 'culture', value: item.culture || this.renderMuted('pageNotFoundTracker_noCulture') },
      { columnAlias: 'type', value: this.#renderType(item.isFile) },
      { columnAlias: 'firstSeen', value: html`<span title=${item.firstSeen}>${formatLocalDateTime(item.firstSeen)}</span>` },
      { columnAlias: 'lastSeen', value: html`<span title=${item.lastSeen}>${formatLocalDateTime(item.lastSeen)}</span>` },
      { columnAlias: 'hits', value: item.hitCount.toLocaleString() },
    ];
  }

  #renderType(isFile: boolean) {
    const label = this.localize.term(isFile ? 'pageNotFoundTracker_typeFile' : 'pageNotFoundTracker_typePage');

    return html`<span class="type" style=${CELL_STYLES.type}><umb-icon name=${isFile ? 'icon-document' : 'icon-globe'}></umb-icon>${label}</span>`;
  }
}

export default PnftTrackedUrlTableElement;
export { PnftTrackedUrlTableElement as element };

declare global {
  interface HTMLElementTagNameMap {
    'pnft-tracked-url-table': PnftTrackedUrlTableElement;
  }
}
