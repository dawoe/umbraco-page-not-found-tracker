import { describe, expect, it } from 'vitest';
import { PNFT_TRACKED_URL_COLLECTION_ALIAS } from '../collection/constants.js';
import en from '../localization/en.js';
import { PNFT_EXPORT_ACTION_ALIAS } from './constants.js';
import { manifests } from './manifests.js';

type AnyManifest = {
  type: string;
  alias: string;
  kind?: string;
  meta?: { label?: string };
  conditions?: Array<{ alias: string; match?: string }>;
};

describe('export manifests', () => {
  const action = manifests.find((manifest) => manifest.alias === PNFT_EXPORT_ACTION_ALIAS) as AnyManifest | undefined;

  it('the alias is the documented one', () => {
    expect(PNFT_EXPORT_ACTION_ALIAS).toBe('Pnft.CollectionAction.TrackedUrl.Export');
  });

  it('the export is a button collection action', () => {
    expect(action?.type).toBe('collectionAction');
    expect(action?.kind).toBe('button');
  });

  it('the export only shows on the tracked URL collection', () => {
    expect(action?.conditions).toStrictEqual([{ alias: 'Umb.Condition.CollectionAlias', match: PNFT_TRACKED_URL_COLLECTION_ALIAS }]);
  });

  it('the label key exists in the English texts', () => {
    expect(action?.meta?.label).toBe('#pageNotFoundTracker_exportAction');
    expect(en.pageNotFoundTracker.exportAction).toBe('Export');
  });
});
