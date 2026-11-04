import type { UmbLocalizationDictionary } from '@umbraco-cms/backoffice/localization-api';

// English texts of the package. Keys are used as `pageNotFoundTracker_<key>`.
export default {
  pageNotFoundTracker: {
    dashboardLabel: 'Page not found',
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
    unavailableHeadline: 'Tracking database unsupported or unavailable',
    unavailableText:
      'The tracking database is not supported by this package or could not be set up when the site started. ' +
      'Nothing is tracked or shown until this is fixed. Check the site log, fix the cause and restart the site.',
    createRedirect: 'Create redirect',
    createRedirectHeadline: 'Create redirect',
    createRedirectConfirm: 'Create redirect',
    redirectSummaryHeadline: 'Tracked URL',
    redirectExplanation:
      "A redirect from this URL to the page you pick is added to Umbraco's redirect URL management. " +
      'Visitors of this URL are then sent to that page, and the URL is removed from this list.',
    redirectTarget: 'Redirect to',
    redirectTargetDescription: 'The page visitors of this URL are sent to.',
    redirectTargetRequired: 'Pick the page to redirect to.',
    redirectFailed: 'The redirect could not be created.',
    redirectNotFound: 'This tracked URL no longer exists. Close this dialog and reload the list.',
    redirectUnavailable:
      'The tracking database is unavailable, so the redirect was not created. Check the site log, fix the cause and restart the site.',
    redirectError: 'The redirect could not be created because the request failed. Check that the site is running and try again.',
    redirectCreatedHeadline: 'Redirect created',
    redirectCreated: (url: string) => `${url} now redirects to the chosen page.`,
    redirectCreatedRecordKept: 'The redirect was created, but the tracked URL could not be removed from the list.',
  },
} satisfies UmbLocalizationDictionary;
