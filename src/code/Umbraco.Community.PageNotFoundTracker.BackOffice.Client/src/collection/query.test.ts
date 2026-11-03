import { describe, expect, it } from 'vitest';
import { buildListQuery } from './query';
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
