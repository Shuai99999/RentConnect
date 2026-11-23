import { test, expect } from '@playwright/test';

const id = '00000000-0000-0000-0000-000000000001';
const protectedRoutes = [
  '/',
  '/properties',
  '/properties/new',
  `/properties/${id}`,
  '/tenancies',
  '/tenancies/new',
  `/tenancies/${id}`,
  '/invitations/example-token',
  '/listings',
  '/listings/manage',
  `/listings/manage/${id}`,
  `/listings/${id}`,
  '/rental-applications',
  '/maintenance',
  '/maintenance/new',
  `/maintenance/${id}`,
  '/bills',
  '/bills/new',
  `/bills/${id}`,
  `/bills/${id}/payment-success`,
  '/notifications',
  '/Account/Manage',
];

test.describe('Anonymous route protection', () => {
  for (const route of protectedRoutes) {
    test(`${route} redirects to login`, async ({ page }) => {
      await page.goto(route);

      await expect(page).toHaveURL(/\/Account\/Login\?returnUrl=/i);
      await expect(
        page.getByRole('heading', { name: 'Log in', exact: true }),
      ).toBeVisible();
    });
  }
});
