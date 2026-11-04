import type { CreateBlockRuleRequestModel, CreateBlockRuleResponseModel } from '../api/index.js';

// Pure mapping of the create and delete block rule answers. No backoffice runtime imports, so it runs in Vitest; the
// modal and the unblock action pass the real request (the generated client through Umbraco's tryExecute) as `send`.

/** The outcome of a block rule request, one kind per answer the editor needs to tell apart. */
export type PnftBlockRuleResult =
  /** Create 200: the rule is saved. `recordsRemoved` false means the matching tracked URLs could not be deleted (`message` says so). */
  | { kind: 'saved'; recordsRemoved: boolean; message: string | undefined }
  /** Delete 200: the rule is gone. */
  | { kind: 'deleted' }
  /** 400: the pattern or status code is not valid; `message` is the server's reason. */
  | { kind: 'invalid'; message: string | undefined }
  /** 404: the rule no longer exists (deleted meanwhile). */
  | { kind: 'notFound' }
  /** 503: the tracking database is unavailable. */
  | { kind: 'unavailable' }
  /** Anything else: network failure, another status, no body. */
  | { kind: 'error' };

/** What a request returns, as Umbraco's `tryExecute` hands it back (`error` is an `UmbApiError` for API errors). */
export interface PnftBlockRuleResponse<T> {
  data?: T;
  error?: unknown;
}

/** The create request: a pattern, a status code and the scope (an empty domain or culture means all). */
export type PnftCreateRuleRequest = Required<{ [K in keyof CreateBlockRuleRequestModel]: NonNullable<CreateBlockRuleRequestModel[K]> }>;

/** Sends the create block rule request. */
export type PnftCreateRuleSend = (request: PnftCreateRuleRequest) => Promise<PnftBlockRuleResponse<CreateBlockRuleResponseModel>>;

/** Sends the delete block rule request for a rule id. */
export type PnftDeleteRuleSend = (id: number) => Promise<PnftBlockRuleResponse<unknown>>;

/** Maps the answer of the create endpoint. */
export function mapCreateRuleResponse(response: PnftBlockRuleResponse<CreateBlockRuleResponseModel>): PnftBlockRuleResult {
  const { data, error } = response;

  if (!error && data) {
    return { kind: 'saved', recordsRemoved: data.recordsRemoved, message: readable(data.message) };
  }

  return mapError(error);
}

/** Maps the answer of the delete endpoint (a 200 has no body). */
export function mapDeleteRuleResponse(response: PnftBlockRuleResponse<unknown>): PnftBlockRuleResult {
  return response.error ? mapError(response.error) : { kind: 'deleted' };
}

/** Sends the create request and maps the answer; a rejected request counts as `error` (it never throws). */
export async function createRule(send: PnftCreateRuleSend, request: PnftCreateRuleRequest): Promise<PnftBlockRuleResult> {
  try {
    return mapCreateRuleResponse(await send(request));
  } catch (error) {
    return mapError(error ?? new Error('Request failed'));
  }
}

/** Sends the delete request and maps the answer; a rejected request counts as `error` (it never throws). */
export async function deleteRule(send: PnftDeleteRuleSend, id: number): Promise<PnftBlockRuleResult> {
  try {
    return mapDeleteRuleResponse(await send(id));
  } catch (error) {
    return mapError(error ?? new Error('Request failed'));
  }
}

function mapError(error: unknown): PnftBlockRuleResult {
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
