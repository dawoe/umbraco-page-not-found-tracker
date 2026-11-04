import { test, expect, type Locator, type Page } from '@playwright/test';

// Blocking tracked URLs and the Blocked URLs dashboard (single-site profile, logged in through auth.setup.ts).
//
// Like dashboard.spec.ts it sends real 404s to paths that are unique to this run and needs the site to run with a
// short flush interval (Umbraco__Community__PageNotFoundTracker__FlushInterval=00:00:01). Every rule a test creates
// is removed again (in the test, and in afterAll for the case that a test failed half-way), so reruns on the same
// database work.

// Copy of the client's data-mark values (src/collection/data-marks.ts and src/blocking/data-marks.ts in the
// BackOffice.Client project): change both together.
const MARKS = {
  collection: 'pnft-collection',
  search: 'pnft-search',
  row: 'pnft-row',
  blockCollection: 'pnft-block-collection',
  blockSearch: 'pnft-block-search',
  blockRow: 'pnft-block-row',
  blockModal: 'pnft-block-rule-modal',
  blockPattern: 'pnft-block-pattern',
  blockPatternRequired: 'pnft-block-pattern-required',
  blockStatus: 'pnft-block-status',
  blockError: 'pnft-block-error',
  blockConfirm: 'pnft-block-confirm',
  blockCancel: 'pnft-block-cancel',
} as const;

const BLOCK_ACTION = 'entity-action:Pnft.EntityAction.TrackedUrl.Block';
const UNBLOCK_ACTION = 'entity-action:Pnft.EntityAction.BlockRule.Unblock';
const ADD_RULE_ACTION = 'collection-action:Pnft.CollectionAction.BlockRule.Add';
const DASHBOARD_PATH = '/umbraco/section/content/dashboard/page-not-found-tracker';
const BLOCKED_PATH = '/umbraco/section/content/dashboard/blocked-urls';

const run = Date.now();
const prefix = `pnft-e2e-${run}`;

function trackedRows(page: Page): Locator {
  return page.getByTestId(MARKS.row);
}

function trackedRowFor(page: Page, path: string): Locator {
  return trackedRows(page).filter({ has: page.locator('span.url', { hasText: new RegExp(`^${path}$`) }) });
}

function ruleRows(page: Page): Locator {
  return page.getByTestId(MARKS.blockRow);
}

/** The rule row whose pattern is exactly `pattern`. */
function ruleRowFor(page: Page, pattern: string): Locator {
  return ruleRows(page).filter({ has: page.locator(`span[title="${pattern}"]`) });
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

/** Opens the Blocked URLs tab and searches for `term`. */
async function openBlockedAndSearch(page: Page, term: string): Promise<void> {
  await expect(async () => {
    await page.goto(BLOCKED_PATH);
    await expect(page.getByTestId(MARKS.blockCollection)).toBeVisible({ timeout: 20_000 });
    await page.getByTestId(MARKS.blockSearch).locator('input').fill(term);
    // Settle: the search reloads the list.
    await page.waitForTimeout(500);
  }).toPass({ timeout: 90_000, intervals: [1_000, 2_000] });
}

/** Opens the row's actions dropdown (page rows) and clicks Block, or clicks the inline Block button (file rows). */
async function clickBlock(row: Locator): Promise<void> {
  // Page rows: Create redirect is inline, Block sits in the dropdown behind the "..." button. File rows have no
  // dropdown (one permitted action renders as an inline button).
  const dropdown = row.locator('umb-entity-actions-dropdown uui-button');
  if ((await dropdown.count()) > 0) {
    await dropdown.first().click();
  }
  await row.getByTestId(BLOCK_ACTION).first().click();
}

/** Removes every rule whose pattern contains `term`, through the UI (Umbraco's confirm modal). */
async function unblockAllMatching(page: Page, term: string): Promise<void> {
  await openBlockedAndSearch(page, term);
  for (let guard = 0; guard < 10; guard++) {
    await page.waitForTimeout(700);
    const row = ruleRows(page).first();
    if ((await row.count()) === 0) {
      return;
    }
    await row.getByTestId(UNBLOCK_ACTION).click();
    await page.locator('umb-confirm-modal #confirm').click();
    await expect(page.locator('umb-confirm-modal')).toBeHidden();
  }
}

async function statusOf(page: Page, path: string): Promise<{ status: number; body: string }> {
  const response = await page.request.get(path, { maxRedirects: 0 });
  return { status: response.status(), body: await response.text() };
}

async function fillRuleModal(page: Page, pattern: string): Promise<void> {
  await expect(page.getByTestId(MARKS.blockModal)).toBeVisible();
  await page.getByTestId(MARKS.blockPattern).locator('input').fill(pattern);
}

test.describe('blocked URLs', () => {
  test.describe.configure({ timeout: 180_000 });

  test.afterAll(async ({ browser }) => {
    const context = await browser.newContext({ storageState: '.auth/user.json', ignoreHTTPSErrors: true });
    const page = await context.newPage();
    try {
      await unblockAllMatching(page, prefix);
    } finally {
      await context.close();
    }
  });

  test('Block_from_a_row_moves_the_url_to_the_blocked_list_and_answers_410', async ({ page, request }) => {
    const path = `/${prefix}-block`;
    expect((await request.get(path)).status()).toBe(404);

    await openTrackedAndSearch(page, `${prefix}-block`, 1);
    const row = trackedRowFor(page, path);
    await expect(row).toHaveCount(1);

    await clickBlock(row);
    await fillRuleModalDefaults(page, path);
    await page.getByTestId(MARKS.blockConfirm).click();
    await expect(page.getByTestId(MARKS.blockModal)).toBeHidden();

    // The row leaves the tracked list.
    await expect(trackedRowFor(page, path)).toHaveCount(0);

    // The rule is on the Blocked URLs tab.
    await openBlockedAndSearch(page, `${prefix}-block`);
    await expect(ruleRowFor(page, path)).toHaveCount(1);

    // The URL answers 410 with an empty body.
    const blocked = await statusOf(page, path);
    expect(blocked.status).toBe(410);
    expect(blocked.body).toBe('');

    // A blocked URL is not tracked again: after a flush wait there is still no tracked row.
    expect((await request.get(path)).status()).toBe(410);
    await page.waitForTimeout(4_000);
    await page.goto(DASHBOARD_PATH);
    await expect(page.getByTestId(MARKS.collection)).toBeVisible({ timeout: 20_000 });
    await page.getByTestId(MARKS.search).locator('input').fill(`${prefix}-block`);
    await page.waitForTimeout(1_000);
    await expect(trackedRows(page)).toHaveCount(0);

    await unblockAllMatching(page, `${prefix}-block`);
  });

  test('Add_rule_with_wildcard_blocks_a_sub_path', async ({ page }) => {
    const pattern = `/${prefix}-wild/*`;
    await page.goto(BLOCKED_PATH);
    await expect(page.getByTestId(MARKS.blockCollection)).toBeVisible({ timeout: 20_000 });

    await page.getByTestId(ADD_RULE_ACTION).click();
    await fillRuleModal(page, pattern);
    await page.getByTestId(MARKS.blockConfirm).click();
    await expect(page.getByTestId(MARKS.blockModal)).toBeHidden();

    await openBlockedAndSearch(page, `${prefix}-wild`);
    await expect(ruleRowFor(page, pattern)).toHaveCount(1);

    expect((await statusOf(page, `/${prefix}-wild/a/b`)).status).toBe(410);

    await unblockAllMatching(page, `${prefix}-wild`);
    expect((await statusOf(page, `/${prefix}-wild/a/b`)).status).toBe(404);
  });

  test('Unblock_makes_the_url_trackable_again', async ({ page, request }) => {
    const path = `/${prefix}-unblock`;
    expect((await request.get(path)).status()).toBe(404);
    await openTrackedAndSearch(page, `${prefix}-unblock`, 1);

    await clickBlock(trackedRowFor(page, path));
    await fillRuleModal(page, path);
    await page.getByTestId(MARKS.blockConfirm).click();
    await expect(page.getByTestId(MARKS.blockModal)).toBeHidden();
    expect((await statusOf(page, path)).status).toBe(410);

    await unblockAllMatching(page, `${prefix}-unblock`);
    await openBlockedAndSearch(page, `${prefix}-unblock`);
    await expect(ruleRows(page)).toHaveCount(0);

    // The URL is a normal missing page again and is tracked with a single hit.
    expect((await request.get(path)).status()).toBe(404);
    await openTrackedAndSearch(page, `${prefix}-unblock`, 1);
    await expect(trackedRowFor(page, path)).toHaveCount(1);
    await expect(trackedRowFor(page, path)).toContainText(/\b1\b/);
  });

  test('Umbraco_owned_pattern_is_rejected_with_a_message', async ({ page }) => {
    await page.goto(BLOCKED_PATH);
    await expect(page.getByTestId(MARKS.blockCollection)).toBeVisible({ timeout: 20_000 });

    await page.getByTestId(ADD_RULE_ACTION).click();
    await fillRuleModal(page, '/umbraco/x*');
    await page.getByTestId(MARKS.blockConfirm).click();

    await expect(page.getByTestId(MARKS.blockError)).toBeVisible();
    await expect(page.getByTestId(MARKS.blockError)).not.toBeEmpty();
    await expect(page.getByTestId(MARKS.blockModal)).toBeVisible();

    await page.getByTestId(MARKS.blockCancel).click();
    await expect(page.getByTestId(MARKS.blockModal)).toBeHidden();
  });

  test('a_css_row_can_be_blocked', async ({ page, request }) => {
    const path = `/${prefix}-style.css`;
    expect((await request.get(path)).status()).toBe(404);
    await openTrackedAndSearch(page, `${prefix}-style`, 1);

    const row = trackedRowFor(page, path);
    // A file row has no Create redirect action, so Block is its only (inline) action.
    await expect(row.getByTestId(BLOCK_ACTION)).toHaveCount(1);
    await row.getByTestId(BLOCK_ACTION).click();
    await fillRuleModal(page, path);
    await page.getByTestId(MARKS.blockStatus).locator('select').selectOption('410');
    await page.getByTestId(MARKS.blockConfirm).click();
    await expect(page.getByTestId(MARKS.blockModal)).toBeHidden();

    await expect(trackedRowFor(page, path)).toHaveCount(0);
    const blocked = await statusOf(page, path);
    expect(blocked.status).toBe(410);
    expect(blocked.body).toBe('');

    await unblockAllMatching(page, `${prefix}-style`);
  });
});

/** The modal opened from a row starts with the row's relative path; checks it and keeps the defaults. */
async function fillRuleModalDefaults(page: Page, expectedPattern: string): Promise<void> {
  await expect(page.getByTestId(MARKS.blockModal)).toBeVisible();
  await expect(page.getByTestId(MARKS.blockPattern).locator('input')).toHaveValue(expectedPattern);
  await expect(page.getByTestId(MARKS.blockStatus).locator('select')).toHaveValue('410');
}
