import { UmbModalToken } from '@umbraco-cms/backoffice/modal';
import { PNFT_CREATE_BLOCK_RULE_MODAL_ALIAS } from './constants.js';

/** The data of the rule modal: the starting values and the headline. An empty domain or culture means all. */
export interface PnftBlockRuleModalData {
  pattern: string;
  domain: string;
  culture: string;
  statusCode: number;
  /** The headline (a localization key prefixed with `#`, or text). */
  title: string;
}

/** The value the rule modal closes with once the rule is saved. */
export interface PnftBlockRuleModalValue {
  saved: true;
  /** Whether the tracked URLs the rule covers were removed; false when the rule is saved but the delete failed. */
  recordsRemoved: boolean;
  /** The server's message (set when the records were not removed). */
  message?: string;
}

/** The rule modal: pattern, scope and status code; it creates the rule on Confirm. */
export const PNFT_CREATE_BLOCK_RULE_MODAL = new UmbModalToken<PnftBlockRuleModalData, PnftBlockRuleModalValue>(
  PNFT_CREATE_BLOCK_RULE_MODAL_ALIAS,
  {
    modal: {
      type: 'sidebar',
      size: 'small',
    },
  },
);
