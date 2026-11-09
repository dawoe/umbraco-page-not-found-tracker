import { css, customElement, html, property, state } from '@umbraco-cms/backoffice/external/lit';
import { UmbLitElement } from '@umbraco-cms/backoffice/lit-element';
import type { UUISelectEvent } from '@umbraco-cms/backoffice/external/uui';
import { PNFT_PAGED_COLLECTION_CONTEXT, type PnftAnyPagedCollectionContext } from '../collection.context-token.js';
import { PNFT_DATA_MARKS } from '../data-marks.js';
import { PNFT_DEFAULT_PAGE_SIZE, PNFT_PAGE_SIZES } from '../state.js';

/**
 * The page size choice (20, 50 or 100) of the nearest package collection. The pager itself is the framework's
 * `umb-collection-pagination`.
 */
@customElement('pnft-page-size')
export class PnftPageSizeElement extends UmbLitElement {
  /** The `data-mark` of the select (default: the tracked URL collection's). */
  @property({ attribute: false })
  mark: string = PNFT_DATA_MARKS.pageSize;

  @state()
  private _pageSize = PNFT_DEFAULT_PAGE_SIZE;

  #context?: PnftAnyPagedCollectionContext;

  constructor() {
    super();
    this.consumeContext(PNFT_PAGED_COLLECTION_CONTEXT, (context) => {
      this.#context = context;
      this.observe(context?.pagination.pageSize, (pageSize) => (this._pageSize = pageSize ?? PNFT_DEFAULT_PAGE_SIZE), 'pnftPageSizeObserver');
    });
  }

  #onChange(event: UUISelectEvent) {
    this.#context?.setPageSize(Number(event.target.value));
  }

  override render() {
    return html`
      <uui-select
        data-mark=${this.mark}
        label=${this.localize.term('pageNotFoundTracker_pageSize')}
        title=${this.localize.term('pageNotFoundTracker_pageSize')}
        .options=${PNFT_PAGE_SIZES.map((size) => ({ name: String(size), value: String(size), selected: size === this._pageSize }))}
        @change=${this.#onChange}></uui-select>
    `;
  }

  static override styles = css`
    :host {
      display: block;
    }
  `;
}

declare global {
  interface HTMLElementTagNameMap {
    'pnft-page-size': PnftPageSizeElement;
  }
}
