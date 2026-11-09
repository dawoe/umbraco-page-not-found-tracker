import { describe, expect, it } from 'vitest';
import { PNFT_BLOCK_RULE_ENTITY_TYPE } from './blocking/constants.js';
import { PNFT_TRACKED_URL_ENTITY_TYPE } from './entity.js';

// The entity type names are public: implementers write them as strings in `forEntityTypes` (see docs/extending/dashboard-actions.md).
describe('entity types', () => {
  it('are the documented strings', () => {
    expect(PNFT_TRACKED_URL_ENTITY_TYPE).toBe('page-not-found-tracker-url');
    expect(PNFT_BLOCK_RULE_ENTITY_TYPE).toBe('page-not-found-tracker-block-rule');
  });
});
