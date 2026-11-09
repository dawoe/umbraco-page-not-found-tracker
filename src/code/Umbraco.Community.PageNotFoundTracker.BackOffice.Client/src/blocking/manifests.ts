import { PNFT_DASHBOARD_ACCESS_CONDITION_ALIAS } from '../conditions/constants.js';
import { PNFT_TRACKED_URL_ENTITY_TYPE } from '../entity.js';
import {
  PNFT_ADD_BLOCK_RULE_ACTION_ALIAS,
  PNFT_BLOCK_ACTION_ALIAS,
  PNFT_BLOCK_RULE_COLLECTION_ALIAS,
  PNFT_BLOCK_RULE_ENTITY_TYPE,
  PNFT_BLOCK_RULE_REPOSITORY_ALIAS,
  PNFT_BLOCK_RULE_TABLE_VIEW_ALIAS,
  PNFT_BLOCK_RULE_TEXT_FILTER_ALIAS,
  PNFT_BLOCK_RULES_DASHBOARD_ALIAS,
  PNFT_BLOCK_RULES_DASHBOARD_PATHNAME,
  PNFT_BLOCK_RULES_DASHBOARD_WEIGHT,
  PNFT_CREATE_BLOCK_RULE_MODAL_ALIAS,
  PNFT_EDIT_RULE_ACTION_ALIAS,
  PNFT_UNBLOCK_ACTION_ALIAS,
} from './constants.js';

// Condition aliases as literals (Umbraco's `UMB_COLLECTION_ALIAS_CONDITION`, `UMB_SECTION_ALIAS_CONDITION_ALIAS`), so this
// file has no backoffice runtime imports and its test runs in Vitest.
const COLLECTION_ALIAS_CONDITION = 'Umb.Condition.CollectionAlias';
const SECTION_ALIAS_CONDITION = 'Umb.Condition.SectionAlias';

const forThisCollection = [{ alias: COLLECTION_ALIAS_CONDITION, match: PNFT_BLOCK_RULE_COLLECTION_ALIAS }];

/**
 * Blocking in the backoffice: the blocked URLs dashboard (second Content tab) with its collection (repository, table
 * view, pattern search, "Add rule" action), the "Block" action on tracked URL rows, the "Edit" and "Unblock" actions on
 * rule rows and the rule modal.
 */
export const manifests: Array<UmbExtensionManifest> = [
  {
    type: 'repository',
    alias: PNFT_BLOCK_RULE_REPOSITORY_ALIAS,
    name: 'Page Not Found Tracker Block Rule Repository',
    api: () => import('./collection/repository/block-rule.repository.js'),
  },
  {
    type: 'collection',
    alias: PNFT_BLOCK_RULE_COLLECTION_ALIAS,
    name: 'Page Not Found Tracker Block Rule Collection',
    api: () => import('./collection/block-rule-collection.context.js'),
    element: () => import('./collection/block-rule-collection.element.js'),
    meta: {
      repositoryAlias: PNFT_BLOCK_RULE_REPOSITORY_ALIAS,
    },
  },
  {
    type: 'collectionView',
    alias: PNFT_BLOCK_RULE_TABLE_VIEW_ALIAS,
    name: 'Page Not Found Tracker Block Rule Table View',
    element: () => import('./collection/views/table/block-rule-table.element.js'),
    weight: 100,
    meta: {
      label: '#pageNotFoundTracker_tableView',
      icon: 'icon-table',
      pathName: 'table',
    },
    conditions: forThisCollection,
  },
  {
    type: 'collectionTextFilter',
    kind: 'default',
    alias: PNFT_BLOCK_RULE_TEXT_FILTER_ALIAS,
    name: 'Page Not Found Tracker Block Rule Search',
    element: () => import('./collection/filters/block-rule-search.element.js'),
    conditions: forThisCollection,
  },
  {
    // 17.8 ships the `button` kind for collection actions (there is no `default` kind).
    type: 'collectionAction',
    kind: 'button',
    alias: PNFT_ADD_BLOCK_RULE_ACTION_ALIAS,
    name: 'Page Not Found Tracker Add Block Rule Collection Action',
    api: () => import('./add-rule.collection-action.js'),
    meta: {
      label: '#pageNotFoundTracker_addRule',
      additionalOptions: true,
    },
    conditions: forThisCollection,
  },
  {
    type: 'entityAction',
    kind: 'default',
    alias: PNFT_BLOCK_ACTION_ALIAS,
    name: 'Page Not Found Tracker Block Entity Action',
    // Below create redirect (weight 0): on a page row create redirect stays the inline first action, block is in the
    // dropdown; a file row has block only.
    weight: -10,
    forEntityTypes: [PNFT_TRACKED_URL_ENTITY_TYPE],
    api: () => import('./block.action.js'),
    meta: {
      icon: 'icon-block',
      label: '#pageNotFoundTracker_block',
      additionalOptions: true,
    },
  },
  {
    type: 'entityAction',
    kind: 'default',
    alias: PNFT_EDIT_RULE_ACTION_ALIAS,
    name: 'Page Not Found Tracker Edit Block Rule Entity Action',
    // Above unblock (weight -10): with two actions Umbraco renders the highest weight inline, so Edit is the row button
    // and Unblock is in the "..." dropdown (which lists both).
    weight: 0,
    forEntityTypes: [PNFT_BLOCK_RULE_ENTITY_TYPE],
    api: () => import('./edit-rule.action.js'),
    meta: {
      icon: 'icon-edit',
      label: '#pageNotFoundTracker_editRule',
      additionalOptions: true,
    },
  },
  {
    type: 'entityAction',
    kind: 'default',
    alias: PNFT_UNBLOCK_ACTION_ALIAS,
    name: 'Page Not Found Tracker Unblock Entity Action',
    // Below edit (weight 0): reached through the row's "..." dropdown.
    weight: -10,
    forEntityTypes: [PNFT_BLOCK_RULE_ENTITY_TYPE],
    api: () => import('./unblock.action.js'),
    meta: {
      icon: 'icon-trash',
      label: '#pageNotFoundTracker_unblock',
      additionalOptions: true,
    },
  },
  {
    type: 'modal',
    alias: PNFT_CREATE_BLOCK_RULE_MODAL_ALIAS,
    name: 'Page Not Found Tracker Create Block Rule Modal',
    element: () => import('./rule-modal.element.js'),
  },
  {
    type: 'dashboard',
    alias: PNFT_BLOCK_RULES_DASHBOARD_ALIAS,
    name: 'Page Not Found Tracker Blocked URLs Dashboard',
    element: () => import('./dashboards/blocked-urls-dashboard.element.js'),
    weight: PNFT_BLOCK_RULES_DASHBOARD_WEIGHT,
    meta: {
      label: '#pageNotFoundTracker_blockedDashboardLabel',
      pathname: PNFT_BLOCK_RULES_DASHBOARD_PATHNAME,
    },
    conditions: [
      { alias: SECTION_ALIAS_CONDITION, match: 'Umb.Section.Content' },
      { alias: PNFT_DASHBOARD_ACCESS_CONDITION_ALIAS },
    ],
  },
];
