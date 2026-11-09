import type { UmbControllerHost } from '@umbraco-cms/backoffice/controller-api';
import type { UmbCollectionRepository } from '@umbraco-cms/backoffice/collection';
import { UmbRepositoryBase } from '@umbraco-cms/backoffice/repository';
import { fromBlockRuleCollectionFilter } from '../query.js';
import type { PnftBlockRuleCollectionFilterModel, PnftBlockRuleCollectionModel } from '../types.js';
import { PnftBlockRuleServerDataSource } from './block-rule.data-source.js';

/**
 * The repository of the block rule collection (registered as `Pnft.Repository.BlockRule`): one page of rules, and the
 * filter choices. It takes the filter as the collection context holds it; the framework's text filter becomes the
 * API's `search` (on the pattern).
 */
export class PnftBlockRuleRepository
  extends UmbRepositoryBase
  implements UmbCollectionRepository<PnftBlockRuleCollectionModel, PnftBlockRuleCollectionFilterModel>
{
  #dataSource: PnftBlockRuleServerDataSource;

  constructor(host: UmbControllerHost) {
    super(host);
    this.#dataSource = new PnftBlockRuleServerDataSource(host);
  }

  /** Requests one page of block rules; missing paging and order fall back to the defaults (20, created descending). */
  async requestCollection(filter: PnftBlockRuleCollectionFilterModel = {}) {
    return this.#dataSource.getCollection(fromBlockRuleCollectionFilter(filter));
  }

  /** Requests the choices of the domain and culture filters. */
  async requestFilterOptions() {
    return this.#dataSource.getFilterOptions();
  }
}

export { PnftBlockRuleRepository as api };
