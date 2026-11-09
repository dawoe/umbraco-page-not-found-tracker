import type { DirectionModel, GetTrackedUrlsData } from '../api/index.js';
import {
  PNFT_EMPTY,
  type PnftCollectionFilter,
  type PnftFilterValue,
  type PnftPagedCollectionFilterModel,
  type PnftPagedFilter,
} from './types.js';

/** The list query parameters of the Management API. */
export type PnftListQuery = NonNullable<GetTrackedUrlsData['query']>;

/** The list query parameters shared by the package's list endpoints (tracked URLs, block rules), per sort column type. */
export interface PnftPagedQuery<TOrderBy extends string> {
  skip: number;
  take: number;
  orderBy: TOrderBy;
  direction: DirectionModel;
  search?: string;
  domain?: string;
  noDomain?: boolean;
  culture?: string;
  noCulture?: boolean;
}

/** The page size when the filter does not say otherwise. */
export const PNFT_DEFAULT_TAKE = 20;

/**
 * Maps a collection filter to list query parameters. Pure.
 *
 * - Missing paging and order fall back to skip 0, take 20, the given column, descending.
 * - A blank search is left out.
 * - The `PNFT_EMPTY` marker (a string starting with U+0000) becomes `noDomain=true` / `noCulture=true`;
 *   any other string (also one that reads like a marker word, such as `empty`) is sent as a value;
 *   undefined or an empty string means all.
 */
export function buildPagedQuery<TOrderBy extends string>(
  filter: Partial<PnftPagedFilter<TOrderBy>>,
  defaultOrderBy: TOrderBy,
): PnftPagedQuery<TOrderBy> {
  const query: PnftPagedQuery<TOrderBy> = {
    skip: filter.skip ?? 0,
    take: filter.take ?? PNFT_DEFAULT_TAKE,
    orderBy: filter.orderBy ?? defaultOrderBy,
    direction: filter.direction ?? 'Descending',
  };

  if (filter.search !== undefined && filter.search.trim() !== '') {
    query.search = filter.search;
  }

  applyValueFilter(filter.domain, (value) => (query.domain = value), () => (query.noDomain = true));
  applyValueFilter(filter.culture, (value) => (query.culture = value), () => (query.noCulture = true));

  return query;
}

/** Maps the tracked URL collection filter to the list query parameters (default order: hits descending). Pure. */
export function buildListQuery(filter: Partial<PnftCollectionFilter>): PnftListQuery {
  return buildPagedQuery(filter, 'Hits');
}

/**
 * Maps the filter the Umbraco collection context holds to the repository filter. Pure.
 *
 * The framework's text filter (`filter`) becomes `search` (untrimmed; `buildPagedQuery` drops a blank one,
 * as the server would), `orderDirection` becomes `direction`. Keys the API does not know (for example the
 * `pageSize` the context copies from its configuration) are dropped.
 */
export function fromCollectionFilter<TOrderBy extends string>(
  filter: PnftPagedCollectionFilterModel<TOrderBy>,
): Partial<PnftPagedFilter<TOrderBy>> {
  const mapped: Partial<PnftPagedFilter<TOrderBy>> = {};

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
