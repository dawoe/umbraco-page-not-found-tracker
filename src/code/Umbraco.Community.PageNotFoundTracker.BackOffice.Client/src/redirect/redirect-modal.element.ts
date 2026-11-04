import { css, customElement, html, nothing, state } from '@umbraco-cms/backoffice/external/lit';
import type { UmbExtensionElementInitializer } from '@umbraco-cms/backoffice/extension-api';
import { UmbModalBaseElement } from '@umbraco-cms/backoffice/modal';
import { tryExecute } from '@umbraco-cms/backoffice/resources';
import { UmbTextStyles } from '@umbraco-cms/backoffice/style';
import { createTrackedUrlRedirect } from '../api/index.js';
import { PNFT_REDIRECT_FORM_EXTENSION_TYPE } from './constants.js';
import { PNFT_REDIRECT_DATA_MARKS } from './data-marks.js';
import type { PnftRedirectFormElement } from './redirect-form.extension.js';
import type { PnftCreateRedirectModalData, PnftCreateRedirectModalValue } from './redirect-modal.token.js';
import { createRedirect, type PnftRedirectResult } from './redirect.service.js';

/**
 * The create redirect modal. It hosts the registered redirect form with the highest weight (`umb-extension-slot` with
 * `single`: the framework sorts the permitted extensions by weight, descending, and keeps the first), asks it for the
 * payload on Confirm and sends it. A failure (the provider's message, or a generic text) shows inline and the modal
 * stays open; on success it closes with `{ removed, message }`.
 */
@customElement('pnft-create-redirect-modal')
export class PnftCreateRedirectModalElement extends UmbModalBaseElement<PnftCreateRedirectModalData, PnftCreateRedirectModalValue> {
  @state()
  private _submitting = false;

  @state()
  private _error?: string;

  @state()
  private _hasForm = false;

  #form?: PnftRedirectFormElement;

  #formProps?: { trackedUrl: PnftCreateRedirectModalData['trackedUrl'] };

  #renderForm = (extension: UmbExtensionElementInitializer) => {
    const form = extension.component as PnftRedirectFormElement | undefined;

    if (form !== this.#form) {
      this.#form = form;
      // Deferred: the slot calls this while it renders.
      queueMicrotask(() => (this._hasForm = !!form));
    }

    return form;
  };

  async #onConfirm() {
    const trackedUrl = this.data?.trackedUrl;

    if (!trackedUrl || !this.#form || this._submitting) {
      return;
    }

    this._error = undefined;
    this._submitting = true;

    try {
      const payload = await this.#form.getPayload();

      if (payload === undefined) {
        return;
      }

      const result = await createRedirect(
        (id, body) => tryExecute(this, createTrackedUrlRedirect({ path: { id }, body: { payload: body } }), { disableNotifications: true }),
        trackedUrl.id,
        payload,
      );

      if (result.kind === 'created') {
        this.value = { removed: result.recordRemoved, message: result.message };
        this._submitModal();
        return;
      }

      this._error = this.#messageOf(result);
    } finally {
      this._submitting = false;
    }
  }

  #messageOf(result: Exclude<PnftRedirectResult, { kind: 'created' }>): string {
    switch (result.kind) {
      case 'notSupported':
      case 'failed':
        return result.message ?? this.localize.term('pageNotFoundTracker_redirectFailed');
      case 'notFound':
        return this.localize.term('pageNotFoundTracker_redirectNotFound');
      case 'unavailable':
        return this.localize.term('pageNotFoundTracker_redirectUnavailable');
      default:
        return this.localize.term('pageNotFoundTracker_redirectError');
    }
  }

  override render() {
    const trackedUrl = this.data?.trackedUrl;

    if (trackedUrl && this.#formProps?.trackedUrl !== trackedUrl) {
      this.#formProps = { trackedUrl };
    }

    return html`
      <umb-body-layout data-mark=${PNFT_REDIRECT_DATA_MARKS.modal} headline=${this.localize.term('pageNotFoundTracker_createRedirectHeadline')}>
        ${trackedUrl
          ? html`<umb-extension-slot
              type=${PNFT_REDIRECT_FORM_EXTENSION_TYPE}
              single
              .props=${this.#formProps}
              .renderMethod=${this.#renderForm}></umb-extension-slot>`
          : nothing}
        ${this._error
          ? html`<div class="error" role="alert" data-mark=${PNFT_REDIRECT_DATA_MARKS.error}>
              <uui-icon name="icon-alert"></uui-icon><span>${this._error}</span>
            </div>`
          : nothing}
        <div slot="actions">
          <uui-button
            data-mark=${PNFT_REDIRECT_DATA_MARKS.cancel}
            label=${this.localize.term('general_cancel')}
            @click=${() => this._rejectModal()}></uui-button>
          <uui-button
            data-mark=${PNFT_REDIRECT_DATA_MARKS.confirm}
            look="primary"
            color="positive"
            label=${this.localize.term('pageNotFoundTracker_createRedirectConfirm')}
            .state=${this._submitting ? 'waiting' : undefined}
            ?disabled=${this._submitting || !this._hasForm}
            @click=${this.#onConfirm}></uui-button>
        </div>
      </umb-body-layout>
    `;
  }

  static override styles = [
    UmbTextStyles,
    css`
      .error {
        display: flex;
        align-items: flex-start;
        gap: var(--uui-size-space-3);
        margin-top: var(--uui-size-space-5);
        padding: var(--uui-size-space-4);
        border: 1px solid var(--uui-color-danger);
        border-radius: var(--uui-border-radius);
        color: var(--uui-color-danger-standalone);
        background: var(--uui-color-surface);
      }

      .error uui-icon {
        flex: none;
        margin-top: 2px;
      }
    `,
  ];
}

export default PnftCreateRedirectModalElement;
export { PnftCreateRedirectModalElement as element };

declare global {
  interface HTMLElementTagNameMap {
    'pnft-create-redirect-modal': PnftCreateRedirectModalElement;
  }
}
