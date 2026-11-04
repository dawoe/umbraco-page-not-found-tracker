import { describe, expect, it, vi } from 'vitest';
import { createRedirect, mapRedirectResponse } from './redirect.service.js';

/** An error as Umbraco's tryExecute returns it (an UmbApiError: status plus problem details). */
function apiError(status: number, detail?: string) {
  return { name: 'UmbApiError', status, message: detail ?? 'x', problemDetails: { status, title: 'Title', detail } };
}

describe('mapRedirectResponse', () => {
  it('maps a 200 with the record removed to created', () => {
    expect(mapRedirectResponse({ data: { recordRemoved: true, message: null } })).toEqual({
      kind: 'created',
      recordRemoved: true,
      message: undefined,
    });
  });

  it('maps a 200 with the record kept to created with the message', () => {
    expect(
      mapRedirectResponse({ data: { recordRemoved: false, message: 'The redirect was created, but the record could not be removed.' } }),
    ).toEqual({
      kind: 'created',
      recordRemoved: false,
      message: 'The redirect was created, but the record could not be removed.',
    });
  });

  it('maps a 404 to not found', () => {
    expect(mapRedirectResponse({ error: apiError(404) })).toEqual({ kind: 'notFound' });
  });

  it('maps a 409 to not supported with the provider message', () => {
    expect(mapRedirectResponse({ error: apiError(409, 'Files are not supported.') })).toEqual({
      kind: 'notSupported',
      message: 'Files are not supported.',
    });
  });

  it('maps a 422 to failed with the provider message', () => {
    expect(mapRedirectResponse({ error: apiError(422, 'The target page is not published.') })).toEqual({
      kind: 'failed',
      message: 'The target page is not published.',
    });
  });

  it('keeps a 409 or 422 without a readable detail distinct, without a message', () => {
    expect(mapRedirectResponse({ error: apiError(409) })).toEqual({ kind: 'notSupported', message: undefined });
    expect(mapRedirectResponse({ error: apiError(422, '   ') })).toEqual({ kind: 'failed', message: undefined });
  });

  it('maps a 503 to unavailable', () => {
    expect(mapRedirectResponse({ error: apiError(503, 'Database down') })).toEqual({ kind: 'unavailable' });
  });

  it('maps a network failure, another status or a missing body to error', () => {
    expect(mapRedirectResponse({ error: apiError(0, 'Connection lost') })).toEqual({ kind: 'error' });
    expect(mapRedirectResponse({ error: apiError(500, 'Boom') })).toEqual({ kind: 'error' });
    expect(mapRedirectResponse({ error: new TypeError('Failed to fetch') })).toEqual({ kind: 'error' });
    expect(mapRedirectResponse({})).toEqual({ kind: 'error' });
  });
});

describe('createRedirect', () => {
  it('sends the id and the payload and maps the answer', async () => {
    const send = vi.fn().mockResolvedValue({ data: { recordRemoved: true } });

    const result = await createRedirect(send, 42, { targetKey: 'abc' });

    expect(send).toHaveBeenCalledWith(42, { targetKey: 'abc' });
    expect(result).toEqual({ kind: 'created', recordRemoved: true, message: undefined });
  });

  it('maps a rejected send to error instead of throwing', async () => {
    const send = vi.fn().mockRejectedValue(new TypeError('Failed to fetch'));

    await expect(createRedirect(send, 1, {})).resolves.toEqual({ kind: 'error' });
  });
});
