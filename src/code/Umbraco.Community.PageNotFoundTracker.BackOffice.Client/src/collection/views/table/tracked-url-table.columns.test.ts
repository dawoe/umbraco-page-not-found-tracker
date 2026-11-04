import { describe, expect, it } from 'vitest';
import {
  PNFT_COLUMNS,
  columnAliasOf,
  formatLocalDateTime,
  sortColumnOf,
} from './tracked-url-table.columns';
import { PNFT_SORT_MARKS } from '../../data-marks';

describe('PNFT_COLUMNS', () => {
  it('columns_AreInSpecOrder', () => {
    expect(PNFT_COLUMNS.map((column) => column.alias)).toStrictEqual([
      'url',
      'domain',
      'culture',
      'type',
      'firstSeen',
      'lastSeen',
      'hits',
    ]);
  });

  it('columns_EveryColumnSortsAndHasItsSortMark', () => {
    for (const column of PNFT_COLUMNS) {
      expect(sortColumnOf(column.alias)).toBeDefined();
      expect(column.sortMark).toBe(PNFT_SORT_MARKS[column.alias]);
      expect(column.labelKey.startsWith('pageNotFoundTracker_')).toBe(true);
    }
  });
});

describe('sortColumnOf / columnAliasOf', () => {
  it('sortColumnOf_MapsAliasesToApiColumns', () => {
    expect(sortColumnOf('url')).toBe('Url');
    expect(sortColumnOf('type')).toBe('IsFile');
    expect(sortColumnOf('firstSeen')).toBe('FirstSeen');
    expect(sortColumnOf('hits')).toBe('Hits');
    expect(sortColumnOf('entityActions')).toBeUndefined();
    expect(sortColumnOf('')).toBeUndefined();
  });

  it('columnAliasOf_IsTheInverse', () => {
    for (const column of PNFT_COLUMNS) {
      expect(columnAliasOf(sortColumnOf(column.alias))).toBe(column.alias);
    }

    expect(columnAliasOf(undefined)).toBeUndefined();
  });
});

describe('formatLocalDateTime', () => {
  it('formatLocalDateTime_UsesToLocaleStringOfTheUtcInstant', () => {
    const iso = '2026-11-03T08:30:00Z';

    expect(formatLocalDateTime(iso)).toBe(new Date(iso).toLocaleString());
  });

  it('formatLocalDateTime_InvalidOrMissingGivesEmptyText', () => {
    expect(formatLocalDateTime('not a date')).toBe('');
    expect(formatLocalDateTime('')).toBe('');
    expect(formatLocalDateTime(undefined)).toBe('');
  });
});
