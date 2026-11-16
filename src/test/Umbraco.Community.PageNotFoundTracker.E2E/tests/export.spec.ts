import { readFile } from 'node:fs/promises';
import { test, expect, type Download, type Locator, type Page } from '@playwright/test';

// The export of the tracked URL dashboard (single-site profile, logged in through auth.setup.ts).
//
// beforeAll sends real 404s to paths that are unique to this run, so the tests only look at their own rows and
// stay deterministic when the suite runs again against the same database. Tracked hits reach the database through a
// background flush, so the site must run with a short flush interval
// (Umbraco__Community__PageNotFoundTracker__FlushInterval=00:00:01, as in pr-validation.yml).

// Copy of the client's data-mark values (src/collection/data-marks.ts in the BackOffice.Client project):
// change both together.
const MARKS = {
  collection: 'pnft-collection',
  search: 'pnft-search',
  row: 'pnft-row',
  sortUrl: 'pnft-sort-url',
} as const;

// Umbraco marks a collection action's button with `collection-action:<alias>`.
const EXPORT_ACTION = 'collection-action:Pnft.CollectionAction.TrackedUrl.Export';

const DASHBOARD_PATH = '/umbraco/section/content/dashboard/page-not-found-tracker';

const HEADER = 'Url;Relative path;Domain;Culture;Type;First seen (UTC);Last seen (UTC);Hits';

const run = Date.now();
const prefix = `pnft-e2e-export-${run}`;
const pathA = `/${prefix}-a`;
const pathB = `/${prefix}-b`;
const pathFile = `/${prefix}-file.jpg`;

// A URL that the search on `prefix` must not export.
const outsidePath = `/pnft-e2e-other-${run}`;

function rows(page: Page): Locator {
  return page.getByTestId(MARKS.row);
}

async function rowUrls(page: Page): Promise<string[]> {
  return rows(page).locator('span.url').allTextContents();
}

/**
 * Opens the dashboard and searches for `term` until `count` rows show. Reloads between attempts: that absorbs the flush
 * interval and a backoffice that loads blank on a direct link in a headless browser.
 */
async function openAndSearch(page: Page, term: string, count: number): Promise<void> {
  await expect(async () => {
    await page.goto(DASHBOARD_PATH);
    await expect(page.getByTestId(MARKS.collection)).toBeVisible({ timeout: 20_000 });
    await page.getByTestId(MARKS.search).locator('input').fill(term);
    await expect
      .poll(async () => (await rowUrls(page)).length, { timeout: 5_000, message: `${count} rows` })
      .toBe(count);
  }).toPass({ timeout: 90_000, intervals: [1_000, 2_000] });
}

/** Clicks Export and returns the downloaded file's name and text (decoded as UTF-8, the byte order mark kept as `﻿`). */
async function exportAndRead(page: Page): Promise<{ download: Download; text: string }> {
  const downloadPromise = page.waitForEvent('download');
  await page.getByTestId(EXPORT_ACTION).click();
  const download = await downloadPromise;
  const path = await download.path();
  return { download, text: (await readFile(path)).toString('utf8') };
}

function dataLines(text: string): string[] {
  // Drops the byte order mark and the header; the rows end in \r\n.
  const lines = text.replace(/^﻿/, '').split('\r\n').filter((line) => line !== '');
  expect(lines[0]).toBe(HEADER);
  return lines.slice(1);
}

test.describe('tracked URL export', () => {
  test.describe.configure({ timeout: 180_000 });

  test.beforeAll(async ({ request }) => {
    const hits: Array<[string, number]> = [[pathA, 1], [pathB, 3], [pathFile, 2], [outsidePath, 1]];

    for (const [path, times] of hits) {
      for (let i = 0; i < times; i++) {
        const response = await request.get(path);
        expect(response.status(), path).toBe(404);
      }
    }
  });

  test('export_downloads_the_rows_matching_the_search', async ({ page }) => {
    await openAndSearch(page, prefix, 3);

    const { download, text } = await exportAndRead(page);

    expect(download.suggestedFilename()).toMatch(/^page-not-found-\d{8}-\d{6}\.csv$/);
    expect(text.startsWith('﻿')).toBe(true);
    const lines = dataLines(text);
    // Hits descending is the default order: b (3 hits), the file (2 hits), then a (1 hit). That differs from URL order.
    expect(lines.map((line) => line.split(';')[0])).toEqual([pathB, pathFile, pathA]);
    expect(lines.every((line) => !line.includes(outsidePath))).toBe(true);
    expect(lines[0].split(';').slice(4)).toEqual([expect.stringMatching(/^Page$/), expect.stringMatching(/^\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}$/), expect.stringMatching(/^\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}$/), '3']);
    expect(lines[1].split(';')[4]).toBe('File');
  });

  test('export_follows_the_sort_of_the_list', async ({ page }) => {
    await openAndSearch(page, prefix, 3);
    await page.getByTestId(MARKS.sortUrl).click();
    // The list re-sorts by URL; whichever direction the first click gives, the file must match what the table shows.
    await expect.poll(async () => (await rowUrls(page)).join('|')).not.toBe([pathB, pathFile, pathA].join('|'));
    const shown = await rowUrls(page);

    const { text } = await exportAndRead(page);

    expect(dataLines(text).map((line) => line.split(';')[0])).toEqual(shown);
  });
});
