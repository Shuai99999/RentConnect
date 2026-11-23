import { test, expect } from '@playwright/test';

test.describe('Login', () => {
  test('required fields are validated', async ({ page }) => {
    await page.goto('/Account/Login');
    await page.getByRole('button', { name: 'Log in' }).click();

    const validationSummary = page.getByRole('alert');
    await expect(validationSummary).toContainText('The Email field is required.');
    await expect(validationSummary).toContainText('The Password field is required.');
  });

  test('invalid credentials display a safe error message', async ({ page }) => {
    await page.goto('/Account/Login');
    await page.getByLabel('Email').fill('not-a-user@rentconnect.test');
    await page.getByLabel('Password').fill('NotTheRightPassword123!');
    await page.getByRole('button', { name: 'Log in' }).click();

    await expect(page.getByRole('alert')).toContainText('Invalid login attempt');
    await expect(page).toHaveURL(/\/Account\/Login/i);
  });
});
