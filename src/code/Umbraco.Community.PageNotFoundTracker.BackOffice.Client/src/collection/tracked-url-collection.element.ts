import { UmbCollectionDefaultElement } from '@umbraco-cms/backoffice/collection';
import { css, customElement, html, nothing, state } from '@umbraco-cms/backoffice/external/lit';
import { PNFT_TRACKED_URL_COLLECTION_CONTEXT } from './collection.context-token.js';
import { PNFT_DATA_MARKS } from './data-marks.js';
import { isFilterActive, pickEmptyState, type PnftCollectionErrorKind } from './state.js';
import './filters/domain-culture-filter.element.js';
import './pagination/page-size.element.js';
import './states/empty-state.element.js';
import './states/empty-filtered-state.element.js';
import './states/error-state.element.js';
import './states/loading-state.element.js';
import './states/unavailable-state.element.js';

/**
 * The tracked URL collection element: Umbraco's default collection element (view, toolbar with the
 * `collectionTextFilter` search slot, pager, selection actions) with our toolbar pieces (domain and culture
 * filters, page size, total) and our states (loading, empty, empty after filtering, error, unavailable).
 */
@customElement('pnft-tracked-url-collection')
export class PnftTrackedUrlCollectionElement extends UmbCollectionDefaultElement {
  @state()
  private _loading = false;

  @state()
  private _error?: PnftCollectionErrorKind;

  @state()
  private _filtered = false;

  @state()
  private _total = 0;

  constructor() {
    super();
    this.consumeContext(PNFT_TRACKED_URL_COLLECTION_CONTEXT, (context) => {
      this.observe(context?.loading, (loading) => (this._loading = loading ?? false), 'pnftLoadingObserver');
      this.observe(context?.error, (error) => (this._error = error), 'pnftErrorObserver');
      this.observe(context?.filter, (filter) => (this._filtered = filter ? isFilterActive(filter) : false), 'pnftFilteredObserver');
      this.observe(context?.totalItems, (total) => (this._total = total ?? 0), 'pnftTotalObserver');
    });
  }

  override connectedCallback(): void {
    super.connectedCallback();
    this.setAttribute('data-mark', PNFT_DATA_MARKS.collection);
  }

  override render() {
    return html`${this._loading ? html`<pnft-loading-state></pnft-loading-state>` : nothing}${super.render()}`;
  }

  protected override renderToolbar() {
    return html`
      <umb-collection-toolbar slot="header">
        <div id="toolbar">
          <pnft-domain-culture-filter></pnft-domain-culture-filter>
          <pnft-page-size></pnft-page-size>
          <span id="total" data-mark=${PNFT_DATA_MARKS.total} aria-live="polite"
            >${this._error ? nothing : this.localize.term('pageNotFoundTracker_total', this._total)}</span
          >
        </div>
      </umb-collection-toolbar>
    `;
  }

  protected override _renderEmptyState() {
    switch (pickEmptyState(this._error, this._filtered)) {
      case 'error':
        return html`<pnft-error-state></pnft-error-state>`;
      case 'unavailable':
        return html`<pnft-unavailable-state></pnft-unavailable-state>`;
      case 'empty-filtered':
        return html`<pnft-empty-filtered-state></pnft-empty-filtered-state>`;
      default:
        return html`<pnft-empty-state></pnft-empty-state>`;
    }
  }

  static override styles = [
    ...UmbCollectionDefaultElement.styles,
    css`
      :host {
        position: relative;
      }

      #toolbar {
        display: flex;
        flex-wrap: wrap;
        align-items: center;
        justify-content: flex-end;
        gap: var(--uui-size-space-4);
      }

      #total {
        color: var(--uui-color-text-alt);
        white-space: nowrap;
      }
    `,
  ];
}

export default PnftTrackedUrlCollectionElement;
export { PnftTrackedUrlCollectionElement as element };

declare global {
  interface HTMLElementTagNameMap {
    'pnft-tracked-url-collection': PnftTrackedUrlCollectionElement;
  }
}
