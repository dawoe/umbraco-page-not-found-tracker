import { PNFT_EMPTY, type PnftPagedCollectionFilterModel } from './types.js';

// Pure helpers of the collection's own state (errors, page size, page reset). No backoffice runtime imports,
// so they run in Vitest without a browser.

/** What went wrong with the last list request. */
export type PnftCollectionErrorKind = 'error' | 'unavailable';

/** Which state the collection shows instead of rows. */
export type PnftEmptyState = 'empty' | 'empty-filtered' | PnftCollectionErrorKind;

/** The page sizes the user can choose. */
export const PNFT_PAGE_SIZES: ReadonlyArray<number> = [20, 50, 100];

/** The page size when the user has not chosen one. */
export const PNFT_DEFAULT_PAGE_SIZE = 20;

/**
 * Classifies a repository error: a 503 (tracking database unavailable) is `unavailable`, a cancelled
 * request is no error at all, anything else (other statuses, network failures) is `error`.
 */
export function classifyError(error: unknown): PnftCollectionErrorKind | undefined {
  if (!error) {
    return undefined;
  }

  const candidate = error as { name?: string; status?: number };

  if (candidate.name === 'UmbCancelError') {
    return undefined;
  }

  return candidate.status === 503 ? 'unavailable' : 'error';
}

/** Whether the search or a domain/culture filter narrows the list (so an empty list means "no match"). */
export function isFilterActive(filter: PnftPagedCollectionFilterModel): boolean {
  const hasSearch = typeof filter.filter === 'string' && filter.filter.trim() !== '';
  const hasDomain = filter.domain === PNFT_EMPTY || (typeof filter.domain === 'string' && filter.domain !== '');
  const hasCulture = filter.culture === PNFT_EMPTY || (typeof filter.culture === 'string' && filter.culture !== '');

  return hasSearch || hasDomain || hasCulture;
}

/** Picks the state shown when the list has no rows. An error wins over the empty states. */
export function pickEmptyState(error: PnftCollectionErrorKind | undefined, filtered: boolean): PnftEmptyState {
  if (error) {
    return error;
  }

  return filtered ? 'empty-filtered' : 'empty';
}

/**
 * Returns the filter change with `skip: 0` added, so search, filter, sort and page size changes start on
 * page 1. A change that sets `skip` itself (a page change, a restored page) is kept as it is.
 */
export function withPageReset<T extends PnftPagedCollectionFilterModel>(change: T): T {
  return 'skip' in change ? change : { ...change, skip: 0 };
}

/** Parses a page size choice; anything but an allowed size gives the default. */
export function parsePageSize(value: unknown): number {
  const size = Number(value);

  return PNFT_PAGE_SIZES.includes(size) ? size : PNFT_DEFAULT_PAGE_SIZE;
}
