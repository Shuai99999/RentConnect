import { defineConfig, devices } from '@playwright/test';
import * as dotenv from 'dotenv';

dotenv.config({ path: '.env.playwright.local' });

const localBaseUrl = 'https://localhost:7114';
const baseURL = process.env.PLAYWRIGHT_BASE_URL ?? localBaseUrl;
const isLocalRun = !process.env.PLAYWRIGHT_BASE_URL;

export default defineConfig({
  testDir: './e2e',
  fullyParallel: false,
  forbidOnly: !!process.env.CI,
  retries: process.env.CI ? 1 : 0,
  workers: 1,
  reporter: [
    ['list'],
    ['html', { open: 'never' }],
  ],
  use: {
    baseURL,
    ignoreHTTPSErrors: isLocalRun,
    trace: 'on-first-retry',
    screenshot: 'only-on-failure',
  },

  projects: [
    {
      name: 'chromium',
      use: { ...devices['Desktop Chrome'] },
    },
  ],

  webServer: isLocalRun
    ? {
        command:
          'dotnet run --project RentConnect/RentConnect.csproj --launch-profile https',
        url: `${localBaseUrl}/health`,
        ignoreHTTPSErrors: true,
        reuseExistingServer: true,
        timeout: 120_000,
      }
    : undefined,
});
