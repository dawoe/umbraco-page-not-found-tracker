import { describe, expect, it } from 'vitest';
import {
  PNFT_BLOCK_DEFAULT_STATUS_CODE,
  PNFT_BLOCK_STATUS_CODES,
  buildRuleDefaults,
  buildScopeChoices,
  buildStatusChoices,
  isPatternGiven,
  toCreateRuleRequest,
} from './rule-form.js';

const trackedUrl = {
  url: '/SiteA/Old-Page',
  relativePath: '/old-page',
  domain: 'localhost:44402/sitea',
  culture: 'en-US',
  isFile: false,
};

describe('buildRuleDefaults', () => {
  it('buildRuleDefaults_PreselectsRelativePathDomainAndCulture_Status410', () => {
    expect(buildRuleDefaults(trackedUrl)).toStrictEqual({
      pattern: '/old-page',
      domain: 'localhost:44402/sitea',
      culture: 'en-US',
      statusCode: 410,
    });
  });

  it('buildRuleDefaults_FileRowUsesTheFilePath', () => {
    const fileRow = { ...trackedUrl, url: '/Missing.CSS', relativePath: '/missing.css', isFile: true };

    expect(buildRuleDefaults(fileRow).pattern).toBe('/missing.css');
  });

  it('buildRuleDefaults_DropsAQueryString', () => {
    expect(buildRuleDefaults({ ...trackedUrl, relativePath: '/search?q=x' }).pattern).toBe('/search');
  });

  it('buildRuleDefaults_KeepsEmptyDomainAndCultureAsAll', () => {
    const defaults = buildRuleDefaults({ ...trackedUrl, domain: '', culture: '' });

    expect(defaults.domain).toBe('');
    expect(defaults.culture).toBe('');
  });

  it('buildRuleDefaults_WithoutARowIsEmptyAndAll', () => {
    expect(buildRuleDefaults(undefined)).toStrictEqual({ pattern: '', domain: '', culture: '', statusCode: 410 });
  });
});

describe('status codes', () => {
  it('statusCodes_Are403_404_410_Default410', () => {
    expect(PNFT_BLOCK_STATUS_CODES).toStrictEqual([403, 404, 410]);
    expect(PNFT_BLOCK_DEFAULT_STATUS_CODE).toBe(410);
  });
});

describe('buildScopeChoices', () => {
  it('buildScopeChoices_AllFirstThenTheConfiguredValues', () => {
    expect(buildScopeChoices(['a', 'b'], '')).toStrictEqual(['', 'a', 'b']);
  });

  it('buildScopeChoices_AddsTheCurrentValueWhenMissing', () => {
    expect(buildScopeChoices(['a'], 'gone')).toStrictEqual(['', 'a', 'gone']);
    expect(buildScopeChoices(['a'], 'a')).toStrictEqual(['', 'a']);
  });

  it('buildScopeChoices_IgnoresBlankAndDuplicateValues', () => {
    expect(buildScopeChoices(['a', '', 'a'], '')).toStrictEqual(['', 'a']);
  });
});

describe('isPatternGiven / toCreateRuleRequest', () => {
  it('isPatternGiven_BlankIsNotGiven', () => {
    expect(isPatternGiven('')).toBe(false);
    expect(isPatternGiven('   ')).toBe(false);
    expect(isPatternGiven('/x')).toBe(true);
  });

  it('toCreateRuleRequest_TrimsThePatternAndKeepsTheScope', () => {
    expect(toCreateRuleRequest({ pattern: ' /wp-admin/* ', domain: '', culture: 'en-US', statusCode: 403 })).toStrictEqual({
      pattern: '/wp-admin/*',
      domain: '',
      culture: 'en-US',
      statusCode: 403,
    });
  });
});

describe('buildStatusChoices', () => {
  it('buildStatusChoices_KeepsTheThreeCodesForAKnownCode', () => {
    expect(buildStatusChoices(404)).toStrictEqual([403, 404, 410]);
  });

  it('buildStatusChoices_AddsAnUnknownCurrentCode', () => {
    expect(buildStatusChoices(451)).toStrictEqual([403, 404, 410, 451]);
  });
});
