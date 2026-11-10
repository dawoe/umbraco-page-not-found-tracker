import type { DeleteTrackedUrlsResponseModel } from '../api/index.js';
import { buildPagedQuery, fromCollectionFilter } from '../collection/query.js';
import type { PnftPagedCollectionFilterModel } from '../collection/types.js';

// Pure mapping of the delete tracked URL answers. No backoffice runtime imports, so it runs in Vitest; the dashboard
// passes the real request (the generated client through Umbraco's tryExecute) as `send`.

/** The outcome of a delete request, one kind per answer the editor needs to tell apart. */
export type PnftDeleteResult =
  /** 200: `deleted` rows were removed (for a single delete, 1). */
  | { kind: 'deleted'; deleted: number }
  /** 404: the tracked URL no longer exists. */
  | { kind: 'notFound' }
  /** 400: the request is not valid; `message` is the server's reason. */
  | { kind: 'invalid'; message: string | undefined }
  /** 503: the tracking database is unavailable. */
  | { kind: 'unavailable' }
  /** Anything else: network failure, another status, no body. */
  | { kind: 'error' };

/** What a request returns, as Umbraco's `tryExecute` hands it back. */
export interface PnftDeleteResponse<T> {
  data?: T;
  error?: unknown;
}

/** The delete-by-filter criteria; `all` means no constraint at all. */
export interface PnftDeleteMatchingCriteria {
  search?: string;
  domain?: string;
  noDomain?: boolean;
  culture?: string;
  noCulture?: boolean;
  all?: boolean;
}

/** Sends the delete request for one tracked URL id. */
export type PnftDeleteOneSend = (id: number) => Promise<PnftDeleteResponse<DeleteTrackedUrlsResponseModel>>;

/** Sends the delete request for several ids. */
export type PnftDeleteManySend = (ids: number[]) => Promise<PnftDeleteResponse<DeleteTrackedUrlsResponseModel>>;

/** Sends the delete request for everything a filter matches. */
export type PnftDeleteMatchingSend = (criteria: PnftDeleteMatchingCriteria) => Promise<PnftDeleteResponse<DeleteTrackedUrlsResponseModel>>;

/** Maps the collection filter to delete criteria, reusing the list query mapping; `all` is set when nothing is constrained. */
export function criteriaFromFilter(filter: PnftPagedCollectionFilterModel): PnftDeleteMatchingCriteria {
  const query = buildPagedQuery(fromCollectionFilter(filter), 'Hits');
  const criteria: PnftDeleteMatchingCriteria = {};

  if (query.search !== undefined) {
    criteria.search = query.search;
  }

  if (query.domain !== undefined) {
    criteria.domain = query.domain;
  }

  if (query.noDomain) {
    criteria.noDomain = true;
  }

  if (query.culture !== undefined) {
    criteria.culture = query.culture;
  }

  if (query.noCulture) {
    criteria.noCulture = true;
  }

  if (Object.keys(criteria).length === 0) {
    criteria.all = true;
  }

  return criteria;
}

/** Deletes one tracked URL; a rejected request counts as `error` (it never throws). */
export function deleteTrackedUrl(send: PnftDeleteOneSend, id: number): Promise<PnftDeleteResult> {
  return run(() => send(id));
}

/** Deletes the given tracked URLs. */
export function deleteTrackedUrls(send: PnftDeleteManySend, ids: number[]): Promise<PnftDeleteResult> {
  return run(() => send(ids));
}

/** Deletes every tracked URL the criteria match. */
export function deleteMatchingTrackedUrls(send: PnftDeleteMatchingSend, criteria: PnftDeleteMatchingCriteria): Promise<PnftDeleteResult> {
  return run(() => send(criteria));
}

async function run(request: () => Promise<PnftDeleteResponse<DeleteTrackedUrlsResponseModel>>): Promise<PnftDeleteResult> {
  try {
    const { data, error } = await request();

    if (!error && data) {
      return { kind: 'deleted', deleted: data.deleted };
    }

    return mapError(error);
  } catch (error) {
    return mapError(error ?? new Error('Request failed'));
  }
}

function mapError(error: unknown): PnftDeleteResult {
  switch ((error as { status?: unknown } | undefined)?.status) {
    case 400:
      return { kind: 'invalid', message: readable((error as { problemDetails?: { detail?: unknown } }).problemDetails?.detail) };
    case 404:
      return { kind: 'notFound' };
    case 503:
      return { kind: 'unavailable' };
    default:
      return { kind: 'error' };
  }
}

function readable(value: unknown): string | undefined {
  return typeof value === 'string' && value.trim() !== '' ? value : undefined;
}
