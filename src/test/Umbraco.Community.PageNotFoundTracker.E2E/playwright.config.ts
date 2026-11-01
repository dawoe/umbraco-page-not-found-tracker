import 'dotenv/config';
import { defineConfig, devices } from '@playwright/test';
import { dirname, join } from 'path';
import { fileURLToPath } from 'url';

const __dirname = dirname(fileURLToPath(import.meta.url));

export const STORAGE_STATE = join(__dirname, '.auth/user.json');

const umbracoUrl = process.env.UMBRACO_URL ?? 'https://localhost:44401';

// The Umbraco testhelpers read the auth token and URL from these variables.
process.env.STORAGE_STATE_PATH = STORAGE_STATE;
process.env.URL = umbracoUrl;

export default defineConfig({
  testDir: './tests',
  timeout: 60 * 1000,
  expect: { timeout: 10 * 1000 },
  fullyParallel: false,
  workers: 1,
  retries: process.env.CI ? 1 : 0,
  reporter: process.env.CI ? [['list'], ['github']] : 'list',
  use: {
    baseURL: umbracoUrl,
    ignoreHTTPSErrors: true,
    trace: 'retain-on-failure',
    // Umbraco marks elements with data-mark, not data-testid.
    testIdAttribute: 'data-mark',
  },
  projects: [
    {
      name: 'setup',
      testMatch: '**/*.setup.ts',
    },
    {
      name: 'e2e',
      testMatch: '**/*.spec.ts',
      dependencies: ['setup'],
      use: {
        ...devices['Desktop Chrome'],
        ignoreHTTPSErrors: true,
        storageState: STORAGE_STATE,
      },
    },
  ],
});
