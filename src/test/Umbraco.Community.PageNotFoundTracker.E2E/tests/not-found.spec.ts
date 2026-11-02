import { test, expect, type Page } from '@playwright/test';

// These tests hit the public site of the single-site profile. They need no backoffice login,
// but run in the shared e2e project, which only adds the stored auth cookies.
// The 404 page is the Clean starter kit Error page (h1 "Page not found") rendered in the
// site layout, so the main navigation proves a real template rendered.

async function expectNotFoundPage(page: Page, path: string) {
  const response = await page.goto(path);
  expect(response?.status()).toBe(404);
  await expect(page.locator('h1')).toHaveText('Page not found');
  await expect(page.locator('nav#mainNav')).toBeVisible();
  // The browser URL is the one that was requested: no redirect happened.
  expect(new URL(page.url()).pathname + new URL(page.url()).search).toBe(path);
}

test.describe('404 handling', () => {
  test('a missing page returns 404 with the 404 page', async ({ page }) => {
    await expectNotFoundPage(page, '/this-page-does-not-exist');
  });

  test('a missing file returns 404 with the 404 page', async ({ page }) => {
    await expectNotFoundPage(page, '/missing-file.pdf');
  });

  test('a missing media file with a query string returns 404 with the 404 page', async ({ page }) => {
    await expectNotFoundPage(page, '/media/missing.png?x=1');
  });

  test('an existing page returns 200 and is not the 404 page', async ({ page }) => {
    const response = await page.goto('/about');
    expect(response?.status()).toBe(200);
    await expect(page.locator('h1')).not.toHaveText('Page not found');
    await expect(page.locator('nav#mainNav')).toBeVisible();
  });

  test('the backoffice is unaffected', async ({ request }) => {
    const response = await request.get('/umbraco/management/api/v1/server/status');
    expect(response.status()).toBe(200);
  });
});
