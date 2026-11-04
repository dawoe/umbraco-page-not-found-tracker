import type { UmbControllerHost } from '@umbraco-cms/backoffice/controller-api';
import { tryExecute } from '@umbraco-cms/backoffice/resources';
import type { UmbPagedModel, UmbRepositoryResponse } from '@umbraco-cms/backoffice/repository';
import { getBlockRules, getBlockScopeOptions } from '../../../api/index.js';
import type { PnftFilterOptionsModel } from '../../../collection/types.js';
import { buildBlockRuleListQuery } from '../query.js';
import type { PnftBlockRuleCollectionModel, PnftBlockRuleFilter } from '../types.js';
import { mapBlockRule } from './block-rule.mapping.js';

/**
 * Reads block rules and the scope options from the package's Management API through the generated client (configured
 * with Umbraco's auth in entry.ts). Errors are returned, not notified: the collection shows them as its error and
 * unavailable states.
 */
export class PnftBlockRuleServerDataSource {
  #host: UmbControllerHost;

  constructor(host: UmbControllerHost) {
    this.#host = host;
  }

  /** Gets one page of block rules. */
  async getCollection(filter: Partial<PnftBlockRuleFilter>): Promise<UmbRepositoryResponse<UmbPagedModel<PnftBlockRuleCollectionModel>>> {
    const { data, error } = await tryExecute(this.#host, getBlockRules({ query: buildBlockRuleListQuery(filter) }), {
      disableNotifications: true,
    });

    if (error || !data) {
      return { error };
    }

    return { data: { items: data.items.map(mapBlockRule), total: data.total } };
  }

  /**
   * Gets the choices of the domain and culture filters: the domains and cultures configured in Umbraco, plus the
   * "all domains"/"all cultures" choice (rules with an empty domain or culture), which always exists for rules.
   */
  async getFilterOptions(): Promise<UmbRepositoryResponse<PnftFilterOptionsModel>> {
    const { data, error } = await tryExecute(this.#host, getBlockScopeOptions(), { disableNotifications: true });

    if (error || !data) {
      return { error };
    }

    return { data: { domains: [...data.domains], cultures: [...data.cultures], hasEmptyDomain: true, hasEmptyCulture: true } };
  }
}
