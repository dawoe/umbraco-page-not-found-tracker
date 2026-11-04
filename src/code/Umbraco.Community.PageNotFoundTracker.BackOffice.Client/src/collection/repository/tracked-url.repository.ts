import type { UmbControllerHost } from '@umbraco-cms/backoffice/controller-api';
import type { UmbCollectionRepository } from '@umbraco-cms/backoffice/collection';
import { UmbRepositoryBase } from '@umbraco-cms/backoffice/repository';
import { fromCollectionFilter } from '../query.js';
import type { PnftTrackedUrlCollectionFilterModel, PnftTrackedUrlCollectionModel } from '../types.js';
import { PnftTrackedUrlServerDataSource } from './tracked-url.data-source.js';

/**
 * The repository of the tracked URL collection (registered as `Pnft.Repository.TrackedUrl`): one page of
 * rows, and the filter choices. It takes the filter as the collection context holds it; the framework's text
 * filter becomes the API's `search`.
 */
export class PnftTrackedUrlRepository
  extends UmbRepositoryBase
  implements UmbCollectionRepository<PnftTrackedUrlCollectionModel, PnftTrackedUrlCollectionFilterModel>
{
  #dataSource: PnftTrackedUrlServerDataSource;

  constructor(host: UmbControllerHost) {
    super(host);
    this.#dataSource = new PnftTrackedUrlServerDataSource(host);
  }

  /** Requests one page of tracked URLs; missing paging and order fall back to the defaults (20, hits descending). */
  async requestCollection(filter: PnftTrackedUrlCollectionFilterModel = {}) {
    return this.#dataSource.getCollection(fromCollectionFilter(filter));
  }

  /** Requests the choices of the domain and culture filters. */
  async requestFilterOptions() {
    return this.#dataSource.getFilterOptions();
  }
}

export { PnftTrackedUrlRepository as api };
