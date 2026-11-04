import { describe, expect, it } from 'vitest';
import { PNFT_DEFAULT_REDIRECT_FORM_ALIAS, PNFT_REDIRECT_FORM_EXTENSION_TYPE } from './constants.js';
import { manifests } from './manifests.js';

describe('redirect form manifests', () => {
  const form = manifests.find((m) => m.alias === PNFT_DEFAULT_REDIRECT_FORM_ALIAS) as
    | { type: string; weight?: number }
    | undefined;

  it('registers the default form as a pnftRedirectForm', () => {
    expect(form).toBeDefined();
    expect(form?.type).toBe(PNFT_REDIRECT_FORM_EXTENSION_TYPE);
    expect(form?.type).toBe('pnftRedirectForm');
  });

  it('gives the default form a weight below zero, so an unweighted custom form wins', () => {
    // umb-extension-slot (single) picks the highest weight; a manifest without a weight counts as 0.
    expect(form?.weight).toBeLessThan(0);
  });
});
