import { PNFT_EMPTY, type PnftFilterValue } from '../types.js';

// Pure mapping between the domain/culture filter values and the string option values of a `uui-select`.
// "All" and the "empty" marker get reserved option values; every real value is prefixed, so no domain or
// culture text can be mistaken for either of them.

const ALL = 'all';
const EMPTY = 'empty';
const VALUE_PREFIX = 'v:';

/** One option of a domain or culture filter select. */
export interface PnftFilterChoice {
  /** The option value (`all`, `empty` or `v:<value>`). */
  value: string;
  /** The text shown. */
  label: string;
}

/** The localized texts of the "all" and "empty" choices. */
export interface PnftFilterChoiceLabels {
  all: string;
  empty: string;
}

/**
 * Builds the options of a filter: "all" first, then "empty" when the data has URLs without a value,
 * then each value as the API returned it.
 */
export function buildFilterChoices(
  values: ReadonlyArray<string>,
  hasEmpty: boolean,
  labels: PnftFilterChoiceLabels,
): Array<PnftFilterChoice> {
  const choices: Array<PnftFilterChoice> = [{ value: ALL, label: labels.all }];

  if (hasEmpty) {
    choices.push({ value: EMPTY, label: labels.empty });
  }

  for (const value of values) {
    choices.push({ value: VALUE_PREFIX + value, label: value });
  }

  return choices;
}

/** The option value of a filter value (undefined or an empty string is "all"). */
export function toChoiceValue(value: PnftFilterValue | undefined): string {
  if (value === PNFT_EMPTY) {
    return EMPTY;
  }

  return value ? VALUE_PREFIX + value : ALL;
}

/** The filter value of an option value: undefined for "all" (or anything unknown), the marker for "empty". */
export function fromChoiceValue(choice: string | undefined): PnftFilterValue | undefined {
  if (choice === EMPTY) {
    return PNFT_EMPTY;
  }

  if (choice?.startsWith(VALUE_PREFIX)) {
    return choice.slice(VALUE_PREFIX.length);
  }

  return undefined;
}
