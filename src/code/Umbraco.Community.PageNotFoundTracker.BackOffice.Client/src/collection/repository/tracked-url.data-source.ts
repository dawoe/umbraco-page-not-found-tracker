import type { UmbControllerHost } from '@umbraco-cms/backoffice/controller-api';
import { tryExecute } from '@umbraco-cms/backoffice/resources';
import type { UmbRepositoryResponse } from '@umbraco-cms/backoffice/repository';
import type { UmbPagedModel } from '@umbraco-cms/backoffice/repository';
import { getTrackedUrlFilterOptions, getTrackedUrls, type TrackedUrlResponseModel } from '../../api/index.js';
import { PNFT_TRACKED_URL_ENTITY_TYPE } from '../../entity.js';
import { buildListQuery } from '../query.js';
import type { PnftCollectionFilter, PnftTrackedUrlCollectionModel, PnftTrackedUrlFilterOptionsModel } from '../types.js';

/**
 * Reads tracked URLs from the package's Management API through the generated client (configured
 * with Umbraco's auth in entry.ts). Errors are handled and notified by `tryExecute`.
 */
export class PnftTrackedUrlServerDataSource {
  #host: UmbControllerHost;

  constructor(host: UmbControllerHost) {
    this.#host = host;
  }

  /** Gets one page of tracked URLs. */
  async getCollection(filter: Partial<PnftCollectionFilter>): Promise<UmbRepositoryResponse<UmbPagedModel<PnftTrackedUrlCollectionModel>>> {
    const { data, error } = await tryExecute(this.#host, getTrackedUrls({ query: buildListQuery(filter) }));

    if (error || !data) {
      return { error };
    }

    return { data: { items: data.items.map(mapItem), total: data.total } };
  }

  /** Gets the choices of the domain and culture filters. */
  async getFilterOptions(): Promise<UmbRepositoryResponse<PnftTrackedUrlFilterOptionsModel>> {
    const { data, error } = await tryExecute(this.#host, getTrackedUrlFilterOptions());

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

function mapItem(item: TrackedUrlResponseModel): PnftTrackedUrlCollectionModel {
  return {
    entityType: PNFT_TRACKED_URL_ENTITY_TYPE,
    unique: String(item.id),
    id: item.id,
    url: item.url,
    relativePath: item.relativePath,
    domain: item.domain,
    culture: item.culture,
    isFile: item.isFile,
    hitCount: item.hitCount,
    firstSeen: item.firstSeen,
    lastSeen: item.lastSeen,
  };
}
