import { css, html, customElement } from "@umbraco-cms/backoffice/external/lit";
import { UmbLitElement } from "@umbraco-cms/backoffice/lit-element";

@customElement("umbraco-community-pagenotfoundtracker-placeholder-dashboard")
export class PageNotFoundTrackerPlaceholderDashboardElement extends UmbLitElement {
  override render() {
    return html`
      <uui-box headline="Page Not Found Tracker">
        <div data-mark="page-not-found-tracker-placeholder">
          Page Not Found Tracker
        </div>
      </uui-box>
    `;
  }

  static override styles = css`
    :host {
      display: block;
      padding: var(--uui-size-layout-1);
    }
  `;
}

export default PageNotFoundTrackerPlaceholderDashboardElement;

declare global {
  interface HTMLElementTagNameMap {
    "umbraco-community-pagenotfoundtracker-placeholder-dashboard": PageNotFoundTrackerPlaceholderDashboardElement;
  }
}
