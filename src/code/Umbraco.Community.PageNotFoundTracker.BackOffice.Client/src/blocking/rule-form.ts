import type { PnftCreateRuleRequest } from './block-rule.service.js';

// Pure logic of the rule modal. No backoffice runtime imports, so it runs in Vitest.

/** The status codes the rule modal offers. */
export const PNFT_BLOCK_STATUS_CODES: ReadonlyArray<number> = [403, 404, 410];

/** The status code the rule modal preselects. */
export const PNFT_BLOCK_DEFAULT_STATUS_CODE = 410;

/** The values of the rule modal's fields. An empty domain or culture means all. */
export interface PnftRuleFormValues {
  pattern: string;
  domain: string;
  culture: string;
  statusCode: number;
}

/** The part of a tracked URL row the "Block" action preselects from. */
export interface PnftRuleSource {
  relativePath: string;
  domain: string;
  culture: string;
}

/**
 * The modal's starting values. From a tracked URL row: its relative path (the path below the domain root, which is what
 * a pattern matches; a query string is dropped, since patterns match the path only), its domain and its culture. Without
 * a row ("Add rule"): an empty pattern and all domains and cultures. The status code is always 410.
 */
export function buildRuleDefaults(trackedUrl: PnftRuleSource | undefined): PnftRuleFormValues {
  if (!trackedUrl) {
    return { pattern: '', domain: '', culture: '', statusCode: PNFT_BLOCK_DEFAULT_STATUS_CODE };
  }

  const queryStart = trackedUrl.relativePath.indexOf('?');

  return {
    pattern: queryStart >= 0 ? trackedUrl.relativePath.slice(0, queryStart) : trackedUrl.relativePath,
    domain: trackedUrl.domain,
    culture: trackedUrl.culture,
    statusCode: PNFT_BLOCK_DEFAULT_STATUS_CODE,
  };
}

/**
 * The choices of a scope select: '' (all) first, then the configured values, then the current value when it is not
 * configured (a row's domain that was removed from Umbraco since), so the preselection always exists.
 */
export function buildScopeChoices(values: ReadonlyArray<string>, current: string): Array<string> {
  const choices = [''];

  for (const value of [...values, current]) {
    if (value !== '' && !choices.includes(value)) {
      choices.push(value);
    }
  }

  return choices;
}

/**
 * The choices of the status select: the offered codes, plus the current code when it is not one of them (a rule created
 * through the API with another 4xx code), so an edit never silently changes it.
 */
export function buildStatusChoices(current: number): Array<number> {
  return PNFT_BLOCK_STATUS_CODES.includes(current) ? [...PNFT_BLOCK_STATUS_CODES] : [...PNFT_BLOCK_STATUS_CODES, current];
}

/** Whether a pattern was entered (the server validates the rest). */
export function isPatternGiven(pattern: string): boolean {
  return pattern.trim() !== '';
}

/** The create request of the modal's values (the pattern trimmed). */
export function toCreateRuleRequest(values: PnftRuleFormValues): PnftCreateRuleRequest {
  return { pattern: values.pattern.trim(), domain: values.domain, culture: values.culture, statusCode: values.statusCode };
}
