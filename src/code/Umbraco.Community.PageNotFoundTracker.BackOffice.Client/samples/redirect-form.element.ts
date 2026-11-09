import { customElement, html, property, state } from '@umbraco-cms/backoffice/external/lit';
import { UmbLitElement } from '@umbraco-cms/backoffice/lit-element';
import type { PnftTrackedUrlCollectionModel } from '../src/collection/types.js';
import type { PnftRedirectFormElement } from '../src/redirect/redirect-form.extension.js';

// docs:begin redirect-form-element
@customElement('my-redirect-form')
export class MyRedirectFormElement extends UmbLitElement implements PnftRedirectFormElement {
  // Set by the create redirect modal: the dashboard row the redirect is created for.
  @property({ attribute: false })
  trackedUrl?: PnftTrackedUrlCollectionModel;

  @state()
  private _targetUrl = '';

  // Called when the editor confirms. Return the payload the redirect provider reads, or undefined when the form is
  // not valid (nothing is sent). The form never calls the API itself.
  async getPayload(): Promise<unknown | undefined> {
    return this._targetUrl.startsWith('/') ? { targetUrl: this._targetUrl } : undefined;
  }

  override render() {
    return html`
      <uui-input
        label="Target URL"
        placeholder="/somewhere"
        .value=${this._targetUrl}
        @input=${(event: Event) => (this._targetUrl = (event.target as HTMLInputElement).value)}></uui-input>
    `;
  }
}

export default MyRedirectFormElement; // the element loader needs a default export
// docs:end redirect-form-element
