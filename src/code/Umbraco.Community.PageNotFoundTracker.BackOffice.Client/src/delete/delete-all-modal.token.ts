import { UmbModalToken } from '@umbraco-cms/backoffice/modal';
import { PNFT_DELETE_ALL_MODAL_ALIAS } from './constants.js';

/** The data of the delete all dialog: how many rows the list has and whether a search or filter narrows it. */
export interface PnftDeleteAllModalData {
  total: number;
  filtered: boolean;
}

/** The value the delete all dialog closes with when the editor confirms. */
export interface PnftDeleteAllModalValue {
  confirmed: boolean;
}

/** The delete all dialog: names the count and, without a filter, asks to type `delete` before Confirm is enabled. */
export const PNFT_DELETE_ALL_MODAL = new UmbModalToken<PnftDeleteAllModalData, PnftDeleteAllModalValue>(
  PNFT_DELETE_ALL_MODAL_ALIAS,
  { modal: { type: 'dialog' } },
);
