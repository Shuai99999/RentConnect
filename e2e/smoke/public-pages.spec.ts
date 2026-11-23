import { test, expect } from '@playwright/test';

test.describe('Public pages', () => {
  test('login page exposes the main anonymous navigation', async ({ page }) => {
    await page.goto('/Account/Login');

    await expect(page.getByRole('link', { name: 'RentConnect home' })).toBeVisible();
    await expect(page.getByRole('link', { name: 'Log in', exact: true })).toBeVisible();
    await expect(page.getByRole('link', { name: 'Create account' })).toBeVisible();
    await expect(
      page.getByRole('heading', { name: 'Log in', exact: true }),
    ).toBeVisible();
  });

  test('forgot-password page can be reached from login', async ({ page }) => {
    await page.goto('/Account/Login');
    await page.getByRole('link', { name: 'Forgot your password?' }).click();

    await expect(page).toHaveURL(/\/Account\/ForgotPassword$/i);
    await expect(
      page.getByRole('heading', { name: 'Forgot your password?' }),
    ).toBeVisible();
    await expect(page.getByLabel('Email')).toBeVisible();
  });

  test('registration page can be reached from login', async ({ page }) => {
    await page.goto('/Account/Login');
    await page.getByRole('link', { name: 'Register as a new user' }).click();

    await expect(page).toHaveURL(/\/Account\/Register/i);
    await expect(page.getByRole('heading', { name: 'Register', exact: true })).toBeVisible();
  });

  test('unknown routes render the not-found page', async ({ page }) => {
    const response = await page.goto('/page-that-does-not-exist');

    expect(response?.status()).toBe(404);
    await expect(page.getByRole('heading', { name: 'Not Found' })).toBeVisible();
    await expect(page.getByText('content you are looking for does not exist')).toBeVisible();
  });
});
