import { describe, expect, it } from 'vitest';
import type { BlockRuleResponseModel } from '../../../api/index.js';
import { mapBlockRule } from './block-rule.mapping.js';

const item: BlockRuleResponseModel = {
  id: 4,
  pattern: '/WP-Admin/*',
  statusCode: 410,
  domain: 'localhost:44402/sitea',
  culture: 'en-US',
  created: '2026-11-04T10:00:00Z',
};

describe('mapBlockRule', () => {
  it('maps every field, the entity type and the unique', () => {
    expect(mapBlockRule(item)).toEqual({
      entityType: 'page-not-found-tracker-block-rule',
      unique: '4',
      id: 4,
      pattern: '/WP-Admin/*',
      statusCode: 410,
      domain: 'localhost:44402/sitea',
      culture: 'en-US',
      created: '2026-11-04T10:00:00Z',
    });
  });

  it('keeps an empty domain and culture empty (all domains, all cultures)', () => {
    const row = mapBlockRule({ ...item, domain: '', culture: '' });

    expect(row.domain).toBe('');
    expect(row.culture).toBe('');
  });
});
