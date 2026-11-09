import { describe, expect, it } from 'vitest';
import { PNFT_BLOCK_SORT_MARKS } from '../../../data-marks.js';
import {
  PNFT_BLOCK_RULE_COLUMNS,
  PNFT_BLOCK_RULE_DEFAULT_SORT,
  blockRuleColumnAliasOf,
  blockRuleSortColumnOf,
} from './block-rule-table.columns.js';

describe('PNFT_BLOCK_RULE_COLUMNS', () => {
  it('columns_AreTheFiveSpecColumnsInOrder', () => {
    expect(PNFT_BLOCK_RULE_COLUMNS.map((column) => column.alias)).toStrictEqual([
      'pattern',
      'statusCode',
      'domain',
      'culture',
      'created',
    ]);
  });

  it('columns_EveryColumnSortsAndHasItsSortMarkAndALabel', () => {
    for (const column of PNFT_BLOCK_RULE_COLUMNS) {
      expect(blockRuleSortColumnOf(column.alias)).toBeDefined();
      expect(column.sortMark).toBe(PNFT_BLOCK_SORT_MARKS[column.alias]);
      expect(column.labelKey.startsWith('pageNotFoundTracker_')).toBe(true);
    }
  });

  it('columns_DefaultSortIsCreatedDescending', () => {
    expect(PNFT_BLOCK_RULE_DEFAULT_SORT).toStrictEqual({ column: 'created', orderBy: 'Created', descending: true });
    expect(blockRuleColumnAliasOf('Created')).toBe('created');
  });
});

describe('blockRuleSortColumnOf / blockRuleColumnAliasOf', () => {
  it('blockRuleSortColumnOf_MapsAliasesToApiColumns', () => {
    expect(blockRuleSortColumnOf('pattern')).toBe('Pattern');
    expect(blockRuleSortColumnOf('statusCode')).toBe('StatusCode');
    expect(blockRuleSortColumnOf('domain')).toBe('Domain');
    expect(blockRuleSortColumnOf('culture')).toBe('Culture');
    expect(blockRuleSortColumnOf('created')).toBe('Created');
    expect(blockRuleSortColumnOf('entityActions')).toBeUndefined();
  });

  it('blockRuleColumnAliasOf_IsTheInverse', () => {
    for (const column of PNFT_BLOCK_RULE_COLUMNS) {
      expect(blockRuleColumnAliasOf(blockRuleSortColumnOf(column.alias))).toBe(column.alias);
    }

    expect(blockRuleColumnAliasOf(undefined)).toBeUndefined();
  });
});
