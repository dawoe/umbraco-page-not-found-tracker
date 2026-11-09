import { customElement, html } from '@umbraco-cms/backoffice/external/lit';
import { PnftPagedTableElementBase, type PnftTableRowData } from '../../../../collection/views/table/paged-table.element.base.js';
import { formatLocalDateTime } from '../../../../collection/views/table/table-columns.js';
import { PNFT_BLOCK_DATA_MARKS } from '../../../data-marks.js';
import type { PnftBlockRuleCollectionModel } from '../../types.js';
import { PNFT_BLOCK_RULE_COLUMNS } from './block-rule-table.columns.js';

/** Inline on purpose: `umb-table` renders cell templates in its own shadow root, out of reach of `static styles`. */
const PATTERN_STYLE = 'word-break: break-all; font-family: var(--uui-font-family-monospace, monospace);';

/**
 * The table view of the block rule collection: the package's table base with the block rule columns. Each row is an
 * entity of `PNFT_BLOCK_RULE_ENTITY_TYPE`, so the unblock action appears per row. An empty domain or culture shows the
 * localized "All domains"/"All cultures".
 */
@customElement('pnft-block-rule-table')
export class PnftBlockRuleTableElement extends PnftPagedTableElementBase<PnftBlockRuleCollectionModel> {
  protected override readonly columns = PNFT_BLOCK_RULE_COLUMNS;

  protected override readonly rowMark = PNFT_BLOCK_DATA_MARKS.row;

  protected override entityName(item: PnftBlockRuleCollectionModel): string {
    return item.pattern;
  }

  protected override renderRowData(item: PnftBlockRuleCollectionModel): PnftTableRowData {
    return [
      { columnAlias: 'pattern', value: html`<span style=${PATTERN_STYLE} title=${item.pattern}>${item.pattern}</span>` },
      { columnAlias: 'statusCode', value: String(item.statusCode) },
      { columnAlias: 'domain', value: item.domain || this.renderMuted('pageNotFoundTracker_allDomains') },
      { columnAlias: 'culture', value: item.culture || this.renderMuted('pageNotFoundTracker_allCultures') },
      { columnAlias: 'created', value: html`<span title=${item.created}>${formatLocalDateTime(item.created)}</span>` },
    ];
  }
}

export default PnftBlockRuleTableElement;
export { PnftBlockRuleTableElement as element };

declare global {
  interface HTMLElementTagNameMap {
    'pnft-block-rule-table': PnftBlockRuleTableElement;
  }
}
