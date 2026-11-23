import { test, expect } from '@playwright/test';
import { anyCredentials, logIn } from '../fixtures/auth';

const user = anyCredentials();

test.describe('Property form', () => {
  test.skip(
    !user,
    'Set PLAYWRIGHT_USER_EMAIL and PLAYWRIGHT_USER_PASSWORD to enable.',
  );

  test.beforeEach(async ({ page }) => {
    await logIn(page, user!);
    await page.goto('/properties/new');
  });

  test('shows the fields needed to create a property', async ({ page }) => {
    await expect(page.getByRole('heading', { name: 'Add a property' })).toBeVisible();
    await expect(page.getByLabel('Property name')).toBeVisible();
    await expect(page.getByLabel('Street address')).toBeVisible();
    await expect(page.getByLabel('City')).toBeVisible();
    await expect(page.getByLabel('Country')).toHaveValue('CA');
  });

  test('validates required fields without creating a property', async ({ page }) => {
    await page.getByRole('button', { name: 'Add property' }).click();

    const validationSummary = page.locator('.validation-summary');
    await expect(validationSummary).toContainText('Enter a property name.');
    await expect(validationSummary).toContainText('Enter a street address.');
    await expect(validationSummary).toContainText('Enter a city.');
  });
});
