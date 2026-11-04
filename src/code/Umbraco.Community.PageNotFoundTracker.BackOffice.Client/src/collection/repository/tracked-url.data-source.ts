import type { UmbControllerHost } from '@umbraco-cms/backoffice/controller-api';
import { tryExecute } from '@umbraco-cms/backoffice/resources';
import type { UmbRepositoryResponse } from '@umbraco-cms/backoffice/repository';
import type { UmbPagedModel } from '@umbraco-cms/backoffice/repository';
import { getTrackedUrlFilterOptions, getTrackedUrls } from '../../api/index.js';
import { buildListQuery } from '../query.js';
import { mapTrackedUrl } from './tracked-url.mapping.js';
import type { PnftCollectionFilter, PnftTrackedUrlCollectionModel, PnftTrackedUrlFilterOptionsModel } from '../types.js';

/**
 * Reads tracked URLs from the package's Management API through the generated client (configured
 * with Umbraco's auth in entry.ts). Errors are returned, not notified: the collection shows them as its
 * error and unavailable states.
 */
export class PnftTrackedUrlServerDataSource {
  #host: UmbControllerHost;

  constructor(host: UmbControllerHost) {
    this.#host = host;
  }

  /** Gets one page of tracked URLs. */
  async getCollection(filter: Partial<PnftCollectionFilter>): Promise<UmbRepositoryResponse<UmbPagedModel<PnftTrackedUrlCollectionModel>>> {
    const { data, error } = await tryExecute(this.#host, getTrackedUrls({ query: buildListQuery(filter) }), {
      disableNotifications: true,
    });

    if (error || !data) {
      return { error };
    }

    return { data: { items: data.items.map(mapTrackedUrl), total: data.total } };
  }

  /** Gets the choices of the domain and culture filters. */
  async getFilterOptions(): Promise<UmbRepositoryResponse<PnftTrackedUrlFilterOptionsModel>> {
    const { data, error } = await tryExecute(this.#host, getTrackedUrlFilterOptions(), { disableNotifications: true });

    if (error || !data) {
      return { error };
    }

    return {
      data: {
        domains: [...data.domains],
        cultures: [...data.cultures],
        hasEmptyDomain: data.hasEmptyDomain,
        hasEmptyCulture: data.hasEmptyCulture,
      },
    };
  }
}
