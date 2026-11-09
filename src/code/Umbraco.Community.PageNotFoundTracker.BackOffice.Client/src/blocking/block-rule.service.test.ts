import { describe, expect, it, vi } from 'vitest';
import { createRule, deleteRule, mapCreateRuleResponse, mapDeleteRuleResponse, updateRule } from './block-rule.service.js';

/** An error as Umbraco's tryExecute returns it (an UmbApiError: status plus problem details). */
function apiError(status: number, detail?: string) {
  return { name: 'UmbApiError', status, message: detail ?? 'x', problemDetails: { status, title: 'Title', detail } };
}

const request = { pattern: '/wp-admin/*', statusCode: 410, domain: '', culture: '' };

describe('mapCreateRuleResponse', () => {
  it('maps a 200 with the records removed to saved', () => {
    expect(mapCreateRuleResponse({ data: { recordsRemoved: true, message: null } })).toEqual({
      kind: 'saved',
      recordsRemoved: true,
      message: undefined,
    });
  });

  it('maps a 200 with a removal failure to saved with the message', () => {
    expect(
      mapCreateRuleResponse({ data: { recordsRemoved: false, message: 'The rule was saved, but the tracked URLs could not be removed.' } }),
    ).toEqual({
      kind: 'saved',
      recordsRemoved: false,
      message: 'The rule was saved, but the tracked URLs could not be removed.',
    });
  });

  it('maps a 400 to invalid with the server message', () => {
    expect(mapCreateRuleResponse({ error: apiError(400, 'The pattern must contain a literal character.') })).toEqual({
      kind: 'invalid',
      message: 'The pattern must contain a literal character.',
    });
  });

  it('maps a 400 without a readable detail to invalid without a message', () => {
    expect(mapCreateRuleResponse({ error: apiError(400, '  ') })).toEqual({ kind: 'invalid', message: undefined });
  });

  it('maps a 404 to not found', () => {
    expect(mapCreateRuleResponse({ error: apiError(404) })).toEqual({ kind: 'notFound' });
  });

  it('maps a 409 to conflict with the server message', () => {
    expect(mapCreateRuleResponse({ error: apiError(409, 'A rule with this pattern, domain and culture already exists.') })).toEqual({
      kind: 'conflict',
      message: 'A rule with this pattern, domain and culture already exists.',
    });
  });

  it('maps a 503 to unavailable', () => {
    expect(mapCreateRuleResponse({ error: apiError(503, 'Database down') })).toEqual({ kind: 'unavailable' });
  });

  it('maps a network failure, another status or a missing body to error', () => {
    expect(mapCreateRuleResponse({ error: apiError(0, 'Connection lost') })).toEqual({ kind: 'error' });
    expect(mapCreateRuleResponse({ error: apiError(500, 'Boom') })).toEqual({ kind: 'error' });
    expect(mapCreateRuleResponse({ error: new TypeError('Failed to fetch') })).toEqual({ kind: 'error' });
    expect(mapCreateRuleResponse({})).toEqual({ kind: 'error' });
  });
});

describe('mapDeleteRuleResponse', () => {
  it('maps a 200 (no body) to deleted', () => {
    expect(mapDeleteRuleResponse({ data: undefined })).toEqual({ kind: 'deleted' });
    expect(mapDeleteRuleResponse({ data: '' })).toEqual({ kind: 'deleted' });
  });

  it('maps a 404 to not found', () => {
    expect(mapDeleteRuleResponse({ error: apiError(404, 'The block rule does not exist (anymore).') })).toEqual({ kind: 'notFound' });
  });

  it('maps a 503 to unavailable and anything else to error', () => {
    expect(mapDeleteRuleResponse({ error: apiError(503) })).toEqual({ kind: 'unavailable' });
    expect(mapDeleteRuleResponse({ error: apiError(500) })).toEqual({ kind: 'error' });
    expect(mapDeleteRuleResponse({ error: new TypeError('Failed to fetch') })).toEqual({ kind: 'error' });
  });
});

describe('createRule', () => {
  it('sends the request and maps the answer', async () => {
    const send = vi.fn().mockResolvedValue({ data: { recordsRemoved: true } });

    const result = await createRule(send, request);

    expect(send).toHaveBeenCalledWith(request);
    expect(result).toEqual({ kind: 'saved', recordsRemoved: true, message: undefined });
  });

  it('maps a rejected send to error instead of throwing', async () => {
    const send = vi.fn().mockRejectedValue(new TypeError('Failed to fetch'));

    await expect(createRule(send, request)).resolves.toEqual({ kind: 'error' });
  });
});

describe('deleteRule', () => {
  it('sends the id and maps the answer', async () => {
    const send = vi.fn().mockResolvedValue({});

    await expect(deleteRule(send, 12)).resolves.toEqual({ kind: 'deleted' });
    expect(send).toHaveBeenCalledWith(12);
  });

  it('maps a rejected send to error instead of throwing', async () => {
    const send = vi.fn().mockRejectedValue(new TypeError('Failed to fetch'));

    await expect(deleteRule(send, 12)).resolves.toEqual({ kind: 'error' });
  });
});

describe('updateRule', () => {
  it('sends the id and the request and maps the answer', async () => {
    const send = vi.fn().mockResolvedValue({ data: { recordsRemoved: true, message: null } });
    expect(await updateRule(send, 7, request)).toEqual({ kind: 'saved', recordsRemoved: true, message: undefined });
    expect(send).toHaveBeenCalledWith(7, request);
  });

  it('maps a rejected send to error instead of throwing', async () => {
    expect(await updateRule(vi.fn().mockRejectedValue(new Error('network')), 7, request)).toEqual({ kind: 'error' });
  });
});
