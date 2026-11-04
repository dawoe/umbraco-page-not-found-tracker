import { test, expect, type Locator, type Page } from '@playwright/test';

// Creating a redirect from a tracked URL row (single-site profile, logged in through auth.setup.ts).
//
// Like dashboard.spec.ts it sends real 404s to paths that are unique to this run and needs the site to run with a
// short flush interval (Umbraco__Community__PageNotFoundTracker__FlushInterval=00:00:01).

// Copy of the client's data-mark values (src/redirect/data-marks.ts and src/collection/data-marks.ts in the
// BackOffice.Client project): change both together.
const MARKS = {
  collection: 'pnft-collection',
  search: 'pnft-search',
  row: 'pnft-row',
  modal: 'pnft-create-redirect-modal',
  confirm: 'pnft-redirect-confirm',
  cancel: 'pnft-redirect-cancel',
  error: 'pnft-redirect-error',
  targetPicker: 'pnft-redirect-target-picker',
  targetRequired: 'pnft-redirect-target-required',
} as const;

const CREATE_REDIRECT_ACTION = 'entity-action:Pnft.EntityAction.TrackedUrl.CreateRedirect';
const DASHBOARD_PATH = '/umbraco/section/content/dashboard/page-not-found-tracker';

const prefix = `pnft-e2e-redirect-${Date.now()}`;
const pagePath = `/${prefix}-page`;
const cancelPath = `/${prefix}-cancel`;
const filePath = `/${prefix}-style.css`;

function rows(page: Page): Locator {
  return page.getByTestId(MARKS.row);
}

/** The row whose URL cell is exactly `path`. */
function rowFor(page: Page, path: string): Locator {
  return rows(page).filter({ has: page.locator('span.url', { hasText: new RegExp(`^${path}$`) }) });
}

/** Opens the dashboard and searches for `term` until at least one row shows (reloads absorb the flush interval). */
async function openAndSearch(page: Page, term: string, expectedRows: number): Promise<void> {
  await expect(async () => {
    await page.goto(DASHBOARD_PATH);
    await expect(page.getByTestId(MARKS.collection)).toBeVisible({ timeout: 20_000 });
    await page.getByTestId(MARKS.search).locator('input').fill(term);
    await expect(rows(page)).toHaveCount(expectedRows, { timeout: 5_000 });
  }).toPass({ timeout: 90_000, intervals: [1_000, 2_000] });
}

test.describe('create redirect from a tracked URL', () => {
  test.describe.configure({ timeout: 180_000 });

  test.beforeAll(async ({ request }) => {
    for (const path of [pagePath, cancelPath, filePath]) {
      const response = await request.get(path);
      expect(response.status(), path).toBe(404);
    }
  });

  test('file_rows_have_no_create_redirect_action', async ({ page }) => {
    await openAndSearch(page, prefix, 3);

    await expect(rowFor(page, filePath)).toHaveCount(1);
    await expect(rowFor(page, filePath).getByTestId(CREATE_REDIRECT_ACTION)).toHaveCount(0);
    await expect(rowFor(page, pagePath).getByTestId(CREATE_REDIRECT_ACTION)).toHaveCount(1);
  });

  test('cancel_keeps_the_row_and_confirm_needs_a_page', async ({ page }) => {
    await openAndSearch(page, prefix, 3);

    await rowFor(page, cancelPath).getByTestId(CREATE_REDIRECT_ACTION).click();
    const modal = page.getByTestId(MARKS.modal);
    await expect(modal).toBeVisible();

    // No page picked: the form asks for one and nothing is sent.
    await page.getByTestId(MARKS.confirm).click();
    await expect(page.getByTestId(MARKS.targetRequired)).toBeVisible();

    await page.getByTestId(MARKS.cancel).click();
    await expect(modal).toBeHidden();
    await expect(rowFor(page, cancelPath)).toHaveCount(1);
  });

  test('creates_a_redirect_to_the_picked_page_and_removes_the_row', async ({ page, request }) => {
    await openAndSearch(page, prefix, 3);

    await rowFor(page, pagePath).getByTestId(CREATE_REDIRECT_ACTION).click();
    await expect(page.getByTestId(MARKS.modal)).toBeVisible();

    // Pick the About page in the document picker (a modal with a content tree; the tree items are shadow DOM).
    await page.getByTestId(MARKS.targetPicker).getByRole('button', { name: /choose/i }).click();
    const picker = page.locator('umb-document-picker-modal');
    await picker.locator('uui-menu-item[label="Home"]').first().locator('#caret-button').click();
    await picker.locator('uui-menu-item[label="About"]').first().locator('#label-button').click();
    await picker.getByRole('button', { name: 'Choose' }).click();
    await expect(picker).toBeHidden();

    await page.getByTestId(MARKS.confirm).click();
    await expect(page.getByTestId(MARKS.modal)).toBeHidden();
    await expect(page.getByTestId(MARKS.error)).toHaveCount(0);

    // The list reloads and the row is gone, the other rows stay.
    await expect(rowFor(page, pagePath)).toHaveCount(0);
    await expect(rowFor(page, cancelPath)).toHaveCount(1);

    // The URL now answers a permanent redirect to the page, no longer a 404.
    const response = await request.get(pagePath, { maxRedirects: 0 });
    expect(response.status()).toBe(301);
    expect(new URL(response.headers()['location'], 'https://localhost').pathname).toBe('/about/');
  });
});
