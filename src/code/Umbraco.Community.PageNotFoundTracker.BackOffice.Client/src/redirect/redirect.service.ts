import type { CreateRedirectResponseModel } from '../api/index.js';

// Pure mapping of the create redirect answer. No backoffice runtime imports, so it runs in Vitest; the modal passes
// the real request (the generated client through Umbraco's tryExecute) as `send`.

/** The outcome of a create redirect request, one kind per answer the editor needs to tell apart. */
export type PnftRedirectResult =
  /** 200: the redirect exists. `recordRemoved` false means the tracked record could not be deleted (`message` says so). */
  | { kind: 'created'; recordRemoved: boolean; message: string | undefined }
  /** 404: the tracked URL no longer exists (deleted meanwhile). */
  | { kind: 'notFound' }
  /** 409: the active provider does not support this tracked URL; `message` is the provider's reason. */
  | { kind: 'notSupported'; message: string | undefined }
  /** 422: the provider could not create the redirect; `message` is the provider's reason. */
  | { kind: 'failed'; message: string | undefined }
  /** 503: the tracking database is unavailable. */
  | { kind: 'unavailable' }
  /** Anything else: network failure, another status, no body. */
  | { kind: 'error' };

/** What the request returns, as Umbraco's `tryExecute` hands it back (`error` is an `UmbApiError` for API errors). */
export interface PnftRedirectResponse {
  data?: CreateRedirectResponseModel;
  error?: unknown;
}

/** Sends the create redirect request for a tracked URL id with the form's payload. */
export type PnftRedirectSend = (id: number, payload: unknown) => Promise<PnftRedirectResponse>;

/** Maps the answer of the create redirect endpoint to a {@link PnftRedirectResult}. */
export function mapRedirectResponse(response: PnftRedirectResponse): PnftRedirectResult {
  const { data, error } = response;

  if (!error && data) {
    return { kind: 'created', recordRemoved: data.recordRemoved, message: readable(data.message) };
  }

  const status = (error as { status?: unknown } | undefined)?.status;

  switch (status) {
    case 404:
      return { kind: 'notFound' };
    case 409:
      return { kind: 'notSupported', message: detailOf(error) };
    case 422:
      return { kind: 'failed', message: detailOf(error) };
    case 503:
      return { kind: 'unavailable' };
    default:
      return { kind: 'error' };
  }
}

/** Sends the request and maps the answer; a rejected request counts as `error` (it never throws). */
export async function createRedirect(send: PnftRedirectSend, id: number, payload: unknown): Promise<PnftRedirectResult> {
  try {
    return mapRedirectResponse(await send(id, payload));
  } catch (error) {
    return mapRedirectResponse({ error: error ?? new Error('Request failed') });
  }
}

function detailOf(error: unknown): string | undefined {
  return readable((error as { problemDetails?: { detail?: unknown } }).problemDetails?.detail);
}

function readable(value: unknown): string | undefined {
  return typeof value === 'string' && value.trim() !== '' ? value : undefined;
}
