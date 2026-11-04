import { css, customElement, html, nothing, property, state } from '@umbraco-cms/backoffice/external/lit';
import type { UmbInputDocumentElement } from '@umbraco-cms/backoffice/document';
import { UmbLitElement } from '@umbraco-cms/backoffice/lit-element';
import { UmbTextStyles } from '@umbraco-cms/backoffice/style';
import type { PnftTrackedUrlCollectionModel } from '../../collection/types.js';
import { PNFT_REDIRECT_DATA_MARKS } from '../data-marks.js';
import type { PnftRedirectFormElement } from '../redirect-form.extension.js';

/** The payload of the default form, as the default (Umbraco redirect management) provider expects it. */
export interface PnftDefaultRedirectPayload {
  /** The key of the target document. */
  targetKey: string;
}

/**
 * The package's default redirect form: a summary of the tracked URL and what will happen, and a document picker for
 * the target page (required). Its payload is `{ targetKey }`.
 */
@customElement('pnft-default-redirect-form')
export class PnftDefaultRedirectFormElement extends UmbLitElement implements PnftRedirectFormElement {
  @property({ attribute: false })
  trackedUrl?: PnftTrackedUrlCollectionModel;

  @state()
  private _targetKey?: string;

  @state()
  private _showRequired = false;

  /** Returns `{ targetKey }`, or undefined (and shows the required message) when no page is picked. */
  async getPayload(): Promise<PnftDefaultRedirectPayload | undefined> {
    if (!this._targetKey) {
      this._showRequired = true;
      return undefined;
    }

    return { targetKey: this._targetKey };
  }

  #onChange(event: Event) {
    const picker = event.target as UmbInputDocumentElement;
    this._targetKey = picker.selection[0];

    if (this._targetKey) {
      this._showRequired = false;
    }
  }

  override render() {
    const trackedUrl = this.trackedUrl;

    if (!trackedUrl) {
      return nothing;
    }

    return html`
      <uui-box headline=${this.localize.term('pageNotFoundTracker_redirectSummaryHeadline')}>
        <dl data-mark=${PNFT_REDIRECT_DATA_MARKS.summary}>
          <dt>${this.localize.term('pageNotFoundTracker_columnUrl')}</dt>
          <dd class="url">${trackedUrl.url}</dd>
          <dt>${this.localize.term('pageNotFoundTracker_columnDomain')}</dt>
          <dd>${trackedUrl.domain || html`<em>${this.localize.term('pageNotFoundTracker_noDomain')}</em>`}</dd>
          <dt>${this.localize.term('pageNotFoundTracker_columnCulture')}</dt>
          <dd>${trackedUrl.culture || html`<em>${this.localize.term('pageNotFoundTracker_noCulture')}</em>`}</dd>
        </dl>
        <p>${this.localize.term('pageNotFoundTracker_redirectExplanation')}</p>
      </uui-box>
      <uui-box headline=${this.localize.term('pageNotFoundTracker_redirectTarget')}>
        <p class="description">${this.localize.term('pageNotFoundTracker_redirectTargetDescription')}</p>
        <umb-input-document
          data-mark=${PNFT_REDIRECT_DATA_MARKS.targetPicker}
          .max=${1}
          @change=${this.#onChange}></umb-input-document>
        ${this._showRequired
          ? html`<p class="required" role="alert" data-mark=${PNFT_REDIRECT_DATA_MARKS.targetRequired}>
              ${this.localize.term('pageNotFoundTracker_redirectTargetRequired')}
            </p>`
          : nothing}
      </uui-box>
    `;
  }

  static override styles = [
    UmbTextStyles,
    css`
      :host {
        display: flex;
        flex-direction: column;
        gap: var(--uui-size-space-5);
      }

      dl {
        display: grid;
        grid-template-columns: max-content 1fr;
        gap: var(--uui-size-space-2) var(--uui-size-space-4);
        margin: 0 0 var(--uui-size-space-4);
      }

      dt {
        font-weight: 700;
      }

      dd {
        margin: 0;
      }

      dd.url {
        word-break: break-all;
      }

      em {
        color: var(--uui-color-text-alt);
      }

      p {
        margin: 0;
      }

      .description {
        margin-bottom: var(--uui-size-space-3);
        color: var(--uui-color-text-alt);
      }

      .required {
        margin-top: var(--uui-size-space-3);
        color: var(--uui-color-danger-standalone);
      }
    `,
  ];
}

export default PnftDefaultRedirectFormElement;
export { PnftDefaultRedirectFormElement as element };

declare global {
  interface HTMLElementTagNameMap {
    'pnft-default-redirect-form': PnftDefaultRedirectFormElement;
  }
}
