import { test, expect, type Locator, type Page } from '@playwright/test';

// The tracked URL dashboard in the Content section (single-site profile, logged in through auth.setup.ts).
//
// beforeAll sends real 404s to paths that are unique to this run, so the tests only look at their own rows and
// stay deterministic when the suite runs again against the same database. Tracked hits reach the database
// through a background flush, so the site must run with a short flush interval
// (Umbraco__Community__PageNotFoundTracker__FlushInterval=00:00:01, as in pr-validation.yml); the tests reload and
// search until the rows are there.

// Copy of the client's data-mark values (src/collection/data-marks.ts in the BackOffice.Client project):
// change both together.
const MARKS = {
  collection: 'pnft-collection',
  search: 'pnft-search',
  filterDomain: 'pnft-filter-domain',
  pageSize: 'pnft-page-size',
  row: 'pnft-row',
  stateEmptyFiltered: 'pnft-state-empty-filtered',
  total: 'pnft-total',
  sortUrl: 'pnft-sort-url',
} as const;

const DASHBOARD_PATH = '/umbraco/section/content/dashboard/page-not-found-tracker';
const LIST_API_PATH = '/umbraco/pagenotfoundtracker/api/v1/tracked-urls';

const run = Date.now();
const prefix = `pnft-e2e-${run}`;
const pathA = `/${prefix}-a`;
const pathB = `/${prefix}-b`;
const pathC = `/${prefix}-c`;
const pathFile = `/${prefix}-file.jpg`;

// 22 single-hit URLs under their own prefix (does not match `prefix`): more than the default page size of 20,
// so there is a page 2.
const pagerPrefix = `pnft-e2e-pager-${run}`;
const pagerPaths = Array.from({ length: 22 }, (_, i) => `/${pagerPrefix}-${String(i + 1).padStart(2, '0')}`);

function rows(page: Page): Locator {
  return page.getByTestId(MARKS.row);
}

async function rowUrls(page: Page): Promise<string[]> {
  return rows(page).locator('span.url').allTextContents();
}

async function search(page: Page, term: string): Promise<void> {
  await page.getByTestId(MARKS.search).locator('input').fill(term);
}

/** Waits until the table shows exactly `count` rows and every row URL contains `term`. */
async function expectRowsMatching(page: Page, term: string, count: number, timeout = 15_000): Promise<void> {
  await expect
    .poll(
      async () => {
        const urls = await rowUrls(page);
        return urls.length === count && urls.every((url) => url.includes(term));
      },
      { timeout, message: `${count} rows containing "${term}"` },
    )
    .toBe(true);
}

/**
 * Opens the dashboard and searches for `term` until `count` rows show (and, when given, the total shows
 * `expectedTotal`). Reloads between attempts: that absorbs the flush interval (the list is only as fresh as the
 * last flush) and a backoffice that loads blank on a direct link in a headless browser.
 */
async function openAndSearch(page: Page, term: string, count: number, expectedTotal?: string): Promise<void> {
  await expect(async () => {
    await page.goto(DASHBOARD_PATH);
    await expect(page.getByTestId(MARKS.collection)).toBeVisible({ timeout: 20_000 });
    await search(page, term);
    await expectRowsMatching(page, term, count, 5_000);
    if (expectedTotal !== undefined) {
      await expect(page.getByTestId(MARKS.total)).toHaveText(expectedTotal, { timeout: 5_000 });
    }
  }).toPass({ timeout: 90_000, intervals: [1_000, 2_000] });
}

async function selectOption(page: Page, mark: string, value: string): Promise<void> {
  await page.getByTestId(mark).locator('select').selectOption(value);
}

/** The framework pager (uui-pagination); its accessible name states the current page. */
function pager(page: Page): Locator {
  return page.getByRole('navigation', { name: /^Pagination navigation/ });
}

async function expectCurrentPage(page: Page, pageNumber: number): Promise<void> {
  await expect(pager(page)).toHaveAccessibleName(`Pagination navigation. Current page: ${pageNumber}.`);
}

/**
 * Opens the pager search and moves to page 2 (2 of the 22 pager rows). Waits for "22 URLs" first: page 1 can
 * already show 20 rows while the last flush has not written all 22, and then there is no page 2 yet.
 */
async function openPagerPage2(page: Page): Promise<void> {
  await openAndSearch(page, pagerPrefix, 20, '22 URLs');
  await pager(page).getByRole('button', { name: 'Go to page 2' }).click();
  await expectRowsMatching(page, pagerPrefix, 2);
  await expectCurrentPage(page, 2);
}

test.describe('tracked URL dashboard', () => {
  test.describe.configure({ timeout: 180_000 });

  test.beforeAll(async ({ request }) => {
    const hits: Array<[string, number]> = [[pathA, 3], [pathB, 2], [pathC, 1], [pathFile, 1], ...pagerPaths.map((path): [string, number] => [path, 1])];

    for (const [path, times] of hits) {
      for (let i = 0; i < times; i++) {
        const response = await request.get(path);
        expect(response.status(), path).toBe(404);
      }
    }
  });

  test('lists_tracked_urls_ordered_by_hits_descending', async ({ page }) => {
    await openAndSearch(page, prefix, 4);

    const urls = await rowUrls(page);
    // The default sort is hits descending with ties by ascending id.
    // a has 3 hits, b 2; c and the file have 1 each (their order is the tie-break by id).
    expect(urls.slice(0, 2)).toEqual([pathA, pathB]);
    expect([...urls.slice(2)].sort()).toEqual([pathC, pathFile].sort());
    await expect(page.getByTestId(MARKS.total)).toHaveText('4 URLs');
  });

  test('search_narrows_by_url', async ({ page }) => {
    await openAndSearch(page, prefix, 4);

    // Upper case: the search is case-insensitive.
    await search(page, `${prefix}-B`.toUpperCase());
    await expect.poll(() => rowUrls(page)).toEqual([pathB]);
    await expect(page.getByTestId(MARKS.total)).toHaveText('1 URL');
  });

  test('sorting_by_url_toggles_direction', async ({ page }) => {
    await openAndSearch(page, prefix, 4);
    const ascending = [pathA, pathB, pathC, pathFile];

    // The first click on the URL header sorts ascending (from the default hits descending), the second descending.
    await page.getByTestId(MARKS.sortUrl).click();
    await expect.poll(() => rowUrls(page)).toEqual(ascending);

    await page.getByTestId(MARKS.sortUrl).click();
    await expect.poll(() => rowUrls(page)).toEqual([...ascending].reverse());
  });

  test('page_filter_by_type_shows_file_row', async ({ page }) => {
    await openAndSearch(page, prefix, 4);

    await expect(rows(page).filter({ hasText: pathFile }).locator('span.type')).toHaveText('File');
    for (const path of [pathA, pathB, pathC]) {
      await expect(rows(page).filter({ hasText: path }).locator('span.type')).toHaveText('Page');
    }
  });

  test('page_size_can_be_changed_to_50_and_100', async ({ page }) => {
    await openAndSearch(page, prefix, 4);

    for (const size of ['50', '100']) {
      const listRequest = page.waitForRequest(
        (req) => req.url().includes(LIST_API_PATH) && new URL(req.url()).searchParams.get('take') === size,
      );
      await selectOption(page, MARKS.pageSize, size);
      await listRequest;
      await expect(page.getByTestId(MARKS.pageSize).locator('select')).toHaveValue(size);
      await expectRowsMatching(page, prefix, 4);
    }
  });

  test('domain_filter_offers_no_domain_choice', async ({ page }) => {
    await openAndSearch(page, prefix, 4);

    // single-site has no domains, so every tracked URL has none.
    const noDomain = page.getByTestId(MARKS.filterDomain).locator('option', { hasText: 'No domain' });
    await expect(noDomain).toHaveCount(1);

    await selectOption(page, MARKS.filterDomain, 'empty');
    await expectRowsMatching(page, prefix, 4);
  });

  test('shows_empty_after_filter_state', async ({ page }) => {
    await openAndSearch(page, prefix, 4);

    await search(page, `${prefix}-no-such-url`);
    await expect(page.getByTestId(MARKS.stateEmptyFiltered)).toBeVisible();
    await expect(rows(page)).toHaveCount(0);
  });

  test('returns_to_page_1_from_page_2_on_sort_search_filter_and_page_size', async ({ page }) => {
    // Page 1 has 20 rows and page 2 has 2, so the row count tells the pages apart.
    await test.step('sort', async () => {
      await openPagerPage2(page);
      // The first click on the URL header sorts ascending, so page 1 holds pager URLs 01 to 20.
      await page.getByTestId(MARKS.sortUrl).click();
      await expect.poll(() => rowUrls(page)).toEqual(pagerPaths.slice(0, 20));
      await expectCurrentPage(page, 1);
    });

    await test.step('search', async () => {
      await openPagerPage2(page);
      await search(page, `${pagerPrefix}-`);
      await expectRowsMatching(page, `${pagerPrefix}-`, 20);
      await expectCurrentPage(page, 1);
    });

    await test.step('filter', async () => {
      await openPagerPage2(page);
      await selectOption(page, MARKS.filterDomain, 'empty');
      await expectRowsMatching(page, pagerPrefix, 20);
      await expectCurrentPage(page, 1);
    });

    await test.step('page size', async () => {
      // 22 rows fit on one page of 50: page 2 of 50 would be empty.
      await openPagerPage2(page);
      await selectOption(page, MARKS.pageSize, '50');
      await expectRowsMatching(page, pagerPrefix, 22);
    });
  });
});
