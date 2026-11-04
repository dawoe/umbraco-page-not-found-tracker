import type { GetTrackedUrlsData } from '../api/index.js';
import {
  PNFT_EMPTY,
  type PnftCollectionFilter,
  type PnftFilterValue,
  type PnftTrackedUrlCollectionFilterModel,
} from './types.js';

/** The list query parameters of the Management API. */
export type PnftListQuery = NonNullable<GetTrackedUrlsData['query']>;

/** The page size when the filter does not say otherwise. */
export const PNFT_DEFAULT_TAKE = 20;

/**
 * Maps the collection filter to the list query parameters. Pure.
 *
 * - Missing paging and order fall back to skip 0, take 20, hits descending.
 * - A blank search is left out.
 * - The `PNFT_EMPTY` marker (a string starting with U+0000) becomes `noDomain=true` / `noCulture=true`;
 *   any other string (also one that reads like a marker word, such as `empty`) is sent as a value;
 *   undefined or an empty string means all.
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

/**
 * Maps the filter the Umbraco collection context holds to the repository filter. Pure.
 *
 * The framework's text filter (`filter`) becomes `search` (untrimmed; `buildListQuery` drops a blank one,
 * as the server would), `orderDirection` becomes `direction`. Keys the API does not know (for example the
 * `pageSize` the context copies from its configuration) are dropped.
 */
export function fromCollectionFilter(filter: PnftTrackedUrlCollectionFilterModel): Partial<PnftCollectionFilter> {
  const mapped: Partial<PnftCollectionFilter> = {};

  if (filter.skip !== undefined) {
    mapped.skip = filter.skip;
  }

  if (filter.take !== undefined) {
    mapped.take = filter.take;
  }

  if (filter.orderBy !== undefined) {
    mapped.orderBy = filter.orderBy;
  }

  if (filter.orderDirection !== undefined) {
    mapped.direction = filter.orderDirection;
  }

  if (filter.filter !== undefined) {
    mapped.search = filter.filter;
  }

  if (filter.domain !== undefined) {
    mapped.domain = filter.domain;
  }

  if (filter.culture !== undefined) {
    mapped.culture = filter.culture;
  }

  return mapped;
}

function applyValueFilter(value: PnftFilterValue | undefined, setValue: (value: string) => void, setEmpty: () => void): void {
  if (value === PNFT_EMPTY) {
    setEmpty();
  } else if (typeof value === 'string' && value !== '') {
    setValue(value);
  }
}
