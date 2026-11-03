import type { DirectionModel, TrackedUrlSortColumn } from '../api/index.js';
import type { PNFT_TRACKED_URL_ENTITY_TYPE } from '../entity.js';

/**
 * Marks the "empty" choice of the domain and culture filters (URLs without a domain or culture).
 * A unique symbol, never a string, so a domain or culture whose text happens to look like a marker
 * word is still sent as a value.
 */
export const PNFT_EMPTY: unique symbol = Symbol('pnft-empty');

/** The value of the domain or culture filter: a value, the "empty" marker, or undefined for all. */
export type PnftFilterValue = string | typeof PNFT_EMPTY;

/** The filter the collection passes to the repository. */
export interface PnftCollectionFilter {
  skip: number;
  take: number;
  orderBy: TrackedUrlSortColumn;
  direction: DirectionModel;
  search?: string;
  domain?: PnftFilterValue;
  culture?: PnftFilterValue;
}

/**
 * The filter as the Umbraco collection context holds it: the framework's `skip`, `take` and `filter`
 * (the search text) and its `orderBy`/`orderDirection` ordering keys, plus the domain and culture filters.
 * The repository maps it to {@link PnftCollectionFilter} with `fromCollectionFilter`.
 */
export interface PnftTrackedUrlCollectionFilterModel {
  skip?: number;
  take?: number;
  /** The URL search text (the framework's text filter). */
  filter?: string;
  orderBy?: TrackedUrlSortColumn;
  orderDirection?: DirectionModel;
  domain?: PnftFilterValue;
  culture?: PnftFilterValue;
}

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
}

/** The choices of the domain and culture filters. */
export interface PnftTrackedUrlFilterOptionsModel {
  domains: Array<string>;
  cultures: Array<string>;
  hasEmptyDomain: boolean;
  hasEmptyCulture: boolean;
}
