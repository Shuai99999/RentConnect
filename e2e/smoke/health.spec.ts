import { test, expect } from '@playwright/test';

test.describe('Application health and security headers', () => {
  test('health endpoint reports a healthy application', async ({ request }) => {
    const response = await request.get('/health');

    expect(response.ok()).toBeTruthy();
    await expect(response.json()).resolves.toEqual({ status: 'healthy' });
  });

  test('responses include the configured security headers', async ({ request }) => {
    const response = await request.get('/health');
    const headers = response.headers();

    expect(headers['x-content-type-options']).toBe('nosniff');
    expect(headers['x-frame-options']).toBe('DENY');
    expect(headers['referrer-policy']).toBe('strict-origin-when-cross-origin');
  });
});
