import { UMB_COLLECTION_ALIAS_CONDITION } from '@umbraco-cms/backoffice/collection';
import {
  PNFT_TRACKED_URL_COLLECTION_ALIAS,
  PNFT_TRACKED_URL_REPOSITORY_ALIAS,
  PNFT_TRACKED_URL_TABLE_VIEW_ALIAS,
  PNFT_TRACKED_URL_TEXT_FILTER_ALIAS,
} from './constants.js';

const forThisCollection = [{ alias: UMB_COLLECTION_ALIAS_CONDITION, match: PNFT_TRACKED_URL_COLLECTION_ALIAS }];

/**
 * The tracked URL collection: repository, collection, its table view and its search box. Row actions are
 * entity actions for `PNFT_TRACKED_URL_ENTITY_TYPE`, registered by redirect/ (create redirect) and blocking/ (block).
 */
export const manifests: Array<UmbExtensionManifest> = [
  {
    type: 'repository',
    alias: PNFT_TRACKED_URL_REPOSITORY_ALIAS,
    name: 'Page Not Found Tracker Tracked URL Repository',
    api: () => import('./repository/tracked-url.repository.js'),
  },
  {
    type: 'collection',
    alias: PNFT_TRACKED_URL_COLLECTION_ALIAS,
    name: 'Page Not Found Tracker Tracked URL Collection',
    api: () => import('./collection.context.js'),
    element: () => import('./tracked-url-collection.element.js'),
    meta: {
      repositoryAlias: PNFT_TRACKED_URL_REPOSITORY_ALIAS,
    },
  },
  {
    type: 'collectionView',
    alias: PNFT_TRACKED_URL_TABLE_VIEW_ALIAS,
    name: 'Page Not Found Tracker Tracked URL Table View',
    element: () => import('./views/table/tracked-url-table.element.js'),
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
    alias: PNFT_TRACKED_URL_TEXT_FILTER_ALIAS,
    name: 'Page Not Found Tracker Tracked URL Search',
    element: () => import('./filters/search.element.js'),
    conditions: forThisCollection,
  },
];
