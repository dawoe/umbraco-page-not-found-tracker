import { UmbEntityActionBase } from '@umbraco-cms/backoffice/entity-action';
import { html } from '@umbraco-cms/backoffice/external/lit';
import { UmbLocalizationController } from '@umbraco-cms/backoffice/localization-api';
import { umbConfirmModal } from '@umbraco-cms/backoffice/modal';
import { UMB_NOTIFICATION_CONTEXT } from '@umbraco-cms/backoffice/notification';
import { tryExecute } from '@umbraco-cms/backoffice/resources';
import { deleteBlockRule } from '../api/index.js';
import { deleteRule } from './block-rule.service.js';
import { PNFT_BLOCK_RULE_COLLECTION_CONTEXT } from './collection/block-rule-collection.context-token.js';

/**
 * The "Unblock" action on a block rule row: asks for confirmation (Umbraco's confirm modal), deletes the rule and
 * reloads the blocked list. URLs the rule matched are tracked again from then on. A rule that is already gone (404)
 * gives the package's own text, because Umbraco's error interceptor replaces 404 problem details.
 */
export class PnftUnblockEntityAction extends UmbEntityActionBase<never> {
  override async execute(): Promise<void> {
    const collection = await this.getContext(PNFT_BLOCK_RULE_COLLECTION_CONTEXT);
    const rule = collection?.getItems().find((item) => item.unique === this.args.unique);

    if (!collection || !rule) {
      return;
    }

    // Taken before the modal opens: the reload below removes the row, and with it this action's host.
    const notifications = await this.getContext(UMB_NOTIFICATION_CONTEXT);
    const localize = new UmbLocalizationController(this);

    try {
      await umbConfirmModal(this, {
        headline: localize.term('pageNotFoundTracker_unblockHeadline'),
        // A template, so the pattern is escaped (a string is rendered as HTML).
        content: html`<p>${localize.term('pageNotFoundTracker_unblockConfirmText', rule.pattern)}</p>`,
        color: 'danger',
        confirmLabel: localize.term('pageNotFoundTracker_unblock'),
      });
    } catch {
      // Cancelled.
      return;
    }

    const result = await deleteRule(
      (id) => tryExecute(this, deleteBlockRule({ path: { id } }), { disableNotifications: true }),
      rule.id,
    );

    collection.loadCollection();

    switch (result.kind) {
      case 'deleted':
        notifications?.peek('positive', {
          data: {
            headline: localize.term('pageNotFoundTracker_unblockedHeadline'),
            message: localize.term('pageNotFoundTracker_unblocked', rule.pattern),
          },
        });
        break;
      case 'notFound':
        notifications?.peek('warning', {
          data: { headline: localize.term('pageNotFoundTracker_unblockedHeadline'), message: localize.term('pageNotFoundTracker_unblockNotFound') },
        });
        break;
      default:
        notifications?.peek('danger', {
          data: {
            headline: localize.term('pageNotFoundTracker_unblockFailedHeadline'),
            message: localize.term(result.kind === 'unavailable' ? 'pageNotFoundTracker_blockUnavailable' : 'pageNotFoundTracker_unblockError'),
          },
        });
    }
  }
}

export { PnftUnblockEntityAction as api };
