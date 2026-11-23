import { expect, test, type Locator } from '@playwright/test';
import {
  anyCredentials,
  applicantCredentials,
  logIn,
} from '../fixtures/auth';

const owner = anyCredentials();
const applicant = applicantCredentials();
const writesEnabled = process.env.PLAYWRIGHT_ALLOW_WRITES === 'true';

async function fillBlazorField(field: Locator, value: string): Promise<void> {
  // A pending server render can replace an input just after navigation.
  // Retry the user interaction rather than retaining a stale DOM value.
  for (let attempt = 0; attempt < 3; attempt += 1) {
    await field.fill(value);
    await field.press('Tab');

    try {
      await expect(field).toHaveValue(value, { timeout: 1_000 });
      return;
    } catch (whenFieldWasReRendered) {
      if (attempt === 2) {
        throw whenFieldWasReRendered;
      }
    }
  }
}

test.describe('Rental lifecycle', () => {
  test.skip(
    !writesEnabled || !owner || !applicant,
    'Requires two test accounts and PLAYWRIGHT_ALLOW_WRITES=true.',
  );

  test('owner publishes a home, applicant applies, and owner approves', async ({
    browser,
  }, testInfo) => {
    test.slow();

    const unique = `${Date.now()}-${testInfo.workerIndex}`;
    const propertyName = `Playwright Property ${unique}`;
    const spaceName = `Suite ${unique}`;
    const listingTitle = `Playwright Home ${unique}`;
    const applicationMessage = `Automated application ${unique}`;

    const ownerContext = await browser.newContext();
    const applicantContext = await browser.newContext();
    const ownerPage = await ownerContext.newPage();
    const applicantPage = await applicantContext.newPage();

    try {
      await logIn(ownerPage, owner!, '/properties/new');

      // The page is server-rendered before its Blazor circuit becomes
      // interactive. Triggering validation gives the circuit a deterministic
      // readiness signal before values are entered.
      await ownerPage.getByRole('button', { name: 'Add property' }).click();
      await expect(ownerPage.locator('.validation-summary')).toContainText(
        'Enter a property name.',
      );
      await ownerPage.waitForTimeout(1_000);

      await fillBlazorField(
        ownerPage.getByLabel('Street address'),
        '123 Test Avenue',
      );
      await fillBlazorField(ownerPage.getByLabel('City'), 'Edmonton');
      await fillBlazorField(ownerPage.getByLabel('Property name'), propertyName);
      await ownerPage.getByRole('button', { name: 'Add property' }).click();

      await expect(ownerPage).toHaveURL(/\/properties\/[0-9a-f-]+$/i);
      await expect(
        ownerPage.getByRole('heading', { name: propertyName }),
      ).toBeVisible();

      await ownerPage.waitForTimeout(750);
      await ownerPage
        .getByRole('button', { name: 'Add rental space' })
        .click();
      await fillBlazorField(ownerPage.getByLabel('Space name'), spaceName);
      await ownerPage.getByRole('button', { name: 'Add space' }).click();

      const unitCard = ownerPage.locator('article.unit-card').filter({
        has: ownerPage.getByRole('heading', { name: spaceName }),
      });
      await expect(unitCard).toBeVisible();
      await unitCard.getByRole('link', { name: 'List for rent' }).click();

      await fillBlazorField(ownerPage.getByLabel('Title'), listingTitle);
      await fillBlazorField(
        ownerPage.getByLabel('Description'),
        'A Playwright-created home for an end-to-end test.',
      );
      await fillBlazorField(
        ownerPage.getByLabel('Total monthly rent'),
        '1450',
      );
      await ownerPage
        .getByRole('button', { name: 'Create and publish' })
        .click();

      await expect(ownerPage).toHaveURL(/\/listings\/[0-9a-f-]+$/i);
      await expect(
        ownerPage.getByRole('heading', { name: listingTitle }),
      ).toBeVisible();
      await expect(ownerPage.getByText('This is your listing')).toBeVisible();
      const listingPath = new URL(ownerPage.url()).pathname;

      await logIn(applicantPage, applicant!, listingPath);
      await fillBlazorField(
        applicantPage.getByLabel(/Message to the landlord/),
        applicationMessage,
      );
      await applicantPage
        .getByRole('button', { name: 'Apply for this home' })
        .click();
      await expect(applicantPage.getByText('Application pending')).toBeVisible();

      await ownerPage.goto('/rental-applications');
      const applicationCard = ownerPage
        .locator('article.application-card')
        .filter({ hasText: listingTitle });
      await expect(applicationCard).toContainText(applicant!.email);
      await expect(applicationCard).toContainText('Pending');
      await applicationCard.getByRole('button', { name: 'Approve' }).click();

      const confirmation = ownerPage.getByRole('dialog', {
        name: 'Approve Rental Application?',
      });
      await confirmation
        .getByRole('button', { name: 'Confirm Approval' })
        .click();

      await expect(ownerPage).toHaveURL(/\/tenancies\/[0-9a-f-]+$/i);
      await expect(
        ownerPage.getByRole('heading', { name: propertyName }),
      ).toBeVisible();
      await expect(ownerPage.getByText('Manager View')).toBeVisible();
      const tenancyPath = new URL(ownerPage.url()).pathname;

      await applicantPage.goto(tenancyPath);
      await expect(
        applicantPage.getByRole('heading', { name: propertyName }),
      ).toBeVisible();
      await expect(applicantPage.getByText('Resident View')).toBeVisible();
      await expect(applicantPage.getByText(spaceName)).toBeVisible();

      await ownerPage.goto('/bills/new');
      await expect(
        ownerPage.getByRole('heading', { name: 'New Shared Bill' }),
      ).toBeVisible();
      await ownerPage.getByLabel('Recurring bill').check();
      await expect(ownerPage.getByLabel('Repeat')).toHaveValue('1');
      await expect(ownerPage.getByLabel(/End date/)).toBeVisible();
    } finally {
      await ownerContext.close();
      await applicantContext.close();
    }
  });
});
