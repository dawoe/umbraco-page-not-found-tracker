import { describe, expect, it } from 'vitest';
import { buildListQuery, fromCollectionFilter } from './query';
import { PNFT_EMPTY } from './types';

describe('buildListQuery', () => {
  it('buildListQuery_DefaultsToHitsDescendingSkip0Take20', () => {
    expect(buildListQuery({})).toStrictEqual({
      skip: 0,
      take: 20,
      orderBy: 'Hits',
      direction: 'Descending',
    });
  });

  it('buildListQuery_PassesPagingAndOrder', () => {
    expect(buildListQuery({ skip: 40, take: 50, orderBy: 'LastSeen', direction: 'Ascending' })).toStrictEqual({
      skip: 40,
      take: 50,
      orderBy: 'LastSeen',
      direction: 'Ascending',
    });
  });

  it('buildListQuery_EmptySentinelBecomesNoDomainFlag', () => {
    const query = buildListQuery({ domain: PNFT_EMPTY });

    expect(query.noDomain).toBe(true);
    expect(query).not.toHaveProperty('domain');
    expect(query).not.toHaveProperty('noCulture');
  });

  it('buildListQuery_EmptySentinelBecomesNoCultureFlag', () => {
    const query = buildListQuery({ culture: PNFT_EMPTY });

    expect(query.noCulture).toBe(true);
    expect(query).not.toHaveProperty('culture');
    expect(query).not.toHaveProperty('noDomain');
  });

  it('buildListQuery_DomainAndCultureValuesAreSentAsValues', () => {
    const query = buildListQuery({ domain: 'www.sitea.example:443/sitea', culture: 'en-US' });

    expect(query.domain).toBe('www.sitea.example:443/sitea');
    expect(query.culture).toBe('en-US');
    expect(query).not.toHaveProperty('noDomain');
    expect(query).not.toHaveProperty('noCulture');
  });

  it('buildListQuery_UndefinedOrEmptyDomainAndCultureMeanAll', () => {
    for (const value of [undefined, '']) {
      const query = buildListQuery({ domain: value, culture: value });

      expect(query).not.toHaveProperty('domain');
      expect(query).not.toHaveProperty('noDomain');
      expect(query).not.toHaveProperty('culture');
      expect(query).not.toHaveProperty('noCulture');
    }
  });

  it('buildListQuery_OmitsBlankSearch', () => {
    for (const search of [undefined, '', '   ', '\t']) {
      expect(buildListQuery({ search })).not.toHaveProperty('search');
    }

    expect(buildListQuery({ search: 'wp-admin' }).search).toBe('wp-admin');
  });

  it('buildListQuery_ADomainTextEqualToTheMarkerWordIsSentAsAValue', () => {
    expect(typeof PNFT_EMPTY).not.toBe('string');

    for (const marker of ['PNFT_EMPTY', 'pnft-empty', 'empty', String(PNFT_EMPTY), PNFT_EMPTY.description ?? '']) {
      if (marker === '') {
        continue;
      }

      const query = buildListQuery({ domain: marker, culture: marker });

      expect(query.domain).toBe(marker);
      expect(query.culture).toBe(marker);
      expect(query).not.toHaveProperty('noDomain');
      expect(query).not.toHaveProperty('noCulture');
    }
  });
});

describe('fromCollectionFilter', () => {
  it('fromCollectionFilter_MapsTheFrameworkTextFilterToSearch', () => {
    expect(fromCollectionFilter({ filter: 'wp-admin' }).search).toBe('wp-admin');
  });

  it('fromCollectionFilter_KeepsANonBlankSearchUntrimmed', () => {
    expect(fromCollectionFilter({ filter: ' wp ' }).search).toBe(' wp ');
  });

  it('fromCollectionFilter_MapsOrderDirectionToDirection', () => {
    const filter = fromCollectionFilter({ skip: 20, take: 50, orderBy: 'Url', orderDirection: 'Ascending' });

    expect(filter).toStrictEqual({ skip: 20, take: 50, orderBy: 'Url', direction: 'Ascending' });
  });

  it('fromCollectionFilter_PassesDomainAndCultureIncludingTheEmptyMarker', () => {
    const filter = fromCollectionFilter({ domain: PNFT_EMPTY, culture: 'nl-NL' });

    expect(filter.domain).toBe(PNFT_EMPTY);
    expect(filter.culture).toBe('nl-NL');
  });

  it('fromCollectionFilter_IgnoresKeysTheApiDoesNotKnow', () => {
    const filter = fromCollectionFilter({ pageSize: 20, take: 20 } as never);

    expect(filter).toStrictEqual({ take: 20 });
  });

  it('fromCollectionFilter_ThenBuildListQuery_DefaultsToHitsDescending', () => {
    expect(buildListQuery(fromCollectionFilter({}))).toStrictEqual({
      skip: 0,
      take: 20,
      orderBy: 'Hits',
      direction: 'Descending',
    });
  });
});
