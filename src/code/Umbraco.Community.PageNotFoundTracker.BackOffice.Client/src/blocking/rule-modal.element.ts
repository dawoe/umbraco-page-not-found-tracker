import { css, customElement, html, nothing, state } from '@umbraco-cms/backoffice/external/lit';
import type { UUIInputEvent, UUISelectEvent } from '@umbraco-cms/backoffice/external/uui';
import { UmbModalBaseElement } from '@umbraco-cms/backoffice/modal';
import { tryExecute } from '@umbraco-cms/backoffice/resources';
import { UmbTextStyles } from '@umbraco-cms/backoffice/style';
import { createBlockRule, getBlockScopeOptions, updateBlockRule } from '../api/index.js';
import { fromChoiceValue, toChoiceValue } from '../collection/filters/filter-choices.js';
import { createRule, updateRule, type PnftBlockRuleResult } from './block-rule.service.js';
import { PNFT_BLOCK_DATA_MARKS } from './data-marks.js';
import type { PnftBlockRuleModalData, PnftBlockRuleModalValue } from './rule-modal.token.js';
import {
  PNFT_BLOCK_DEFAULT_STATUS_CODE,
  PNFT_BLOCK_STATUS_CODES,
  buildScopeChoices,
  buildStatusChoices,
  isPatternGiven,
  toCreateRuleRequest,
  type PnftRuleFormValues,
} from './rule-form.js';

/**
 * The rule modal ("Block" on a tracked URL row and "Add rule" on the blocked list). Fields: the pattern (required, `*`
 * is a wildcard), the domain and culture (all, or one of the values configured in Umbraco plus the preselected one) and
 * the status code (403, 404 or 410). A static warning says a rule applies to every matching request, not only missing
 * pages. Confirm creates the rule, or updates it when the data has a `ruleId` (edit mode: the Confirm label is Save and a
 * 404 or 409 shows its message inline); a 400 shows the server's message inline and the modal stays open; on success it
 * closes with `{ saved, recordsRemoved, message }`.
 */
@customElement('pnft-block-rule-modal')
export class PnftBlockRuleModalElement extends UmbModalBaseElement<PnftBlockRuleModalData, PnftBlockRuleModalValue> {
  @state()
  private _values: PnftRuleFormValues = { pattern: '', domain: '', culture: '', statusCode: PNFT_BLOCK_DEFAULT_STATUS_CODE };

  @state()
  private _domains: Array<string> = [];

  @state()
  private _cultures: Array<string> = [];

  @state()
  private _submitting = false;

  @state()
  private _showPatternRequired = false;

  @state()
  private _error?: string;

  override connectedCallback(): void {
    super.connectedCallback();

    const data = this.data;

    if (data) {
      this._values = { pattern: data.pattern, domain: data.domain, culture: data.culture, statusCode: data.statusCode };
    }

    this.#loadScopeOptions();
  }

  async #loadScopeOptions() {
    // A failure leaves "all" plus the preselected values: the rule can still be created.
    const { data } = await tryExecute(this, getBlockScopeOptions(), { disableNotifications: true });

    if (data) {
      this._domains = [...data.domains];
      this._cultures = [...data.cultures];
    }
  }

  #set(change: Partial<PnftRuleFormValues>) {
    this._values = { ...this._values, ...change };
  }

  #onPatternInput(event: UUIInputEvent) {
    this.#set({ pattern: String(event.target.value ?? '') });

    if (isPatternGiven(this._values.pattern)) {
      this._showPatternRequired = false;
    }
  }

  async #onConfirm() {
    if (this._submitting) {
      return;
    }

    this._error = undefined;

    if (!isPatternGiven(this._values.pattern)) {
      this._showPatternRequired = true;
      return;
    }

    this._submitting = true;

    try {
      const ruleId = this.data?.ruleId;
      const request = toCreateRuleRequest(this._values);
      const result =
        ruleId === undefined
          ? await createRule((body) => tryExecute(this, createBlockRule({ body }), { disableNotifications: true }), request)
          : await updateRule(
              (id, body) => tryExecute(this, updateBlockRule({ path: { id }, body }), { disableNotifications: true }),
              ruleId,
              request,
            );

      if (result.kind === 'saved') {
        this.value = { saved: true, recordsRemoved: result.recordsRemoved, message: result.message };
        this._submitModal();
        return;
      }

      this._error = this.#messageOf(result);
    } finally {
      this._submitting = false;
    }
  }

  #messageOf(result: PnftBlockRuleResult): string {
    switch (result.kind) {
      case 'invalid':
        return result.message ?? this.localize.term('pageNotFoundTracker_blockInvalid');
      case 'conflict':
        return result.message ?? this.localize.term('pageNotFoundTracker_blockRuleExists');
      case 'notFound':
        return this.localize.term('pageNotFoundTracker_editRuleNotFound');
      case 'unavailable':
        return this.localize.term('pageNotFoundTracker_blockUnavailable');
      default:
        return this.localize.term('pageNotFoundTracker_blockError');
    }
  }

  #scopeOptions(values: ReadonlyArray<string>, current: string, allKey: string) {
    const selected = toChoiceValue(current);

    return buildScopeChoices(values, current).map((value) => {
      const choice = toChoiceValue(value);
      return { name: value === '' ? this.localize.term(allKey) : value, value: choice, selected: choice === selected };
    });
  }

  override render() {
    const values = this._values;

    return html`
      <umb-body-layout
        data-mark=${PNFT_BLOCK_DATA_MARKS.modal}
        headline=${this.localize.string(this.data?.title ?? '#pageNotFoundTracker_blockHeadline')}>
        <uui-box>
          <div class="field">
            <uui-label for="pattern" required>${this.localize.term('pageNotFoundTracker_blockPattern')}</uui-label>
            <p class="description" id="pattern-hint">${this.localize.term('pageNotFoundTracker_blockPatternHint')}</p>
            <uui-input
              id="pattern"
              data-mark=${PNFT_BLOCK_DATA_MARKS.pattern}
              label=${this.localize.term('pageNotFoundTracker_blockPattern')}
              placeholder="/wp-admin/*"
              aria-describedby="pattern-hint"
              required
              .value=${values.pattern}
              @input=${this.#onPatternInput}></uui-input>
            ${this._showPatternRequired
              ? html`<p class="required" role="alert" data-mark=${PNFT_BLOCK_DATA_MARKS.patternRequired}>
                  ${this.localize.term('pageNotFoundTracker_blockPatternRequired')}
                </p>`
              : nothing}
          </div>
          <div class="field">
            <uui-label for="domain">${this.localize.term('pageNotFoundTracker_columnDomain')}</uui-label>
            <uui-select
              id="domain"
              data-mark=${PNFT_BLOCK_DATA_MARKS.domain}
              label=${this.localize.term('pageNotFoundTracker_columnDomain')}
              .options=${this.#scopeOptions(this._domains, values.domain, 'pageNotFoundTracker_allDomains')}
              @change=${(event: UUISelectEvent) => this.#set({ domain: fromChoiceValue(String(event.target.value)) ?? '' })}></uui-select>
          </div>
          <div class="field">
            <uui-label for="culture">${this.localize.term('pageNotFoundTracker_columnCulture')}</uui-label>
            <uui-select
              id="culture"
              data-mark=${PNFT_BLOCK_DATA_MARKS.culture}
              label=${this.localize.term('pageNotFoundTracker_columnCulture')}
              .options=${this.#scopeOptions(this._cultures, values.culture, 'pageNotFoundTracker_allCultures')}
              @change=${(event: UUISelectEvent) => this.#set({ culture: fromChoiceValue(String(event.target.value)) ?? '' })}></uui-select>
          </div>
          <div class="field">
            <uui-label for="status">${this.localize.term('pageNotFoundTracker_columnStatusCode')}</uui-label>
            <uui-select
              id="status"
              data-mark=${PNFT_BLOCK_DATA_MARKS.statusCode}
              label=${this.localize.term('pageNotFoundTracker_columnStatusCode')}
              .options=${buildStatusChoices(this.data?.statusCode ?? values.statusCode).map((code) => ({
                name: PNFT_BLOCK_STATUS_CODES.includes(code) ? this.localize.term(`pageNotFoundTracker_status${code}`) : String(code),
                value: String(code),
                selected: code === values.statusCode,
              }))}
              @change=${(event: UUISelectEvent) => this.#set({ statusCode: Number(event.target.value) })}></uui-select>
          </div>
        </uui-box>
        <div class="warning" role="note" data-mark=${PNFT_BLOCK_DATA_MARKS.warning}>
          <uui-icon name="icon-alert"></uui-icon><span>${this.localize.term('pageNotFoundTracker_blockWarning')}</span>
        </div>
        ${this._error
          ? html`<div class="error" role="alert" data-mark=${PNFT_BLOCK_DATA_MARKS.error}>
              <uui-icon name="icon-alert"></uui-icon><span>${this._error}</span>
            </div>`
          : nothing}
        <div slot="actions">
          <uui-button
            data-mark=${PNFT_BLOCK_DATA_MARKS.cancel}
            label=${this.localize.term('general_cancel')}
            ?disabled=${this._submitting}
            @click=${() => this._rejectModal()}></uui-button>
          <uui-button
            data-mark=${PNFT_BLOCK_DATA_MARKS.confirm}
            look="primary"
            color=${this.data?.ruleId === undefined ? 'danger' : 'positive'}
            label=${this.localize.term(this.data?.ruleId === undefined ? 'pageNotFoundTracker_blockConfirm' : 'pageNotFoundTracker_saveRule')}
            .state=${this._submitting ? 'waiting' : undefined}
            ?disabled=${this._submitting}
            @click=${this.#onConfirm}></uui-button>
        </div>
      </umb-body-layout>
    `;
  }

  static override styles = [
    UmbTextStyles,
    css`
      .field {
        display: flex;
        flex-direction: column;
        gap: var(--uui-size-space-2);
      }

      .field + .field {
        margin-top: var(--uui-size-space-5);
      }

      uui-input,
      uui-select {
        width: 100%;
      }

      p {
        margin: 0;
      }

      .description {
        color: var(--uui-color-text-alt);
      }

      .required {
        color: var(--uui-color-danger-standalone);
      }

      .warning,
      .error {
        display: flex;
        align-items: flex-start;
        gap: var(--uui-size-space-3);
        margin-top: var(--uui-size-space-5);
        padding: var(--uui-size-space-4);
        border-radius: var(--uui-border-radius);
        background: var(--uui-color-surface);
      }

      .warning {
        border: 1px solid var(--uui-color-warning);
        color: var(--uui-color-text);
      }

      .error {
        border: 1px solid var(--uui-color-danger);
        color: var(--uui-color-danger-standalone);
      }

      .warning uui-icon,
      .error uui-icon {
        flex: none;
        margin-top: 2px;
      }
    `,
  ];
}

export default PnftBlockRuleModalElement;
export { PnftBlockRuleModalElement as element };

declare global {
  interface HTMLElementTagNameMap {
    'pnft-block-rule-modal': PnftBlockRuleModalElement;
  }
}
