import type { GetTrackedUrlsData } from '../api/index.js';
import { PNFT_EMPTY, type PnftCollectionFilter, type PnftFilterValue } from './types.js';

/** The list query parameters of the Management API. */
export type PnftListQuery = NonNullable<GetTrackedUrlsData['query']>;

/** The page size when the filter does not say otherwise. */
export const PNFT_DEFAULT_TAKE = 20;

/**
 * Maps the collection filter to the list query parameters. Pure.
 *
 * - Missing paging and order fall back to skip 0, take 20, hits descending.
 * - A blank search is left out.
 * - The `PNFT_EMPTY` marker becomes `noDomain=true` / `noCulture=true`; any string (also one that
 *   reads like a marker word) is sent as a value; undefined or an empty string means all.
 */
export function buildListQuery(filter: Partial<PnftCollectionFilter>): PnftListQuery {
  const query: PnftListQuery = {
    skip: filter.skip ?? 0,
    take: filter.take ?? PNFT_DEFAULT_TAKE,
    orderBy: filter.orderBy ?? 'Hits',
    direction: filter.direction ?? 'Descending',
  };

  if (filter.search !== undefined && filter.search.trim() !== '') {
    query.search = filter.search;
  }

  applyValueFilter(filter.domain, (value) => (query.domain = value), () => (query.noDomain = true));
  applyValueFilter(filter.culture, (value) => (query.culture = value), () => (query.noCulture = true));

  return query;
}

function applyValueFilter(value: PnftFilterValue | undefined, setValue: (value: string) => void, setEmpty: () => void): void {
  if (value === PNFT_EMPTY) {
    setEmpty();
  } else if (typeof value === 'string' && value !== '') {
    setValue(value);
  }
}
