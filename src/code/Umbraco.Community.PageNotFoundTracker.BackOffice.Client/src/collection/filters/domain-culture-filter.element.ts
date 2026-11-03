import { css, customElement, html, state } from '@umbraco-cms/backoffice/external/lit';
import { UmbLitElement } from '@umbraco-cms/backoffice/lit-element';
import type { UUISelectEvent } from '@umbraco-cms/backoffice/external/uui';
import { PNFT_TRACKED_URL_COLLECTION_CONTEXT } from '../collection.context-token.js';
import type { PnftTrackedUrlCollectionContext } from '../collection.context.js';
import { PNFT_DATA_MARKS } from '../data-marks.js';
import type { PnftFilterValue, PnftTrackedUrlCollectionFilterModel, PnftTrackedUrlFilterOptionsModel } from '../types.js';
import { buildFilterChoices, fromChoiceValue, toChoiceValue } from './filter-choices.js';

/**
 * The domain and culture filters: a select each with "All", "No domain"/"No culture" (when the data has URLs
 * without one) and every value the API returns. The choices are requested once, when the collection renders.
 */
@customElement('pnft-domain-culture-filter')
export class PnftDomainCultureFilterElement extends UmbLitElement {
  @state()
  private _options?: PnftTrackedUrlFilterOptionsModel;

  @state()
  private _domain = toChoiceValue(undefined);

  @state()
  private _culture = toChoiceValue(undefined);

  #context?: PnftTrackedUrlCollectionContext;

  constructor() {
    super();
    this.consumeContext(PNFT_TRACKED_URL_COLLECTION_CONTEXT, (context) => {
      this.#context = context;
      this.observe(
        context?.filter,
        (filter) => {
          this._domain = toChoiceValue((filter as PnftTrackedUrlCollectionFilterModel | undefined)?.domain);
          this._culture = toChoiceValue((filter as PnftTrackedUrlCollectionFilterModel | undefined)?.culture);
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
    const domains = buildFilterChoices(this._options?.domains ?? [], this._options?.hasEmptyDomain ?? false, {
      all: this.localize.term('pageNotFoundTracker_allDomains'),
      empty: this.localize.term('pageNotFoundTracker_noDomain'),
    });
    const cultures = buildFilterChoices(this._options?.cultures ?? [], this._options?.hasEmptyCulture ?? false, {
      all: this.localize.term('pageNotFoundTracker_allCultures'),
      empty: this.localize.term('pageNotFoundTracker_noCulture'),
    });

    return html`
      <uui-select
        data-mark=${PNFT_DATA_MARKS.filterDomain}
        label=${this.localize.term('pageNotFoundTracker_filterDomain')}
        .options=${domains.map((choice) => ({ name: choice.label, value: choice.value, selected: choice.value === this._domain }))}
        @change=${(event: UUISelectEvent) => this.#onChange('domain', event)}></uui-select>
      <uui-select
        data-mark=${PNFT_DATA_MARKS.filterCulture}
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
