import { defineConfig, devices } from '@playwright/test';
import * as dotenv from 'dotenv';

dotenv.config({ path: '.env.playwright.local' });

const localBaseUrl = 'https://localhost:7114';
const configuredBaseUrl = process.env.PLAYWRIGHT_BASE_URL?.trim();
const baseURL = configuredBaseUrl || localBaseUrl;
const baseUrlHost = new URL(baseURL).hostname;
const isLoopback = baseUrlHost === 'localhost' || baseUrlHost === '127.0.0.1';
const shouldStartWebServer =
  !configuredBaseUrl || process.env.PLAYWRIGHT_START_SERVER === 'true';
const webServerCommand =
  process.env.PLAYWRIGHT_WEB_SERVER_COMMAND ??
  'dotnet run --project RentConnect/RentConnect.csproj --launch-profile https';

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
    ignoreHTTPSErrors: isLoopback && baseURL.startsWith('https://'),
    trace: 'on-first-retry',
    screenshot: 'only-on-failure',
  },

  projects: [
    {
      name: 'chromium',
      use: { ...devices['Desktop Chrome'] },
    },
  ],

  webServer: shouldStartWebServer
    ? {
        command: webServerCommand,
        url: `${baseURL.replace(/\/$/, '')}/health`,
        ignoreHTTPSErrors: isLoopback && baseURL.startsWith('https://'),
        reuseExistingServer: !process.env.CI,
        timeout: 120_000,
      }
    : undefined,
});
