import { css, customElement, html, nothing, state } from '@umbraco-cms/backoffice/external/lit';
import type { UUIInputEvent } from '@umbraco-cms/backoffice/external/uui';
import { UmbModalBaseElement } from '@umbraco-cms/backoffice/modal';
import { UmbTextStyles } from '@umbraco-cms/backoffice/style';
import { PNFT_DELETE_DATA_MARKS } from './data-marks.js';
import { isDeleteAllConfirmed } from './delete-all.js';
import type { PnftDeleteAllModalData, PnftDeleteAllModalValue } from './delete-all-modal.token.js';

/**
 * The "Delete all matching" dialog. It names the number of URLs; with a search or filter active it says the delete applies
 * to the current filter, without one it says every tracked URL goes and keeps Confirm disabled until `delete` is typed.
 */
@customElement('pnft-delete-all-modal')
export class PnftDeleteAllModalElement extends UmbModalBaseElement<PnftDeleteAllModalData, PnftDeleteAllModalValue> {
  @state()
  private _typed = '';

  /** The one place that decides whether Confirm is allowed: a filtered delete always, an unfiltered one only once `delete` is typed. */
  #canConfirm(): boolean {
    return (this.data?.filtered ?? false) || isDeleteAllConfirmed(this._typed);
  }

  #confirm() {
    if (!this.#canConfirm()) {
      return;
    }

    this.value = { confirmed: true };
    this._submitModal();
  }

  override render() {
    const total = this.data?.total ?? 0;
    const filtered = this.data?.filtered ?? false;
    const canConfirm = this.#canConfirm();

    return html`
      <uui-dialog-layout
        data-mark=${PNFT_DELETE_DATA_MARKS.deleteAllModal}
        headline=${this.localize.term('pageNotFoundTracker_deleteAllHeadline')}>
        <p data-mark=${PNFT_DELETE_DATA_MARKS.deleteAllCount}>${this.localize.term('pageNotFoundTracker_deleteAllCountText', total)}</p>
        <p>${this.localize.term(filtered ? 'pageNotFoundTracker_deleteAllFilteredText' : 'pageNotFoundTracker_deleteAllEverythingText')}</p>
        ${filtered
          ? nothing
          : html`<uui-label for="confirm-input">${this.localize.term('pageNotFoundTracker_deleteAllTypeToConfirm')}</uui-label>
              <uui-input
                id="confirm-input"
                data-mark=${PNFT_DELETE_DATA_MARKS.deleteAllConfirmInput}
                label=${this.localize.term('pageNotFoundTracker_deleteAllTypeToConfirm')}
                autocomplete="off"
                .value=${this._typed}
                @input=${(event: UUIInputEvent) => (this._typed = String(event.target.value ?? ''))}></uui-input>`}
        <uui-button
          slot="actions"
          data-mark=${PNFT_DELETE_DATA_MARKS.deleteAllCancel}
          label=${this.localize.term('general_cancel')}
          @click=${() => this._rejectModal()}></uui-button>
        <uui-button
          slot="actions"
          data-mark=${PNFT_DELETE_DATA_MARKS.deleteAllConfirm}
          look="primary"
          color="danger"
          label=${this.localize.term('pageNotFoundTracker_deleteAllConfirmLabel')}
          ?disabled=${!canConfirm}
          @click=${this.#confirm}></uui-button>
      </uui-dialog-layout>
    `;
  }

  static override styles = [
    UmbTextStyles,
    css`
      uui-input {
        width: 100%;
      }

      p {
        margin: 0 0 var(--uui-size-space-4);
      }
    `,
  ];
}

export default PnftDeleteAllModalElement;
export { PnftDeleteAllModalElement as element };

declare global {
  interface HTMLElementTagNameMap {
    'pnft-delete-all-modal': PnftDeleteAllModalElement;
  }
}
