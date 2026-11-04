/**
 * The `data-mark` hooks of the create redirect flow. The E2E tests keep their own copy of these values: change both
 * together. The row action button itself is marked by Umbraco: `entity-action:Pnft.EntityAction.TrackedUrl.CreateRedirect`.
 */
export const PNFT_REDIRECT_DATA_MARKS = {
  /** The create redirect modal (its body layout). */
  modal: 'pnft-create-redirect-modal',
  /** The modal's Confirm button. */
  confirm: 'pnft-redirect-confirm',
  /** The modal's Cancel button. */
  cancel: 'pnft-redirect-cancel',
  /** The inline error of the modal (provider message or a generic failure). */
  error: 'pnft-redirect-error',
  /** The default form's target page picker. */
  targetPicker: 'pnft-redirect-target-picker',
  /** The default form's "pick a page" validation message. */
  targetRequired: 'pnft-redirect-target-required',
  /** The default form's summary of the tracked URL. */
  summary: 'pnft-redirect-summary',
} as const;
