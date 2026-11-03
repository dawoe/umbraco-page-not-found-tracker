import type { UmbControllerHost } from '@umbraco-cms/backoffice/controller-api';
import type { UmbConditionConfigBase, UmbConditionControllerArguments, UmbExtensionCondition } from '@umbraco-cms/backoffice/extension-api';
import { UmbConditionBase } from '@umbraco-cms/backoffice/extension-registry';
import { getDashboardAccess } from '../api/index.js';
import { whenClientConfigured } from '../api/client-ready.js';
import { createDashboardAccessCheck } from './dashboard-access.js';
import type { PNFT_DASHBOARD_ACCESS_CONDITION_ALIAS } from './constants.js';

/** The configuration of the dashboard access condition (no options). */
export type PnftDashboardAccessConditionConfig = UmbConditionConfigBase<typeof PNFT_DASHBOARD_ACCESS_CONDITION_ALIAS>;

/**
 * Asks the server whether the current user may see the dashboard (`GET access`): the first condition instance
 * starts the request, concurrent instances share it, and a successful answer is kept for the page load. Nothing is
 * requested at boot, only when a manifest with this condition is evaluated. A failed request (error status,
 * network failure, missing body) means not permitted (fail closed) and is not kept, so the next evaluation of the
 * condition asks again. This only hides the dashboard: the list endpoints enforce the same group rule with 403.
 */
const checkAccess = createDashboardAccessCheck(async () => {
  await whenClientConfigured;

  return getDashboardAccess({ throwOnError: false });
});

/** The dashboard access condition: permitted when the server says the current user is allowed. */
export class PnftDashboardAccessCondition
  extends UmbConditionBase<PnftDashboardAccessConditionConfig>
  implements UmbExtensionCondition
{
  constructor(host: UmbControllerHost, args: UmbConditionControllerArguments<PnftDashboardAccessConditionConfig>) {
    super(host, args);

    checkAccess().then((allowed) => {
      this.permitted = allowed;
    });
  }
}

export { PnftDashboardAccessCondition as api };
