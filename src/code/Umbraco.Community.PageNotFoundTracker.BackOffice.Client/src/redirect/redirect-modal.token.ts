import { UmbModalToken } from '@umbraco-cms/backoffice/modal';
import type { PnftTrackedUrlCollectionModel } from '../collection/types.js';
import { PNFT_CREATE_REDIRECT_MODAL_ALIAS } from './constants.js';

/** The data of the create redirect modal: the row the redirect is created for. */
export interface PnftCreateRedirectModalData {
  trackedUrl: PnftTrackedUrlCollectionModel;
}

/** The value the create redirect modal closes with once the redirect exists. */
export interface PnftCreateRedirectModalValue {
  /** Whether the tracked record was removed; false when the redirect exists but deleting the record failed. */
  removed: boolean;
  /** The server's message (set when the record was not removed). */
  message?: string;
}

/** The create redirect modal: hosts the highest-weight redirect form and sends its payload on Confirm. */
export const PNFT_CREATE_REDIRECT_MODAL = new UmbModalToken<PnftCreateRedirectModalData, PnftCreateRedirectModalValue>(
  PNFT_CREATE_REDIRECT_MODAL_ALIAS,
  {
    modal: {
      type: 'sidebar',
      size: 'small',
    },
  },
);
