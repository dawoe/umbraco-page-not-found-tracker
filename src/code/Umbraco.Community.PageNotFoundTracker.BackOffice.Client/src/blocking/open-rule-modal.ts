import { UmbLocalizationController } from '@umbraco-cms/backoffice/localization-api';
import { umbOpenModal } from '@umbraco-cms/backoffice/modal';
import { UMB_NOTIFICATION_CONTEXT } from '@umbraco-cms/backoffice/notification';
import type { UmbControllerBase } from '@umbraco-cms/backoffice/class-api';
import { PNFT_CREATE_BLOCK_RULE_MODAL, type PnftBlockRuleModalData, type PnftBlockRuleModalValue } from './rule-modal.token.js';

/**
 * Opens the rule modal and, once the rule is saved, reloads the given collection and notifies: a success toast, or a
 * warning with the server's message when the rule is saved but the tracked URLs it covers could not be removed.
 * Cancelling does nothing when creating; when editing (data has a `ruleId`) it reloads the list, because the modal cannot
 * tell a plain cancel from a cancel after the rule turned out to be gone. Shared by the "Block" row action, the "Add
 * rule" collection action and the "Edit" action.
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
    // Cancelled. An edit reloads: the rule may have been removed meanwhile (the modal then asks to close and reload).
    if (data.ruleId !== undefined) {
      reload();
    }

    return;
  }

  reload();

  if (value.recordsRemoved) {
    notifications?.peek('positive', {
      data: {
        headline: localize.term('pageNotFoundTracker_blockSavedHeadline'),
        message: localize.term(data.ruleId !== undefined ? 'pageNotFoundTracker_ruleUpdated' : 'pageNotFoundTracker_blockSaved'),
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
