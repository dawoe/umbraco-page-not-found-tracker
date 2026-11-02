import { test, expect } from '@playwright/test';

test('backoffice shows the Page Not Found Tracker placeholder', async ({ page }) => {
  await page.goto('/umbraco/section/settings/dashboard/page-not-found-tracker');
  await expect(page.getByTestId('page-not-found-tracker-placeholder')).toBeVisible();
});
