import type { UmbLocalizationDictionary } from '@umbraco-cms/backoffice/localization-api';

// English texts of the package. Keys are used as `pageNotFoundTracker_<key>`.
export default {
  pageNotFoundTracker: {
    trackedUrl: 'Tracked URL',
    trackedUrls: 'Tracked URLs',
    columnUrl: 'URL',
    columnDomain: 'Domain',
    columnCulture: 'Culture',
    columnIsFile: 'File',
    columnHits: 'Hits',
    columnFirstSeen: 'First seen',
    columnLastSeen: 'Last seen',
    noDomain: 'No domain',
    noCulture: 'No culture',
    allDomains: 'All domains',
    allCultures: 'All cultures',
  },
} satisfies UmbLocalizationDictionary;
