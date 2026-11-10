import { describe, expect, it } from 'vitest';
import { PNFT_TRACKED_URL_ENTITY_TYPE } from '../entity.js';
import { PNFT_BLOCK_ACTION_ALIAS } from '../blocking/constants.js';
import { manifests as blockingManifests } from '../blocking/manifests.js';
import { PNFT_DELETE_ACTION_ALIAS } from './constants.js';
import { manifests } from './manifests.js';

type AnyManifest = {
  type: string;
  alias: string;
  weight?: number;
  forEntityTypes?: Array<string>;
  conditions?: Array<unknown>;
};

const byAlias = (alias: string) => manifests.find((manifest) => manifest.alias === alias) as AnyManifest | undefined;

describe('delete manifests', () => {
  it('the delete action is an entity action for tracked URL rows', () => {
    const del = byAlias(PNFT_DELETE_ACTION_ALIAS);

    expect(PNFT_DELETE_ACTION_ALIAS).toBe('Pnft.EntityAction.TrackedUrl.Delete');
    expect(del?.type).toBe('entityAction');
    expect(del?.forEntityTypes).toStrictEqual([PNFT_TRACKED_URL_ENTITY_TYPE]);
  });

  it('the delete action has weight -20, below block (-10)', () => {
    expect(byAlias(PNFT_DELETE_ACTION_ALIAS)?.weight).toBe(-20);
    const block = blockingManifests.find((manifest) => manifest.alias === PNFT_BLOCK_ACTION_ALIAS) as AnyManifest | undefined;

    expect(block?.weight).toBe(-10);
  });

  it('the delete action has no condition so it shows on every row', () => {
    expect(byAlias(PNFT_DELETE_ACTION_ALIAS)?.conditions ?? []).toHaveLength(0);
  });
});
