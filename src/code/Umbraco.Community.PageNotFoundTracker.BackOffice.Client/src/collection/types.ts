import type { DirectionModel, TrackedUrlSortColumn } from '../api/index.js';
import type { PNFT_TRACKED_URL_ENTITY_TYPE } from '../entity.js';

/**
 * Marks the "empty" choice of the domain and culture filters (URLs without a domain or culture).
 *
 * A string, so it survives `JSON.stringify`: Umbraco's `UmbDeepState` only emits when the serialised value
 * changes, and a symbol would serialise like "All". It starts with U+0000, which no Umbraco domain name or
 * culture code contains, so a real domain or culture whose text looks like a marker word (`empty`, `all`)
 * is still sent as a value.
 */
export const PNFT_EMPTY = '\u0000empty';

/** The value of the domain or culture filter: a value, the {@link PNFT_EMPTY} marker, or undefined for all. */
export type PnftFilterValue = string;

/** The filter a collection passes to its repository (the API's list parameters before mapping), per sort column type. */
export interface PnftPagedFilter<TOrderBy extends string> {
  skip: number;
  take: number;
  orderBy: TOrderBy;
  direction: DirectionModel;
  search?: string;
  domain?: PnftFilterValue;
  culture?: PnftFilterValue;
}

/**
 * The filter as the Umbraco collection context holds it: the framework's `skip`, `take` and `filter`
 * (the search text) and its `orderBy`/`orderDirection` ordering keys, plus the domain and culture filters.
 * The repository maps it to {@link PnftPagedFilter} with `fromCollectionFilter`.
 */
export interface PnftPagedCollectionFilterModel<TOrderBy extends string = string> {
  skip?: number;
  take?: number;
  /** The search text (the framework's text filter). */
  filter?: string;
  orderBy?: TOrderBy;
  orderDirection?: DirectionModel;
  domain?: PnftFilterValue;
  culture?: PnftFilterValue;
}

/** The filter the tracked URL collection passes to the repository. */
export type PnftCollectionFilter = PnftPagedFilter<TrackedUrlSortColumn>;

/** The filter of the tracked URL collection as the collection context holds it. */
export type PnftTrackedUrlCollectionFilterModel = PnftPagedCollectionFilterModel<TrackedUrlSortColumn>;

/** One row of the tracked URL collection. */
export interface PnftTrackedUrlCollectionModel {
  entityType: typeof PNFT_TRACKED_URL_ENTITY_TYPE;
  unique: string;
  id: number;
  url: string;
  relativePath: string;
  domain: string;
  culture: string;
  isFile: boolean;
  hitCount: number;
  /** UTC, ISO 8601. */
  firstSeen: string;
  /** UTC, ISO 8601. */
  lastSeen: string;
  /** Whether the active redirect provider supports this row: the create redirect action shows only then. */
  canCreateRedirect: boolean;
}

/** The choices of a collection's domain and culture filters. */
export interface PnftFilterOptionsModel {
  domains: Array<string>;
  cultures: Array<string>;
  hasEmptyDomain: boolean;
  hasEmptyCulture: boolean;
}

/** The choices of the tracked URL collection's domain and culture filters. */
export type PnftTrackedUrlFilterOptionsModel = PnftFilterOptionsModel;
