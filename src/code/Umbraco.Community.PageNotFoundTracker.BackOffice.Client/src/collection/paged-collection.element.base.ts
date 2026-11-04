import { UmbCollectionDefaultElement } from '@umbraco-cms/backoffice/collection';
import { css, html, nothing, state } from '@umbraco-cms/backoffice/external/lit';
import { PNFT_PAGED_COLLECTION_CONTEXT } from './collection.context-token.js';
import type { PnftDomainCultureFilterSettings } from './filters/domain-culture-filter.element.js';
import { isFilterActive, pickEmptyState, type PnftCollectionErrorKind, type PnftEmptyState } from './state.js';
import type { PnftStateMessage } from './states/state-message.element.js';
import './filters/domain-culture-filter.element.js';
import './pagination/page-size.element.js';
import './states/loading-state.element.js';
import './states/state-message.element.js';

/** What a package collection element shows: its `data-mark`s, texts and states. */
export interface PnftPagedCollectionSettings {
  /** The `data-mark` of the collection element itself. */
  collectionMark: string;
  /** The `data-mark` of the total. */
  totalMark: string;
  /** The localization key of the total (a function of the count). */
  totalKey: string;
  /** The `data-mark` of the page size select. */
  pageSizeMark: string;
  /** The loader bar's `data-mark` and label key. */
  loading: { mark: string; labelKey: string };
  /** The domain and culture filters. */
  filter: PnftDomainCultureFilterSettings;
  /** The message states. */
  states: Record<PnftEmptyState, PnftStateMessage>;
}

/**
 * The base of the package's collection elements: Umbraco's default collection element (view, toolbar with the
 * collection actions and the `collectionTextFilter` search slot, pager, selection actions) with the package's toolbar
 * pieces (domain and culture filters, page size, total) and states (loading, empty, empty after filtering, error,
 * unavailable). A subclass names its `data-mark`s and texts.
 */
export abstract class PnftPagedCollectionElementBase extends UmbCollectionDefaultElement {
  /** The collection's `data-mark`s, texts and states. */
  protected abstract readonly settings: PnftPagedCollectionSettings;

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
    this.consumeContext(PNFT_PAGED_COLLECTION_CONTEXT, (context) => {
      this.observe(context?.loading, (loading) => (this._loading = loading ?? false), 'pnftLoadingObserver');
      this.observe(context?.error, (error) => (this._error = error), 'pnftErrorObserver');
      this.observe(context?.filter, (filter) => (this._filtered = filter ? isFilterActive(filter) : false), 'pnftFilteredObserver');
      this.observe(context?.totalItems, (total) => (this._total = total ?? 0), 'pnftTotalObserver');
    });
  }

  override connectedCallback(): void {
    super.connectedCallback();
    this.setAttribute('data-mark', this.settings.collectionMark);
  }

  override render() {
    const loading = this.settings.loading;

    return html`${this._loading
      ? html`<pnft-loading-state .mark=${loading.mark} .labelKey=${loading.labelKey}></pnft-loading-state>`
      : nothing}${super.render()}`;
  }

  protected override renderToolbar() {
    return html`
      <umb-collection-toolbar slot="header">
        <div id="toolbar">
          <pnft-domain-culture-filter .settings=${this.settings.filter}></pnft-domain-culture-filter>
          <pnft-page-size .mark=${this.settings.pageSizeMark}></pnft-page-size>
          <span id="total" data-mark=${this.settings.totalMark} aria-live="polite"
            >${this._error ? nothing : this.localize.term(this.settings.totalKey, this._total)}</span
          >
        </div>
      </umb-collection-toolbar>
    `;
  }

  protected override _renderEmptyState() {
    return html`<pnft-state-message .state=${this.settings.states[pickEmptyState(this._error, this._filtered)]}></pnft-state-message>`;
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
