import { test, expect, type Locator, type Page } from '@playwright/test';

// Deleting tracked URLs (single-site profile, logged in through auth.setup.ts): the row action, "Delete selected" and
// "Delete all matching".
//
// Like dashboard.spec.ts it sends real 404s to paths that are unique to this run and needs the site to run with a
// short flush interval (Umbraco__Community__PageNotFoundTracker__FlushInterval=00:00:01). Everything a test tracks
// carries the run prefix and is removed at the end (afterAll deletes whatever is left, through "Delete all matching"
// filtered on the prefix). The unfiltered "Delete all" is never confirmed here: it would delete other runs' rows.

// Copy of the client's data-mark values (src/collection/data-marks.ts and src/delete/data-marks.ts in the
// BackOffice.Client project): change both together.
const MARKS = {
  collection: 'pnft-collection',
  search: 'pnft-search',
  row: 'pnft-row',
  stateEmpty: 'pnft-state-empty',
  stateEmptyFiltered: 'pnft-state-empty-filtered',
  deleteAllModal: 'pnft-delete-all-modal',
  deleteAllConfirm: 'pnft-delete-all-confirm',
  deleteAllCancel: 'pnft-delete-all-cancel',
  deleteAllConfirmInput: 'pnft-delete-all-confirm-input',
  deleteAllCount: 'pnft-delete-all-count',
} as const;

const DELETE_ACTION = 'entity-action:Pnft.EntityAction.TrackedUrl.Delete';
const DELETE_ALL_ACTION = 'collection-action:Pnft.CollectionAction.TrackedUrl.DeleteAll';
const DASHBOARD_PATH = '/umbraco/section/content/dashboard/page-not-found-tracker';

const run = Date.now();
const prefix = `pnft-e2e-del-${run}`;

function trackedRows(page: Page): Locator {
  return page.getByTestId(MARKS.row);
}

function trackedRowFor(page: Page, path: string): Locator {
  return trackedRows(page).filter({ has: page.locator('span.url', { hasText: new RegExp(`^${path}$`) }) });
}

/** Opens the tracked list and searches for `term` until `expectedRows` rows show (reloads absorb the flush interval). */
async function openTrackedAndSearch(page: Page, term: string, expectedRows: number): Promise<void> {
  await expect(async () => {
    await page.goto(DASHBOARD_PATH);
    await expect(page.getByTestId(MARKS.collection)).toBeVisible({ timeout: 20_000 });
    await page.getByTestId(MARKS.search).locator('input').fill(term);
    await expect(trackedRows(page)).toHaveCount(expectedRows, { timeout: 5_000 });
  }).toPass({ timeout: 90_000, intervals: [1_000, 2_000] });
}

/** Hits is the cell before the actions column (the first cells are the checkbox and the URL). */
async function hitsOf(row: Locator): Promise<string> {
  const cells = await row.locator('uui-table-cell').allInnerTexts();
  return cells[cells.length - 2].trim();
}

/** Opens the row's "..." dropdown and clicks Delete (Delete is always in the dropdown). */
async function clickDelete(row: Locator): Promise<void> {
  await row.locator('umb-entity-actions-dropdown uui-button').first().click();
  await row.getByTestId(DELETE_ACTION).click();
}

async function confirmUmbracoModal(page: Page): Promise<void> {
  await page.locator('umb-confirm-modal #confirm').click();
  await expect(page.locator('umb-confirm-modal')).toBeHidden();
}

async function track(request: { get(url: string): Promise<{ status(): number }> }, paths: string[]): Promise<void> {
  for (const path of paths) {
    expect((await request.get(path)).status(), path).toBe(404);
  }
}

test.describe('delete tracked URLs', () => {
  test.describe.configure({ timeout: 180_000 });

  test.afterAll(async ({ browser }) => {
    const context = await browser.newContext({ storageState: '.auth/user.json', ignoreHTTPSErrors: true });
    const page = await context.newPage();
    try {
      await page.goto(DASHBOARD_PATH);
      await expect(page.getByTestId(MARKS.collection)).toBeVisible({ timeout: 20_000 });
      await page.getByTestId(MARKS.search).locator('input').fill(prefix);
      // Settled for the prefix: every shown row has it in its URL, or the empty state shows.
      await expect
        .poll(
          async () => {
            const urls = await trackedRows(page).locator('span.url').allInnerTexts();
            if (urls.length === 0) {
              return (
                (await page.getByTestId(MARKS.stateEmptyFiltered).count()) + (await page.getByTestId(MARKS.stateEmpty).count()) > 0
              );
            }
            return urls.every((url) => url.includes(prefix));
          },
          { timeout: 20_000 },
        )
        .toBe(true);
      if ((await trackedRows(page).count()) > 0) {
        await page.getByTestId(DELETE_ALL_ACTION).click();
        // A filter is active, so no typed word is asked for. Never confirm an unfiltered delete.
        await expect(page.getByTestId(MARKS.deleteAllModal)).toBeVisible();
        await expect(page.getByTestId(MARKS.deleteAllConfirmInput)).toHaveCount(0);
        await page.getByTestId(MARKS.deleteAllConfirm).click();
        await expect(page.getByTestId(MARKS.deleteAllModal)).toBeHidden();
      }
    } finally {
      await context.close();
    }
  });

  test('Delete_removes_a_single_row', async ({ page, request }) => {
    const path = `/${prefix}-single`;
    const keep = `/${prefix}-single-keep`;
    await track(request, [path, path, path, keep]);
    await openTrackedAndSearch(page, `${prefix}-single`, 2);
    await expect.poll(async () => hitsOf(trackedRowFor(page, path)), { timeout: 1_000 }).not.toBe('');

    // Cancel keeps the row.
    await clickDelete(trackedRowFor(page, path));
    await page.locator('umb-confirm-modal #cancel').click();
    await expect(page.locator('umb-confirm-modal')).toBeHidden();
    await expect(trackedRowFor(page, path)).toHaveCount(1);

    await clickDelete(trackedRowFor(page, path));
    await confirmUmbracoModal(page);
    await expect(trackedRowFor(page, path)).toHaveCount(0);
    await expect(trackedRowFor(page, keep)).toHaveCount(1);

    // The deleted URL is tracked again from 1 after another 404.
    await track(request, [path]);
    await openTrackedAndSearch(page, `${prefix}-single`, 2);
    await expect(trackedRowFor(page, path)).toHaveCount(1);
    expect(await hitsOf(trackedRowFor(page, path))).toBe('1');
  });

  test('Delete_selected_removes_the_selected_rows_only', async ({ page, request }) => {
    const a = `/${prefix}-sel-a`;
    const b = `/${prefix}-sel-b`;
    const c = `/${prefix}-sel-c`;
    await track(request, [a, b, c]);
    await openTrackedAndSearch(page, `${prefix}-sel`, 3);

    // The selection checkbox has no size of its own: a click on the row's first cell toggles it.
    await trackedRowFor(page, a).locator('uui-table-cell').first().click();
    await trackedRowFor(page, b).locator('uui-table-cell').first().click();

    // The bulk action has no data-mark in 17.8: it is an umb-entity-bulk-action in the selection footer.
    const bulk = page.locator('umb-collection-selection-actions umb-entity-bulk-action').filter({ hasText: 'Delete selected' });
    await expect(bulk).toBeVisible();
    await bulk.locator('uui-button').first().click();
    await confirmUmbracoModal(page);

    await expect(trackedRowFor(page, a)).toHaveCount(0);
    await expect(trackedRowFor(page, b)).toHaveCount(0);
    await expect(trackedRowFor(page, c)).toHaveCount(1);
  });

  test('Delete_all_matching_a_search_removes_only_those_rows', async ({ page, request }) => {
    const gone1 = `/${prefix}-all-x1`;
    const gone2 = `/${prefix}-all-x2`;
    const stays = `/${prefix}-allkeep`;
    await track(request, [gone1, gone2, stays]);
    await openTrackedAndSearch(page, `${prefix}-all`, 3);

    // Search narrows to the two rows.
    await page.getByTestId(MARKS.search).locator('input').fill(`${prefix}-all-x`);
    await expect(trackedRows(page)).toHaveCount(2);

    await page.getByTestId(DELETE_ALL_ACTION).click();
    await expect(page.getByTestId(MARKS.deleteAllModal)).toBeVisible();
    await expect(page.getByTestId(MARKS.deleteAllCount)).toContainText('2');
    // A filter is active: no typed word needed.
    await expect(page.getByTestId(MARKS.deleteAllConfirmInput)).toHaveCount(0);
    await page.getByTestId(MARKS.deleteAllConfirm).click();
    await expect(page.getByTestId(MARKS.deleteAllModal)).toBeHidden();
    await expect(trackedRows(page)).toHaveCount(0);

    // The row outside the search is untouched.
    await page.getByTestId(MARKS.search).locator('input').fill(`${prefix}-all`);
    await expect(trackedRowFor(page, stays)).toHaveCount(1);
    await expect(trackedRows(page)).toHaveCount(1);
  });

  test('Delete_all_without_a_filter_needs_the_typed_word', async ({ page, request }) => {
    const path = `/${prefix}-typed`;
    await track(request, [path]);
    await openTrackedAndSearch(page, `${prefix}-typed`, 1);

    // Clear the search: the delete applies to everything, so the typed word is required.
    await page.getByTestId(MARKS.search).locator('input').fill('');
    // The list has reloaded unfiltered once it shows a row that is not this test's.
    await expect
      .poll(async () => (await trackedRows(page).locator('span.url').allInnerTexts()).some((url) => !url.includes(`${prefix}-typed`)))
      .toBe(true);
    await page.getByTestId(DELETE_ALL_ACTION).click();
    await expect(page.getByTestId(MARKS.deleteAllModal)).toBeVisible();

    // A uui-button is a custom element: Playwright's toBeDisabled does not see its disabled attribute.
    const confirm = page.getByTestId(MARKS.deleteAllConfirm);
    await expect(confirm).toHaveAttribute('disabled', '');
    const input = page.getByTestId(MARKS.deleteAllConfirmInput).locator('input');
    await input.fill('dele');
    await expect(confirm).toHaveAttribute('disabled', '');
    await input.fill('delete');
    await expect(confirm).not.toHaveAttribute('disabled');
    await input.fill('');
    await expect(confirm).toHaveAttribute('disabled', '');

    // Never confirmed: it would delete the rows of other tests and runs.
    await page.getByTestId(MARKS.deleteAllCancel).click();
    await expect(page.getByTestId(MARKS.deleteAllModal)).toBeHidden();
    await page.getByTestId(MARKS.search).locator('input').fill(`${prefix}-typed`);
    await expect(trackedRowFor(page, path)).toHaveCount(1);
  });
});
