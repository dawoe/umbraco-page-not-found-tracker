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
  stateEmpty: 'pnft-state-empty',
  stateEmptyFiltered: 'pnft-state-empty-filtered',
  blockCollection: 'pnft-block-collection',
  blockSearch: 'pnft-block-search',
  blockRow: 'pnft-block-row',
  blockStateEmpty: 'pnft-block-state-empty',
  blockStateEmptyFiltered: 'pnft-block-state-empty-filtered',
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

/**
 * Waits until the blocked list has settled for `term`: every shown rule contains the term, and when there are none
 * the empty (filtered) state is shown. An unfiltered or still loading list never satisfies this. Returns the patterns.
 */
async function settledRulePatterns(page: Page, term: string): Promise<string[]> {
  let patterns: string[] = [];
  await expect
    .poll(
      async () => {
        // The pattern is the first titled span of a row (other cells carry titles too).
        patterns = await ruleRows(page).evaluateAll((els) =>
          els.map((e) => e.querySelector('span[title]')?.getAttribute('title') ?? ''),
        );
        if (patterns.length === 0) {
          return (
            (await page.getByTestId(MARKS.blockStateEmptyFiltered).count()) +
              (await page.getByTestId(MARKS.blockStateEmpty).count()) >
            0
          );
        }
        return patterns.every((pattern) => pattern.includes(term));
      },
      { timeout: 20_000, message: `blocked list settled for "${term}"` },
    )
    .toBe(true);
  return patterns;
}

/** Opens the Blocked URLs tab, searches for `term` and waits until the list has settled. */
async function openBlockedAndSearch(page: Page, term: string): Promise<string[]> {
  let patterns: string[] = [];
  await expect(async () => {
    await page.goto(BLOCKED_PATH);
    await expect(page.getByTestId(MARKS.blockCollection)).toBeVisible({ timeout: 20_000 });
    await page.getByTestId(MARKS.blockSearch).locator('input').fill(term);
    patterns = await settledRulePatterns(page, term);
  }).toPass({ timeout: 90_000, intervals: [1_000, 2_000] });
  return patterns;
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
    // Only rows whose pattern contains the term are ever touched, whatever the list shows.
    const row = ruleRows(page)
      .filter({ has: page.locator(`span[title*="${term}"]`) })
      .first();
    if ((await row.count()) === 0) {
      await settledRulePatterns(page, term);
      return;
    }
    const id = await row.getAttribute('data-unique');
    await row.getByTestId(UNBLOCK_ACTION).click();
    await page.locator('umb-confirm-modal #confirm').click();
    await expect(page.locator('umb-confirm-modal')).toBeHidden();
    // Wait for the reload to drop this rule before looking at the list again.
    await expect(page.locator(`[data-mark="${MARKS.blockRow}"][data-unique="${id}"]`)).toHaveCount(0);
  }
}

/** Asserts that no rule contains `term` (a loaded, filtered list shows the empty state). */
async function expectNoRulesMatching(page: Page, term: string): Promise<void> {
  expect(await openBlockedAndSearch(page, term)).toEqual([]);
  await expect(ruleRows(page)).toHaveCount(0);
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
    await page.waitForTimeout(4_000); // more than the flush interval: a tracked hit would be written by now
    await page.goto(DASHBOARD_PATH);
    await expect(page.getByTestId(MARKS.collection)).toBeVisible({ timeout: 20_000 });
    await page.getByTestId(MARKS.search).locator('input').fill(`${prefix}-block`);
    // Loaded and filtered: the empty states only show once the list answered for this search.
    await expect(page.getByTestId(MARKS.stateEmptyFiltered).or(page.getByTestId(MARKS.stateEmpty))).toBeVisible();
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
    await expectNoRulesMatching(page, `${prefix}-unblock`);

    // The URL is a normal missing page again and is tracked with a single hit.
    expect((await request.get(path)).status()).toBe(404);
    await openTrackedAndSearch(page, `${prefix}-unblock`, 1);
    await expect(trackedRowFor(page, path)).toHaveCount(1);
    // Hits is the 7th cell (url, domain, culture, type, first seen, last seen, hits).
    await expect(trackedRowFor(page, path).locator('uui-table-cell').nth(6)).toHaveText(/^\s*1\s*$/);
  });

  test('Umbraco_owned_pattern_is_rejected_with_a_message', async ({ page }) => {
    await page.goto(BLOCKED_PATH);
    await expect(page.getByTestId(MARKS.blockCollection)).toBeVisible({ timeout: 20_000 });

    await page.getByTestId(ADD_RULE_ACTION).click();
    await fillRuleModal(page, '/umbraco/x*');
    await page.getByTestId(MARKS.blockConfirm).click();

    await expect(page.getByTestId(MARKS.blockError)).toBeVisible();
    await expect(page.getByTestId(MARKS.blockError)).toContainText('/umbraco');
    await expect(page.getByTestId(MARKS.blockModal)).toBeVisible();

    await page.getByTestId(MARKS.blockCancel).click();
    await expect(page.getByTestId(MARKS.blockModal)).toBeHidden();

    // Nothing was saved for that pattern.
    await expectNoRulesMatching(page, 'umbraco/x');
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
