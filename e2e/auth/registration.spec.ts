import { test, expect } from '@playwright/test';

test.describe('Registration', () => {
  test.beforeEach(async ({ page }) => {
    await page.goto('/Account/Register');
  });

  test('does not force a permanent account type', async ({ page }) => {
    await expect(page.getByLabel('Account type')).toHaveCount(0);
    await expect(
      page.getByRole('button', { name: 'Register', exact: true }),
    ).toBeVisible();
  });

  test('required fields are validated without creating an account', async ({ page }) => {
    await page.getByRole('button', { name: 'Register', exact: true }).click();

    const validationSummary = page.getByRole('alert');
    await expect(validationSummary).toContainText('The Display name field is required.');
    await expect(validationSummary).toContainText('The Email field is required.');
    await expect(validationSummary).toContainText('The Password field is required.');
  });

  test('password confirmation must match', async ({ page }) => {
    await page.getByLabel('Display name').fill('Playwright User');
    await page.getByLabel('Email').fill('playwright-validation@rentconnect.test');
    await page.getByLabel('Password', { exact: true }).fill('ValidPassword123!');
    await page.getByLabel('Confirm Password').fill('DifferentPassword123!');
    await page.getByRole('button', { name: 'Register', exact: true }).click();

    await expect(page.getByRole('alert')).toContainText(
      'The password and confirmation password do not match.',
    );
  });
});
