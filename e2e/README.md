# RentConnect E2E tests

These Playwright tests provide a non-destructive browser regression suite for
RentConnect.

## Test organization

```text
e2e/
├─ account/    Capabilities available to every authenticated account
├─ auth/       Login, registration, and protected routes
├─ fixtures/   Shared credentials and login helpers
├─ properties/ Property-management forms
├─ shared/     Pages available to every authenticated account
└─ smoke/      Health, headers, public navigation, and not-found handling
```

Test files end in `.spec.ts`. Files under `fixtures` are reusable support code,
not tests.

## Run locally

```powershell
npm run test:e2e
```

Playwright reuses RentConnect when it is already running at
`https://localhost:7114`. Otherwise, it starts the web project automatically.

The tests that do not require an account run immediately. Authenticated tests
are skipped until dedicated, email-confirmed test accounts are configured in
the Git-ignored `.env.playwright.local` file:

```dotenv
PLAYWRIGHT_USER_EMAIL=user@example.com
PLAYWRIGHT_USER_PASSWORD=your-test-password
```

Every authenticated RentConnect account can both manage its own rental
properties and participate in rentals. The older `PLAYWRIGHT_LANDLORD_*` and
`PLAYWRIGHT_RENTER_*` variable names remain accepted for local compatibility,
but new setups should use `PLAYWRIGHT_USER_*`.

`.env.playwright.example` documents the supported values without containing
real credentials. Do not put real credentials in that example file.

To watch Chromium perform the tests:

```powershell
npm run test:e2e:headed
```

For Playwright's interactive test runner:

```powershell
npm run test:e2e:ui
```

Run only the quick smoke tests or authentication tests:

```powershell
npm run test:e2e:smoke
npm run test:e2e:auth
```

## Run against Azure

Set the deployed application URL for the current PowerShell session, run the
tests, and then remove the variable:

```powershell
$env:PLAYWRIGHT_BASE_URL = "https://your-app.azurewebsites.net"
npm run test:e2e
Remove-Item Env:PLAYWRIGHT_BASE_URL
```

Keep cloud-targeted tests non-destructive unless the deployment uses a
dedicated test database.

## View the last HTML report

```powershell
npm run test:e2e:report
```
