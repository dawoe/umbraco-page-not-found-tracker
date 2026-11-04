import type { UmbCollectionTextFilterApi, UmbCollectionTextFilterElement } from '@umbraco-cms/backoffice/collection';
import { css, customElement, html, state } from '@umbraco-cms/backoffice/external/lit';
import { UmbLitElement } from '@umbraco-cms/backoffice/lit-element';
import { PNFT_DATA_MARKS } from '../data-marks.js';

/**
 * The URL search box. It is the element of a `collectionTextFilter` of the framework's `default` kind, so the
 * kind's API does the work: it debounces the text and sets the collection's `filter`, which the repository
 * sends as `search`. Only the label, placeholder and `data-mark` are our own.
 */
@customElement('pnft-tracked-url-search')
export class PnftTrackedUrlSearchElement extends UmbLitElement implements UmbCollectionTextFilterElement {
  @state()
  private _text?: string;

  #api?: UmbCollectionTextFilterApi;

  /** The text filter API, set by the extension system. */
  get api(): UmbCollectionTextFilterApi | undefined {
    return this.#api;
  }

  set api(value: UmbCollectionTextFilterApi | undefined) {
    this.#api = value;
    this.observe(value?.text, (text) => (this._text = text), 'pnftSearchTextObserver');
  }

  #onInput(event: InputEvent) {
    this.#api?.setText(String((event.target as HTMLInputElement).value ?? ''));
  }

  override render() {
    return html`
      <uui-input
        type="search"
        data-mark=${PNFT_DATA_MARKS.search}
        label=${this.localize.term('pageNotFoundTracker_search')}
        placeholder=${this.localize.term('pageNotFoundTracker_searchPlaceholder')}
        .value=${this._text ?? ''}
        @input=${this.#onInput}>
        <uui-icon name="search" slot="prepend"></uui-icon>
      </uui-input>
    `;
  }

  static override styles = css`
    :host {
      display: flex;
      flex: 1 1 auto;
    }

    uui-input {
      width: 100%;
    }

    uui-icon {
      padding-left: var(--uui-size-space-3);
    }
  `;
}

export { PnftTrackedUrlSearchElement as element };

declare global {
  interface HTMLElementTagNameMap {
    'pnft-tracked-url-search': PnftTrackedUrlSearchElement;
  }
}
