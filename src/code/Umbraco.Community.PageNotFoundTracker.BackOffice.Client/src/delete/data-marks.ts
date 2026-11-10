/**
 * The `data-mark` hooks of the delete flow. The E2E tests keep their own copy of these values: change both together.
 * Umbraco marks the row action itself: `entity-action:Pnft.EntityAction.TrackedUrl.Delete`, and the collection action
 * `collection-action:Pnft.CollectionAction.TrackedUrl.DeleteAll`. The "Delete selected" bulk action has no mark in 17.8:
 * Umbraco renders it as `umb-entity-bulk-action` (a `uui-button` labelled with the text) inside
 * `umb-collection-selection-actions`, the collection footer.
 */
export const PNFT_DELETE_DATA_MARKS = {
  deleteAllModal: 'pnft-delete-all-modal',
  deleteAllConfirm: 'pnft-delete-all-confirm',
  deleteAllCancel: 'pnft-delete-all-cancel',
  deleteAllConfirmInput: 'pnft-delete-all-confirm-input',
  deleteAllCount: 'pnft-delete-all-count',
} as const;
