import { describe, expect, it } from 'vitest';
import { PNFT_TRACKED_URL_ENTITY_TYPE } from '../entity.js';
import {
  PNFT_ADD_BLOCK_RULE_ACTION_ALIAS,
  PNFT_BLOCK_ACTION_ALIAS,
  PNFT_BLOCK_RULE_COLLECTION_ALIAS,
  PNFT_BLOCK_RULE_ENTITY_TYPE,
  PNFT_BLOCK_RULES_DASHBOARD_ALIAS,
  PNFT_UNBLOCK_ACTION_ALIAS,
} from './constants.js';
import { manifests } from './manifests.js';

type AnyManifest = {
  type: string;
  alias: string;
  kind?: string;
  weight?: number;
  forEntityTypes?: Array<string>;
  meta?: Record<string, unknown>;
  conditions?: Array<{ alias: string; match?: string }>;
};

const byAlias = (alias: string) => manifests.find((manifest) => manifest.alias === alias) as AnyManifest | undefined;

describe('blocking manifests', () => {
  it('the block action targets the tracked URL entity type, without a row condition', () => {
    const block = byAlias(PNFT_BLOCK_ACTION_ALIAS);

    expect(PNFT_BLOCK_ACTION_ALIAS).toBe('Pnft.EntityAction.TrackedUrl.Block');
    expect(block?.type).toBe('entityAction');
    expect(block?.forEntityTypes).toStrictEqual([PNFT_TRACKED_URL_ENTITY_TYPE]);
    expect(block?.meta?.icon).toBe('icon-block');
    expect(block?.conditions ?? []).toHaveLength(0);
  });

  it('the block action sorts after create redirect, so a page row keeps create redirect as its first action', () => {
    expect(byAlias(PNFT_BLOCK_ACTION_ALIAS)?.weight ?? 0).toBeLessThan(0);
  });

  it('the unblock action targets the block rule entity type', () => {
    const unblock = byAlias(PNFT_UNBLOCK_ACTION_ALIAS);

    expect(PNFT_UNBLOCK_ACTION_ALIAS).toBe('Pnft.EntityAction.BlockRule.Unblock');
    expect(PNFT_BLOCK_RULE_ENTITY_TYPE).toBe('page-not-found-tracker-block-rule');
    expect(unblock?.type).toBe('entityAction');
    expect(unblock?.forEntityTypes).toStrictEqual([PNFT_BLOCK_RULE_ENTITY_TYPE]);
  });

  it('the add rule action is a collection action of the block rule collection only', () => {
    const add = byAlias(PNFT_ADD_BLOCK_RULE_ACTION_ALIAS);

    expect(add?.type).toBe('collectionAction');
    expect(add?.kind).toBe('button');
    expect(add?.conditions).toContainEqual({ alias: 'Umb.Condition.CollectionAlias', match: PNFT_BLOCK_RULE_COLLECTION_ALIAS });
  });

  it('the dashboard is the second Content tab, behind the access condition', () => {
    const dashboard = byAlias(PNFT_BLOCK_RULES_DASHBOARD_ALIAS);

    expect(PNFT_BLOCK_RULES_DASHBOARD_ALIAS).toBe('Umbraco.Community.PageNotFoundTracker.Dashboard.BlockRules');
    expect(dashboard?.type).toBe('dashboard');
    // Tabs sort by weight descending: below the tracked URLs dashboard (5) means after it.
    expect(dashboard?.weight).toBe(4);
    expect(dashboard?.meta?.pathname).toBe('blocked-urls');
    expect(dashboard?.conditions).toStrictEqual([
      { alias: 'Umb.Condition.SectionAlias', match: 'Umb.Section.Content' },
      { alias: 'Pnft.Condition.DashboardAccess' },
    ]);
  });
});
