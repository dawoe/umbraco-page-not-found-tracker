import { describe, expect, it } from 'vitest';
import { canCreateRedirectFor } from './can-create-redirect.js';

const rows = [
  { unique: '1', canCreateRedirect: true },
  { unique: '2', canCreateRedirect: false },
];

describe('canCreateRedirectFor', () => {
  it('is true only for a row the server flagged', () => {
    expect(canCreateRedirectFor(rows, '1')).toBe(true);
    expect(canCreateRedirectFor(rows, '2')).toBe(false);
  });

  it('is false for an unknown row, a missing unique or no rows', () => {
    expect(canCreateRedirectFor(rows, '3')).toBe(false);
    expect(canCreateRedirectFor(rows, undefined)).toBe(false);
    expect(canCreateRedirectFor(rows, null)).toBe(false);
    expect(canCreateRedirectFor(undefined, '1')).toBe(false);
  });
});
