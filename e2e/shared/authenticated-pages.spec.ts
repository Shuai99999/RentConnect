import { test, expect } from '@playwright/test';
import { anyCredentials, logIn } from '../fixtures/auth';

const user = anyCredentials();
const sharedPages = [
  { path: '/listings', heading: 'Find a place that feels right' },
  { path: '/tenancies', heading: 'Tenancies' },
  { path: '/maintenance', heading: 'Maintenance Requests' },
  { path: '/bills', heading: 'Shared Bills' },
  { path: '/notifications', heading: 'Notifications' },
];

test.describe('Shared authenticated pages', () => {
  test.skip(
    !user,
    'Set PLAYWRIGHT_USER_EMAIL and PLAYWRIGHT_USER_PASSWORD to enable.',
  );

  test.beforeEach(async ({ page }) => {
    await logIn(page, user!);
  });

  for (const sharedPage of sharedPages) {
    test(`${sharedPage.path} loads its main content`, async ({ page }) => {
      await page.goto(sharedPage.path);

      await expect(
        page.getByRole('heading', { name: sharedPage.heading, exact: true }),
      ).toBeVisible();
      await expect(page.getByRole('button', { name: 'Log out' })).toBeVisible();
    });
  }

  test('listing search controls are available', async ({ page }) => {
    await page.goto('/listings');

    await expect(page.getByLabel('Where do you want to live?')).toBeVisible();
    await expect(page.getByLabel('Search area')).toHaveValue('0');
    await expect(page.getByRole('button', { name: 'Search homes' })).toBeVisible();
    await expect(page.getByRole('button', { name: 'Use my location' })).toBeVisible();
  });
});
