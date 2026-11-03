import { css, html } from '@umbraco-cms/backoffice/external/lit';
import { UmbLitElement } from '@umbraco-cms/backoffice/lit-element';
import { UmbTextStyles } from '@umbraco-cms/backoffice/style';

/**
 * Base of the collection's message states (empty, empty after filtering, error, unavailable): a headline and
 * an explanation, both localized, and the state's `data-mark` on the wrapper.
 */
export abstract class PnftStateMessageElementBase extends UmbLitElement {
  /** The `data-mark` of the state. */
  protected abstract readonly mark: string;

  /** The localization key of the headline. */
  protected abstract readonly headlineKey: string;

  /** The localization key of the explanation. */
  protected abstract readonly textKey: string;

  /** The icon shown above the headline. */
  protected readonly icon: string = 'icon-search';

  override render() {
    return html`
      <div id="state" data-mark=${this.mark} role="status">
        <umb-icon name=${this.icon}></umb-icon>
        <h4>${this.localize.term(this.headlineKey)}</h4>
        <p>${this.localize.term(this.textKey)}</p>
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
