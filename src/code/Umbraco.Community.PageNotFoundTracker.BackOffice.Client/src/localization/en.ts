import type { UmbLocalizationDictionary } from '@umbraco-cms/backoffice/localization-api';

// English texts of the package. Keys are used as `pageNotFoundTracker_<key>`.
export default {
  pageNotFoundTracker: {
    trackedUrl: 'Tracked URL',
    trackedUrls: 'Tracked URLs',
    tableView: 'Table',
    columnUrl: 'URL',
    columnDomain: 'Domain',
    columnCulture: 'Culture',
    columnType: 'Type',
    columnHits: 'Hits',
    columnFirstSeen: 'First seen',
    columnLastSeen: 'Last seen',
    typePage: 'Page',
    typeFile: 'File',
    noDomain: 'No domain',
    noCulture: 'No culture',
    allDomains: 'All domains',
    allCultures: 'All cultures',
    filterDomain: 'Filter by domain',
    filterCulture: 'Filter by culture',
    search: 'Search URLs',
    searchPlaceholder: 'Search URLs...',
    pageSize: 'Items per page',
    total: (count: number) => (count === 1 ? '1 URL' : `${count.toLocaleString()} URLs`),
    loading: 'Loading tracked URLs',
    emptyHeadline: 'No 404s tracked yet',
    emptyText:
      'Requests that end in a 404 are counted in the background and written to the database on the next flush ' +
      '(every minute by default). Reload this page after the next flush to see them.',
    emptyFilteredHeadline: 'No matching URLs',
    emptyFilteredText: 'No tracked URLs match the search or filters. Change or clear them to see more.',
    errorHeadline: 'The tracked URLs could not be loaded',
    errorText: 'The request failed. Check that the site is running and reload the page.',
    unavailableHeadline: 'Tracking data is unavailable',
    unavailableText:
      'The tracking database cannot be read at the moment. Check the site log; the list appears again once the database is available.',
  },
} satisfies UmbLocalizationDictionary;
