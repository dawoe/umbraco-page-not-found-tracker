import { UmbLocalizationController } from '@umbraco-cms/backoffice/localization-api';
import { umbOpenModal } from '@umbraco-cms/backoffice/modal';
import { UMB_NOTIFICATION_CONTEXT } from '@umbraco-cms/backoffice/notification';
import type { UmbControllerBase } from '@umbraco-cms/backoffice/class-api';
import { PNFT_CREATE_BLOCK_RULE_MODAL, type PnftBlockRuleModalData, type PnftBlockRuleModalValue } from './rule-modal.token.js';

/**
 * Opens the rule modal and, once the rule is saved, reloads the given collection and notifies: a success toast, or a
 * warning with the server's message when the rule is saved but the tracked URLs it covers could not be removed.
 * Cancelling does nothing. Shared by the "Block" row action and the "Add rule" collection action.
 *
 * The notification context and the localization are taken before the modal opens: the reload removes the blocked row,
 * and with it the host of the row action.
 */
export async function openRuleModalAndReload(
  host: UmbControllerBase,
  data: PnftBlockRuleModalData,
  reload: () => void,
): Promise<void> {
  const notifications = await host.getContext(UMB_NOTIFICATION_CONTEXT);
  const localize = new UmbLocalizationController(host);

  let value: PnftBlockRuleModalValue;

  try {
    value = await umbOpenModal(host, PNFT_CREATE_BLOCK_RULE_MODAL, { data });
  } catch {
    // Cancelled.
    return;
  }

  reload();

  if (value.recordsRemoved) {
    notifications?.peek('positive', {
      data: {
        headline: localize.term('pageNotFoundTracker_blockSavedHeadline'),
        message: localize.term('pageNotFoundTracker_blockSaved'),
      },
    });
  } else {
    notifications?.peek('warning', {
      data: {
        headline: localize.term('pageNotFoundTracker_blockSavedHeadline'),
        message: value.message ?? localize.term('pageNotFoundTracker_blockSavedRecordsKept'),
      },
    });
  }
}
