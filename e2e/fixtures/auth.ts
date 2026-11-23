import { expect, type Page } from '@playwright/test';

export interface TestCredentials {
  email: string;
  password: string;
}

function readCredentials(prefix: string): TestCredentials | undefined {
  const email = process.env[`${prefix}_EMAIL`];
  const password = process.env[`${prefix}_PASSWORD`];

  return email && password ? { email, password } : undefined;
}

export function anyCredentials(): TestCredentials | undefined {
  return (
    readCredentials('PLAYWRIGHT_USER') ??
    readCredentials('PLAYWRIGHT_LANDLORD') ??
    readCredentials('PLAYWRIGHT_RENTER')
  );
}

export async function logIn(
  page: Page,
  credentials: TestCredentials,
  returnUrl?: string,
): Promise<void> {
  const loginUrl = returnUrl
    ? `/Account/Login?returnUrl=${encodeURIComponent(returnUrl)}`
    : '/Account/Login';

  await page.goto(loginUrl);
  await page.getByLabel('Email').fill(credentials.email);
  await page.getByLabel('Password').fill(credentials.password);
  await page.getByRole('button', { name: 'Log in' }).click();

  await expect(page.getByRole('button', { name: 'Log out' })).toBeVisible();

  if (!returnUrl) {
    await expect(page.getByText('Rental workspace')).toBeVisible();
  }
}
