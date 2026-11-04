import type { TrackedUrlResponseModel } from '../../api/index.js';
import { PNFT_TRACKED_URL_ENTITY_TYPE } from '../../entity.js';
import type { PnftTrackedUrlCollectionModel } from '../types.js';

// Pure mapping of an API item to a collection row. No backoffice runtime imports, so it runs in Vitest.

/** Maps one API item to a row of the tracked URL collection (entity type, `unique` = the id as a string). */
export function mapTrackedUrl(item: TrackedUrlResponseModel): PnftTrackedUrlCollectionModel {
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
    canCreateRedirect: item.canCreateRedirect,
  };
}
