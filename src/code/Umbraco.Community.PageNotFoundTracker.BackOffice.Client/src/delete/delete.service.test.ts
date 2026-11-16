import { describe, expect, it } from 'vitest';
import { PNFT_EMPTY } from '../collection/types.js';
import { criteriaFromFilter, deleteMatchingTrackedUrls, deleteTrackedUrl, deleteTrackedUrls } from './delete.service.js';

const apiError = (status: number, detail?: string) => ({ status, problemDetails: { detail } });

describe('delete service', () => {
  it('maps 200 to deleted with the count', async () => {
    expect(await deleteTrackedUrls(async () => ({ data: { deleted: 3 } }), [1, 2, 3])).toEqual({ kind: 'deleted', deleted: 3 });
  });

  it('maps 404 to notFound', async () => {
    expect(await deleteTrackedUrl(async () => ({ error: apiError(404) }), 1)).toEqual({ kind: 'notFound' });
  });

  it('maps 400 to invalid with the detail', async () => {
    expect(await deleteMatchingTrackedUrls(async () => ({ error: apiError(400, 'Too many') }), { all: true })).toEqual({
      kind: 'invalid',
      message: 'Too many',
    });
  });

  it('maps 503 to unavailable', async () => {
    expect(await deleteTrackedUrls(async () => ({ error: apiError(503) }), [1])).toEqual({ kind: 'unavailable' });
  });

  it('maps a rejected send to error', async () => {
    expect(await deleteTrackedUrl(async () => Promise.reject(new Error('x')), 1)).toEqual({ kind: 'error' });
  });
});

describe('criteriaFromFilter', () => {
  it('sets all when nothing is constrained', () => {
    expect(criteriaFromFilter({})).toEqual({ all: true });
  });

  it('maps the empty markers to noDomain and noCulture', () => {
    expect(criteriaFromFilter({ domain: PNFT_EMPTY, culture: PNFT_EMPTY })).toEqual({ noDomain: true, noCulture: true });
  });

  it('ignores a whitespace-only search', () => {
    expect(criteriaFromFilter({ filter: '   ' })).toEqual({ all: true });
  });

  it('does not set all when a search is present', () => {
    expect(criteriaFromFilter({ filter: 'abc', domain: 'x.com' })).toEqual({ search: 'abc', domain: 'x.com' });
  });
});
