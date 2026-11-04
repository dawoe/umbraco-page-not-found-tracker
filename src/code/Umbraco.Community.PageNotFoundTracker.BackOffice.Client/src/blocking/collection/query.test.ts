import { describe, expect, it } from 'vitest';
import { PNFT_EMPTY } from '../../collection/types.js';
import { buildBlockRuleListQuery, fromBlockRuleCollectionFilter } from './query.js';

describe('buildBlockRuleListQuery', () => {
  it('buildBlockRuleListQuery_DefaultsToCreatedDescendingSkip0Take20', () => {
    expect(buildBlockRuleListQuery({})).toStrictEqual({
      skip: 0,
      take: 20,
      orderBy: 'Created',
      direction: 'Descending',
    });
  });

  it('buildBlockRuleListQuery_PassesPagingAndOrder', () => {
    expect(buildBlockRuleListQuery({ skip: 20, take: 50, orderBy: 'StatusCode', direction: 'Ascending' })).toStrictEqual({
      skip: 20,
      take: 50,
      orderBy: 'StatusCode',
      direction: 'Ascending',
    });
  });

  it('buildBlockRuleListQuery_EmptySentinelBecomesNoDomainAndNoCulture', () => {
    const query = buildBlockRuleListQuery({ domain: PNFT_EMPTY, culture: PNFT_EMPTY });

    expect(query.noDomain).toBe(true);
    expect(query.noCulture).toBe(true);
    expect(query).not.toHaveProperty('domain');
    expect(query).not.toHaveProperty('culture');
  });

  it('buildBlockRuleListQuery_ValuesAreSentAsValues_UndefinedMeansAll', () => {
    expect(buildBlockRuleListQuery({ domain: 'localhost:44402/sitea', culture: 'nl-NL' })).toMatchObject({
      domain: 'localhost:44402/sitea',
      culture: 'nl-NL',
    });

    const all = buildBlockRuleListQuery({ domain: undefined, culture: '' });
    expect(all).not.toHaveProperty('domain');
    expect(all).not.toHaveProperty('noDomain');
    expect(all).not.toHaveProperty('culture');
    expect(all).not.toHaveProperty('noCulture');
  });

  it('buildBlockRuleListQuery_OmitsBlankSearch', () => {
    expect(buildBlockRuleListQuery({ search: '  ' })).not.toHaveProperty('search');
    expect(buildBlockRuleListQuery({ search: 'wp-' }).search).toBe('wp-');
  });
});

describe('fromBlockRuleCollectionFilter', () => {
  it('fromBlockRuleCollectionFilter_MapsFilterToSearchAndOrderDirectionToDirection', () => {
    expect(
      fromBlockRuleCollectionFilter({ skip: 0, take: 20, filter: 'wp', orderBy: 'Pattern', orderDirection: 'Ascending' }),
    ).toStrictEqual({ skip: 0, take: 20, search: 'wp', orderBy: 'Pattern', direction: 'Ascending' });
  });

  it('fromBlockRuleCollectionFilter_PassesDomainAndCultureIncludingTheMarker', () => {
    expect(fromBlockRuleCollectionFilter({ domain: PNFT_EMPTY, culture: 'en-US' })).toStrictEqual({
      domain: PNFT_EMPTY,
      culture: 'en-US',
    });
  });

  it('fromBlockRuleCollectionFilter_ThenBuild_DefaultsToCreatedDescending', () => {
    expect(buildBlockRuleListQuery(fromBlockRuleCollectionFilter({}))).toStrictEqual({
      skip: 0,
      take: 20,
      orderBy: 'Created',
      direction: 'Descending',
    });
  });
});
