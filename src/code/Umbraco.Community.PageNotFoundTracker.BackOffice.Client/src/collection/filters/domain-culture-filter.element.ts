import { css, customElement, html, property, state } from '@umbraco-cms/backoffice/external/lit';
import { UmbLitElement } from '@umbraco-cms/backoffice/lit-element';
import type { UUISelectEvent } from '@umbraco-cms/backoffice/external/uui';
import { PNFT_PAGED_COLLECTION_CONTEXT, type PnftAnyPagedCollectionContext } from '../collection.context-token.js';
import { PNFT_DATA_MARKS } from '../data-marks.js';
import type { PnftFilterOptionsModel, PnftFilterValue, PnftPagedCollectionFilterModel } from '../types.js';
import { buildFilterChoices, fromChoiceValue, toChoiceValue } from './filter-choices.js';

/** The `data-mark`s and texts of the domain and culture filters. */
export interface PnftDomainCultureFilterSettings {
  domainMark: string;
  cultureMark: string;
  /** Localization keys of the "all" (no filter) and "empty" choices. */
  allDomainsKey: string;
  emptyDomainKey: string;
  allCulturesKey: string;
  emptyCultureKey: string;
}

/** The tracked URL collection's filter settings (the default). */
export const PNFT_TRACKED_URL_FILTER_SETTINGS: PnftDomainCultureFilterSettings = {
  domainMark: PNFT_DATA_MARKS.filterDomain,
  cultureMark: PNFT_DATA_MARKS.filterCulture,
  allDomainsKey: 'pageNotFoundTracker_allDomains',
  emptyDomainKey: 'pageNotFoundTracker_noDomain',
  allCulturesKey: 'pageNotFoundTracker_allCultures',
  emptyCultureKey: 'pageNotFoundTracker_noCulture',
};

/**
 * The domain and culture filters of the nearest package collection: a select each with "All", the "empty" choice
 * (when the data has rows without one) and every value the collection's repository returns. The choices are requested
 * once, when the element connects to the collection.
 */
@customElement('pnft-domain-culture-filter')
export class PnftDomainCultureFilterElement extends UmbLitElement {
  @property({ attribute: false })
  settings: PnftDomainCultureFilterSettings = PNFT_TRACKED_URL_FILTER_SETTINGS;

  @state()
  private _options?: PnftFilterOptionsModel;

  @state()
  private _domain = toChoiceValue(undefined);

  @state()
  private _culture = toChoiceValue(undefined);

  #context?: PnftAnyPagedCollectionContext;

  constructor() {
    super();
    this.consumeContext(PNFT_PAGED_COLLECTION_CONTEXT, (context) => {
      this.#context = context;
      this.observe(
        context?.filter,
        (filter) => {
          this._domain = toChoiceValue((filter as PnftPagedCollectionFilterModel | undefined)?.domain);
          this._culture = toChoiceValue((filter as PnftPagedCollectionFilterModel | undefined)?.culture);
        },
        'pnftFilterObserver',
      );
      this.#loadOptions();
    });
  }

  async #loadOptions() {
    this._options = await this.#context?.requestFilterOptions();
  }

  #onChange(key: 'domain' | 'culture', event: UUISelectEvent) {
    const value: PnftFilterValue | undefined = fromChoiceValue(String(event.target.value));
    this.#context?.setFilter({ [key]: value });
  }

  override render() {
    const settings = this.settings;
    const domains = buildFilterChoices(this._options?.domains ?? [], this._options?.hasEmptyDomain ?? false, {
      all: this.localize.term(settings.allDomainsKey),
      empty: this.localize.term(settings.emptyDomainKey),
    });
    const cultures = buildFilterChoices(this._options?.cultures ?? [], this._options?.hasEmptyCulture ?? false, {
      all: this.localize.term(settings.allCulturesKey),
      empty: this.localize.term(settings.emptyCultureKey),
    });

    return html`
      <uui-select
        data-mark=${settings.domainMark}
        label=${this.localize.term('pageNotFoundTracker_filterDomain')}
        .options=${domains.map((choice) => ({ name: choice.label, value: choice.value, selected: choice.value === this._domain }))}
        @change=${(event: UUISelectEvent) => this.#onChange('domain', event)}></uui-select>
      <uui-select
        data-mark=${settings.cultureMark}
        label=${this.localize.term('pageNotFoundTracker_filterCulture')}
        .options=${cultures.map((choice) => ({ name: choice.label, value: choice.value, selected: choice.value === this._culture }))}
        @change=${(event: UUISelectEvent) => this.#onChange('culture', event)}></uui-select>
    `;
  }

  static override styles = css`
    :host {
      display: flex;
      gap: var(--uui-size-space-3);
    }
  `;
}

declare global {
  interface HTMLElementTagNameMap {
    'pnft-domain-culture-filter': PnftDomainCultureFilterElement;
  }
}
