import { UmbCollectionViewElementBase } from '@umbraco-cms/backoffice/collection';
import type { UmbTableColumn, UmbTableConfig, UmbTableElement, UmbTableItem } from '@umbraco-cms/backoffice/components';
import { UmbElementControllerHost } from '@umbraco-cms/backoffice/controller-api';
import { UmbEntityContext } from '@umbraco-cms/backoffice/entity';
import { css, html, state, type PropertyValues } from '@umbraco-cms/backoffice/external/lit';
import { UmbTextStyles } from '@umbraco-cms/backoffice/style';
import { PNFT_PAGED_COLLECTION_CONTEXT, type PnftAnyPagedCollectionContext } from '../../collection.context-token.js';
import type { PnftPagedCollectionFilterModel } from '../../types.js';
import { PNFT_ENTITY_ACTIONS_COLUMN, columnAliasIn, sortColumnIn, type PnftTableColumnDefinition } from './table-columns.js';

/** The cell data of one table row (without the entity actions cell, which the base adds). */
export type PnftTableRowData = UmbTableItem['data'];

/**
 * The base of the package's table views. Built on the framework's collection view base (items, selection, item
 * actions setting) and `umb-table` (header sorting UI, rows). Each row is an entity with its own entity context
 * (entity type and `unique` of the item), and the last column is the framework's entity actions column, so entity
 * actions registered for the row's entity type appear per row. A subclass names its columns, its row `data-mark` and
 * renders its cells.
 *
 * The framework's `table` kind is not used: it has no column sorting and renders every value as plain text.
 */
export abstract class PnftPagedTableElementBase<TItem extends { entityType: string; unique: string }> extends UmbCollectionViewElementBase<TItem> {
  /** The data columns in display order. */
  protected abstract readonly columns: ReadonlyArray<PnftTableColumnDefinition>;

  /** The `data-mark` of every row. */
  protected abstract readonly rowMark: string;

  /** The cells of one row, by column alias. */
  protected abstract renderRowData(item: TItem): PnftTableRowData;

  /** The name of a row in the entity actions' accessible labels. */
  protected abstract entityName(item: TItem): string;

  @state()
  private _tableColumns: Array<UmbTableColumn> = [];

  @state()
  private _tableRows: Array<UmbTableItem> = [];

  @state()
  private _orderingColumn = '';

  @state()
  private _orderingDesc = true;

  #tableConfig: UmbTableConfig = { allowSelection: false, allowSelectAll: false, hideIcon: true };

  #context?: PnftAnyPagedCollectionContext;

  #rowContexts = new Map<string, { host: UmbElementControllerHost; entityContext: UmbEntityContext }>();

  constructor() {
    super();
    this.consumeContext(PNFT_PAGED_COLLECTION_CONTEXT, (context) => {
      this.#context = context;
      this.observe(
        context?.filter,
        (filter) => {
          const { orderBy, orderDirection } = (filter ?? {}) as PnftPagedCollectionFilterModel;
          this._orderingColumn = columnAliasIn(this.columns, orderBy) ?? '';
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
    const columns: Array<UmbTableColumn> = this.columns.map((column) => ({
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
      data: [...this.renderRowData(item), { columnAlias: PNFT_ENTITY_ACTIONS_COLUMN, value: { name: this.entityName(item) } }],
    }));

    const currentIds = new Set(this._tableRows.map((row) => row.id));

    for (const [id, rowContext] of this.#rowContexts) {
      if (!currentIds.has(id)) {
        rowContext.host.destroy();
        this.#rowContexts.delete(id);
      }
    }
  }

  /** Renders the "none"/"all" text of an empty cell. */
  protected renderMuted(key: string) {
    return html`<em class="none" style="color: var(--uui-color-text-alt);">${this.localize.term(key)}</em>`;
  }

  /** Marks each row for tests and gives it an entity context (entity type and unique) for its entity actions. */
  #onRowRendered = (element: HTMLElement | undefined, item: UmbTableItem) => {
    const existing = this.#rowContexts.get(item.id);

    if (!element) {
      existing?.host.destroy();
      this.#rowContexts.delete(item.id);
      return;
    }

    element.setAttribute('data-mark', this.rowMark);
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
    const orderBy = sortColumnIn(this.columns, table.orderingColumn);

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
    `,
  ];
}
