import { describe, expect, it } from 'vitest';
import {
  PNFT_DEFAULT_PAGE_SIZE,
  PNFT_PAGE_SIZES,
  classifyError,
  isFilterActive,
  parsePageSize,
  pickEmptyState,
  withPageReset,
} from './state';
import { PNFT_EMPTY } from './types';
import { fromChoiceValue } from './filters/filter-choices';

describe('classifyError', () => {
  it('classifyError_503IsUnavailable', () => {
    expect(classifyError({ name: 'UmbApiError', status: 503 })).toBe('unavailable');
  });

  it('classifyError_OtherStatusesAndNetworkErrorsAreErrors', () => {
    for (const status of [0, 400, 403, 500]) {
      expect(classifyError({ name: 'UmbApiError', status })).toBe('error');
    }

    expect(classifyError(new Error('boom'))).toBe('error');
  });

  it('classifyError_CancellationIsNotAnError', () => {
    expect(classifyError({ name: 'UmbCancelError' })).toBeUndefined();
  });

  it('classifyError_NoErrorIsUndefined', () => {
    expect(classifyError(undefined)).toBeUndefined();
  });
});

describe('isFilterActive', () => {
  it('isFilterActive_DefaultFilterIsNotActive', () => {
    expect(isFilterActive({ skip: 40, take: 20, orderBy: 'Url', orderDirection: 'Ascending' })).toBe(false);
  });

  it('isFilterActive_BlankSearchIsNotActive', () => {
    expect(isFilterActive({ filter: '   ' })).toBe(false);
    expect(isFilterActive({ filter: '' })).toBe(false);
  });

  it('isFilterActive_SearchDomainOrCultureMakeItActive', () => {
    expect(isFilterActive({ filter: 'wp' })).toBe(true);
    expect(isFilterActive({ domain: 'localhost:44402/sitea' })).toBe(true);
    expect(isFilterActive({ domain: PNFT_EMPTY })).toBe(true);
    expect(isFilterActive({ culture: 'en-US' })).toBe(true);
    expect(isFilterActive({ culture: PNFT_EMPTY })).toBe(true);
  });

  it('isFilterActive_AValueNamedLikeAChoiceWordIsActive', () => {
    expect(isFilterActive({ domain: 'empty' })).toBe(true);
    expect(isFilterActive({ culture: 'all' })).toBe(true);
  });

  it('isFilterActive_EmptyStringDomainOrCultureMeansAll', () => {
    expect(isFilterActive({ domain: '', culture: '' })).toBe(false);
  });
});

describe('filter state serialisation', () => {
  // Umbraco's UmbDeepState only emits when JSON.stringify of the new value differs, so every filter choice
  // must serialise differently or the change is invisible to the observers.
  const serialise = (choice: string) =>
    JSON.stringify({ skip: 0, take: 20, domain: fromChoiceValue(choice), culture: fromChoiceValue(choice) });

  it('filterState_EachChoiceSerialisesDifferently', () => {
    const all = serialise('all');
    const empty = serialise('empty');
    const value = serialise('v:empty');

    expect(new Set([all, empty, value]).size).toBe(3);
  });

  it('filterState_AllToNoDomainToAll_ChangesEachStep', () => {
    const steps = ['all', 'empty', 'all'].map(serialise);

    expect(steps[1]).not.toBe(steps[0]);
    expect(steps[2]).not.toBe(steps[1]);
  });
});

describe('pickEmptyState', () => {
  it('pickEmptyState_ErrorWins', () => {
    expect(pickEmptyState('error', true)).toBe('error');
    expect(pickEmptyState('unavailable', false)).toBe('unavailable');
  });

  it('pickEmptyState_FilteredOrNot', () => {
    expect(pickEmptyState(undefined, true)).toBe('empty-filtered');
    expect(pickEmptyState(undefined, false)).toBe('empty');
  });
});

describe('withPageReset', () => {
  it('withPageReset_ResetsSkipWhenTheChangeDoesNotSetIt', () => {
    expect(withPageReset({ filter: 'wp' })).toStrictEqual({ filter: 'wp', skip: 0 });
    expect(withPageReset({ orderBy: 'Url', orderDirection: 'Ascending' })).toStrictEqual({
      orderBy: 'Url',
      orderDirection: 'Ascending',
      skip: 0,
    });
  });

  it('withPageReset_KeepsAnExplicitSkip', () => {
    expect(withPageReset({ skip: 40 })).toStrictEqual({ skip: 40 });
    expect(withPageReset({ filter: 'wp', skip: 20 })).toStrictEqual({ filter: 'wp', skip: 20 });
  });
});

describe('page size', () => {
  it('pageSizes_Are20_50_100_Default20', () => {
    expect(PNFT_PAGE_SIZES).toStrictEqual([20, 50, 100]);
    expect(PNFT_DEFAULT_PAGE_SIZE).toBe(20);
  });

  it('parsePageSize_AcceptsOnlyTheAllowedSizes', () => {
    expect(parsePageSize('50')).toBe(50);
    expect(parsePageSize(100)).toBe(100);
    expect(parsePageSize('30')).toBe(20);
    expect(parsePageSize('abc')).toBe(20);
    expect(parsePageSize(undefined)).toBe(20);
  });
});
