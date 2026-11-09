import { css, customElement, html, property } from '@umbraco-cms/backoffice/external/lit';
import { UmbLitElement } from '@umbraco-cms/backoffice/lit-element';
import { UmbTextStyles } from '@umbraco-cms/backoffice/style';

/** What a message state shows: its `data-mark`, the localization keys of headline and text, and an icon. */
export interface PnftStateMessage {
  mark: string;
  headlineKey: string;
  textKey: string;
  icon: string;
}

/**
 * A message state of a package collection (empty, empty after filtering, error, unavailable): a headline and an
 * explanation, both localized, and the state's `data-mark` on the wrapper. The collection element passes the state.
 */
@customElement('pnft-state-message')
export class PnftStateMessageElement extends UmbLitElement {
  @property({ attribute: false })
  state?: PnftStateMessage;

  override render() {
    const state = this.state;

    if (!state) {
      return html``;
    }

    return html`
      <div id="state" data-mark=${state.mark} role="status">
        <umb-icon name=${state.icon}></umb-icon>
        <h4>${this.localize.term(state.headlineKey)}</h4>
        <p>${this.localize.term(state.textKey)}</p>
      </div>
    `;
  }

  static override styles = [
    UmbTextStyles,
    css`
      :host {
        display: block;
      }

      #state {
        max-width: 60ch;
        margin: var(--uui-size-layout-2) auto;
        text-align: center;
      }

      umb-icon {
        font-size: var(--uui-size-12);
        color: var(--uui-color-text-alt);
      }
    `,
  ];
}

declare global {
  interface HTMLElementTagNameMap {
    'pnft-state-message': PnftStateMessageElement;
  }
}
