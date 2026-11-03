import { UmbCollectionViewElementBase } from '@umbraco-cms/backoffice/collection';
import type { UmbTableColumn, UmbTableConfig, UmbTableElement, UmbTableItem } from '@umbraco-cms/backoffice/components';
import { UmbElementControllerHost } from '@umbraco-cms/backoffice/controller-api';
import { UmbEntityContext } from '@umbraco-cms/backoffice/entity';
import { css, customElement, html, state, type PropertyValues } from '@umbraco-cms/backoffice/external/lit';
import { UmbTextStyles } from '@umbraco-cms/backoffice/style';
import { PNFT_TRACKED_URL_COLLECTION_CONTEXT } from '../../collection.context-token.js';
import type { PnftTrackedUrlCollectionContext } from '../../collection.context.js';
import { PNFT_DATA_MARKS } from '../../data-marks.js';
import type { PnftTrackedUrlCollectionFilterModel, PnftTrackedUrlCollectionModel } from '../../types.js';
import {
  PNFT_COLUMNS,
  PNFT_ENTITY_ACTIONS_COLUMN,
  columnAliasOf,
  formatLocalDateTime,
  sortColumnOf,
  type PnftColumnAlias,
} from './tracked-url-table.columns.js';

/**
 * The table view of the tracked URL collection. Built on the framework's collection view base (items,
 * selection, item actions setting) and `umb-table` (header sorting UI, rows). Each row is an entity of
 * `PNFT_TRACKED_URL_ENTITY_TYPE` with its own entity context, and the last column is the framework's entity
 * actions column, so entity actions registered for that entity type (specs 5 and 6) appear per row.
 *
 * The framework's `table` kind is not used: it has no column sorting and renders every value as plain text.
 */
@customElement('pnft-tracked-url-table')
export class PnftTrackedUrlTableElement extends UmbCollectionViewElementBase<PnftTrackedUrlCollectionModel> {
  @state()
  private _tableColumns: Array<UmbTableColumn> = [];

  @state()
  private _tableRows: Array<UmbTableItem> = [];

  @state()
  private _orderingColumn: PnftColumnAlias | '' = 'hits';

  @state()
  private _orderingDesc = true;

  #tableConfig: UmbTableConfig = { allowSelection: false, allowSelectAll: false, hideIcon: true };

  #context?: PnftTrackedUrlCollectionContext;

  #rowContexts = new Map<string, { host: UmbElementControllerHost; entityContext: UmbEntityContext }>();

  constructor() {
    super();
    this.consumeContext(PNFT_TRACKED_URL_COLLECTION_CONTEXT, (context) => {
      this.#context = context;
      this.observe(
        context?.filter,
        (filter) => {
          const { orderBy, orderDirection } = (filter ?? {}) as PnftTrackedUrlCollectionFilterModel;
          this._orderingColumn = columnAliasOf(orderBy) ?? '';
          this._orderingDesc = orderDirection !== 'Ascending';
        },
        'pnftOrderingObserver',
      );
    });
  }

  override willUpdate(changedProperties: PropertyValues): void {
    super.willUpdate(changedProperties);

    if (changedProperties.has('_items') || changedProperties.has('_hideItemActions')) {
      this.#buildColumns();
      this.#buildRows();
    }
  }

  #buildColumns() {
    const columns: Array<UmbTableColumn> = PNFT_COLUMNS.map((column) => ({
      // umb-table renders the name inside its sort button; a template lets the header carry its data-mark.
      // The cast mirrors 17.8 umb-table header rendering (name is typed string); re-check on an Umbraco upgrade.
      name: html`<span data-mark=${column.sortMark}>${this.localize.term(column.labelKey)}</span>` as unknown as string,
      alias: column.alias,
      allowSorting: true,
      align: column.align,
    }));

    if (!this._hideItemActions) {
      columns.push({
        name: '',
        alias: PNFT_ENTITY_ACTIONS_COLUMN,
        align: 'right',
        elementName: 'umb-entity-actions-table-column-view',
      });
    }

    this._tableColumns = columns;
  }

  #buildRows() {
    this._tableRows = this._items.map((item) => ({
      id: item.unique,
      entityType: item.entityType,
      data: [
        { columnAlias: 'url', value: html`<span class="url" title=${item.url}>${item.url}</span>` },
        { columnAlias: 'domain', value: item.domain || this.#renderNone('pageNotFoundTracker_noDomain') },
        { columnAlias: 'culture', value: item.culture || this.#renderNone('pageNotFoundTracker_noCulture') },
        { columnAlias: 'type', value: this.#renderType(item.isFile) },
        { columnAlias: 'firstSeen', value: html`<span title=${item.firstSeen}>${formatLocalDateTime(item.firstSeen)}</span>` },
        { columnAlias: 'lastSeen', value: html`<span title=${item.lastSeen}>${formatLocalDateTime(item.lastSeen)}</span>` },
        { columnAlias: 'hits', value: item.hitCount.toLocaleString() },
        { columnAlias: PNFT_ENTITY_ACTIONS_COLUMN, value: { name: item.url } },
      ],
    }));

    const currentIds = new Set(this._tableRows.map((row) => row.id));

    for (const [id, rowContext] of this.#rowContexts) {
      if (!currentIds.has(id)) {
        rowContext.host.destroy();
        this.#rowContexts.delete(id);
      }
    }
  }

  #renderNone(key: string) {
    return html`<em class="none">${this.localize.term(key)}</em>`;
  }

  #renderType(isFile: boolean) {
    const label = this.localize.term(isFile ? 'pageNotFoundTracker_typeFile' : 'pageNotFoundTracker_typePage');

    return html`<span class="type"><umb-icon name=${isFile ? 'icon-document' : 'icon-globe'}></umb-icon>${label}</span>`;
  }

  /** Marks each row for tests and gives it an entity context (entity type and unique) for its entity actions. */
  #onRowRendered = (element: HTMLElement | undefined, item: UmbTableItem) => {
    const existing = this.#rowContexts.get(item.id);

    if (!element) {
      existing?.host.destroy();
      this.#rowContexts.delete(item.id);
      return;
    }

    element.setAttribute('data-mark', PNFT_DATA_MARKS.row);
    element.setAttribute('data-unique', item.id);

    if (existing) {
      existing.entityContext.setEntityType(item.entityType);
      existing.entityContext.setUnique(item.id);
      return;
    }

    const host = new UmbElementControllerHost(element);
    host.hostConnected();
    const entityContext = new UmbEntityContext(host);
    entityContext.setEntityType(item.entityType);
    entityContext.setUnique(item.id);
    this.#rowContexts.set(item.id, { host, entityContext });
  };

  #onOrdered(event: Event) {
    const table = event.target as UmbTableElement;
    const orderBy = sortColumnOf(table.orderingColumn);

    if (!orderBy) {
      return;
    }

    this.#context?.setFilter({ orderBy, orderDirection: table.orderingDesc ? 'Descending' : 'Ascending' });
  }

  override disconnectedCallback(): void {
    super.disconnectedCallback();

    for (const [, rowContext] of this.#rowContexts) {
      rowContext.host.destroy();
    }

    this.#rowContexts.clear();
  }

  override render() {
    return html`
      <umb-table
        .config=${this.#tableConfig}
        .columns=${this._tableColumns}
        .items=${this._tableRows}
        .orderingColumn=${this._orderingColumn}
        .orderingDesc=${this._orderingDesc}
        .onRowRendered=${this.#onRowRendered}
        @ordered=${this.#onOrdered}></umb-table>
    `;
  }

  static override styles = [
    UmbTextStyles,
    css`
      :host {
        display: flex;
        flex-direction: column;
      }

      .url {
        word-break: break-all;
      }

      .none {
        color: var(--uui-color-text-alt);
      }

      .type {
        display: inline-flex;
        align-items: center;
        gap: var(--uui-size-space-2);
      }
    `,
  ];
}

export default PnftTrackedUrlTableElement;
export { PnftTrackedUrlTableElement as element };

declare global {
  interface HTMLElementTagNameMap {
    'pnft-tracked-url-table': PnftTrackedUrlTableElement;
  }
}
