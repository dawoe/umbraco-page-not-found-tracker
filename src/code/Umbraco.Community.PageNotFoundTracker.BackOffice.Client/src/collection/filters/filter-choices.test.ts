import { describe, expect, it } from 'vitest';
import { buildFilterChoices, fromChoiceValue, toChoiceValue } from './filter-choices';
import { PNFT_EMPTY } from '../types';

const labels = { all: 'All domains', empty: 'No domain' };

describe('buildFilterChoices', () => {
  it('buildFilterChoices_AllFirstThenEmptyThenValues', () => {
    const choices = buildFilterChoices(['localhost:44402/sitea', 'localhost:44402/siteb'], true, labels);

    expect(choices.map((choice) => choice.label)).toStrictEqual([
      'All domains',
      'No domain',
      'localhost:44402/sitea',
      'localhost:44402/siteb',
    ]);
  });

  it('buildFilterChoices_NoEmptyChoiceWhenTheDataHasNoEmptyValue', () => {
    const choices = buildFilterChoices(['en-US'], false, labels);

    expect(choices.map((choice) => choice.label)).toStrictEqual(['All domains', 'en-US']);
  });

  it('buildFilterChoices_EveryChoiceValueRoundTrips', () => {
    const values = ['all', 'empty', 'v:x', '', 'pnft-empty'];
    const choices = buildFilterChoices(values, true, labels);

    expect(choices.map((choice) => fromChoiceValue(choice.value))).toStrictEqual([undefined, PNFT_EMPTY, ...values]);
  });

  it('buildFilterChoices_ChoiceValuesAreUnique', () => {
    const choices = buildFilterChoices(['all', 'empty'], true, labels);

    expect(new Set(choices.map((choice) => choice.value)).size).toBe(choices.length);
  });
});

describe('toChoiceValue / fromChoiceValue', () => {
  it('toChoiceValue_UndefinedAndEmptyStringAreAll', () => {
    expect(fromChoiceValue(toChoiceValue(undefined))).toBeUndefined();
    expect(fromChoiceValue(toChoiceValue(''))).toBeUndefined();
  });

  it('toChoiceValue_MarkerAndValuesRoundTrip', () => {
    expect(fromChoiceValue(toChoiceValue(PNFT_EMPTY))).toBe(PNFT_EMPTY);
    expect(fromChoiceValue(toChoiceValue('nl-NL'))).toBe('nl-NL');
  });

  it('fromChoiceValue_EmptyIsTheMarker_ARealValueNamedEmptyOrAllIsAValue', () => {
    expect(fromChoiceValue('empty')).toBe(PNFT_EMPTY);
    expect(fromChoiceValue('v:empty')).toBe('empty');
    expect(fromChoiceValue('v:all')).toBe('all');
    expect(fromChoiceValue('v:empty')).not.toBe(PNFT_EMPTY);
    expect(toChoiceValue('empty')).toBe('v:empty');
    expect(toChoiceValue('all')).toBe('v:all');
    expect(toChoiceValue(PNFT_EMPTY)).toBe('empty');
  });

  it('fromChoiceValue_UnknownTextIsAll', () => {
    expect(fromChoiceValue('nonsense')).toBeUndefined();
    expect(fromChoiceValue(undefined)).toBeUndefined();
  });
});
