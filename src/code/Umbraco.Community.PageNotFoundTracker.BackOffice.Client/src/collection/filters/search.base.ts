import type { UmbCollectionTextFilterApi, UmbCollectionTextFilterElement } from '@umbraco-cms/backoffice/collection';
import { css, html, state } from '@umbraco-cms/backoffice/external/lit';
import { UmbLitElement } from '@umbraco-cms/backoffice/lit-element';

/**
 * The base of the package's search boxes. Each is the element of a `collectionTextFilter` of the framework's
 * `default` kind, so the kind's API does the work: it debounces the text and sets the collection's `filter`, which the
 * repository sends as `search`. A subclass names its `data-mark`, label and placeholder.
 */
export abstract class PnftSearchElementBase extends UmbLitElement implements UmbCollectionTextFilterElement {
  /** The `data-mark` of the input. */
  protected abstract readonly mark: string;

  /** The localization key of the label. */
  protected abstract readonly labelKey: string;

  /** The localization key of the placeholder. */
  protected abstract readonly placeholderKey: string;

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
        data-mark=${this.mark}
        label=${this.localize.term(this.labelKey)}
        placeholder=${this.localize.term(this.placeholderKey)}
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
