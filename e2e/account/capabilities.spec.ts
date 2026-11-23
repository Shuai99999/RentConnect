import { test, expect } from '@playwright/test';
import { anyCredentials, logIn } from '../fixtures/auth';

const user = anyCredentials();

test.describe('Authenticated account capabilities', () => {
  test.skip(
    !user,
    'Set PLAYWRIGHT_USER_EMAIL and PLAYWRIGHT_USER_PASSWORD to enable.',
  );

  test.beforeEach(async ({ page }) => {
    await logIn(page, user!);
  });

  test('one account can access owner and renter navigation', async ({ page }) => {
    await expect(page.getByText('Rental workspace')).toBeVisible();
    const navigation = page.getByRole('navigation', { name: 'Primary navigation' });

    for (const linkName of [
      'Dashboard',
      'Find a home',
      'Properties',
      'Applications',
      'Tenancies',
      'Maintenance',
      'Bills',
    ]) {
      await expect(navigation.getByRole('link', { name: linkName })).toBeVisible();
    }
  });

  test('user can open properties and add-property pages', async ({ page }) => {
    await page.goto('/properties');
    await expect(page.getByRole('heading', { name: 'Properties', exact: true })).toBeVisible();
    await expect(page.getByRole('link', { name: /Add property/i })).toBeVisible();

    await page.goto('/properties/new');
    await expect(page.getByRole('heading', { name: 'Add a property' })).toBeVisible();
  });

  test('user can review applications for listings they own', async ({ page }) => {
    await page.goto('/rental-applications');
    await expect(
      page.getByRole('heading', { name: 'Rental Applications' }),
    ).toBeVisible();
  });

  test('user can open tenancy creation', async ({ page }) => {
    await page.goto('/tenancies/new');
    await expect(page.getByRole('heading', { name: 'Create a tenancy' })).toBeVisible();
  });

  test('login preserves the requested authenticated page', async ({ page, context }) => {
    await context.clearCookies();
    await logIn(page, user!, '/properties');

    await expect(page).toHaveURL(/\/properties$/i);
    await expect(page.getByRole('heading', { name: 'Properties', exact: true })).toBeVisible();
  });
});
