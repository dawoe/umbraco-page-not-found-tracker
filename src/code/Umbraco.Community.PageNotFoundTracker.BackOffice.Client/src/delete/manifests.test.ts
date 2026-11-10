import { describe, expect, it } from 'vitest';
import { PNFT_TRACKED_URL_ENTITY_TYPE } from '../entity.js';
import { PNFT_BLOCK_ACTION_ALIAS } from '../blocking/constants.js';
import { manifests as blockingManifests } from '../blocking/manifests.js';
import { PNFT_TRACKED_URL_COLLECTION_ALIAS } from '../collection/constants.js';
import {
  PNFT_DELETE_ACTION_ALIAS,
  PNFT_DELETE_ALL_ACTION_ALIAS,
  PNFT_DELETE_ALL_MODAL_ALIAS,
  PNFT_DELETE_SELECTED_BULK_ACTION_ALIAS,
} from './constants.js';
import { manifests } from './manifests.js';

type AnyManifest = {
  type: string;
  alias: string;
  weight?: number;
  forEntityTypes?: Array<string>;
  conditions?: Array<unknown>;
  kind?: string;
  meta?: { icon?: string; label?: string };
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

  it('the bulk action is registered for tracked URL rows only', () => {
    const bulk = byAlias(PNFT_DELETE_SELECTED_BULK_ACTION_ALIAS);

    expect(PNFT_DELETE_SELECTED_BULK_ACTION_ALIAS).toBe('Pnft.EntityBulkAction.TrackedUrl.Delete');
    expect(bulk?.type).toBe('entityBulkAction');
    expect(bulk?.kind).toBe('default');
    expect(bulk?.weight).toBe(0);
    expect(bulk?.forEntityTypes).toStrictEqual([PNFT_TRACKED_URL_ENTITY_TYPE]);
    expect(bulk?.meta).toStrictEqual({ icon: 'icon-trash', label: '#pageNotFoundTracker_deleteSelected' });
    // 17.8 does not filter bulk actions by forEntityTypes: the collection alias condition keeps the action (and with it
    // row selection) out of every other collection, the block rule list included.
    expect(bulk?.conditions).toStrictEqual([{ alias: 'Umb.Condition.CollectionAlias', match: PNFT_TRACKED_URL_COLLECTION_ALIAS }]);
  });

  it('the delete all action is a collection action for the tracked URL collection only', () => {
    const all = byAlias(PNFT_DELETE_ALL_ACTION_ALIAS);

    expect(PNFT_DELETE_ALL_ACTION_ALIAS).toBe('Pnft.CollectionAction.TrackedUrl.DeleteAll');
    expect(all?.type).toBe('collectionAction');
    expect(all?.kind).toBe('button');
    expect(all?.meta).toStrictEqual({ label: '#pageNotFoundTracker_deleteAll', additionalOptions: false });
    expect(all?.conditions).toStrictEqual([{ alias: 'Umb.Condition.CollectionAlias', match: PNFT_TRACKED_URL_COLLECTION_ALIAS }]);
  });

  it('the delete all modal is registered', () => {
    expect(PNFT_DELETE_ALL_MODAL_ALIAS).toBe('Pnft.Modal.DeleteAll');
    expect(byAlias(PNFT_DELETE_ALL_MODAL_ALIAS)?.type).toBe('modal');
  });
});
