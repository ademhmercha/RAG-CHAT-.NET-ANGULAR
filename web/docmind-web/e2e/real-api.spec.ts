import { expect, test } from '@playwright/test';

const PASSWORD = 'Passw0rd-Secure1';

test.describe('against the real API', () => {
  test.skip(!process.env['E2E_REAL_API'], 'Set E2E_REAL_API=1 with the backend running.');

  test('registers, restores the session and signs out', async ({ page }) => {
    const email = `real-${Date.now()}@example.com`;

    await page.goto('/auth/register');
    await page.getByLabel(/^Full name/).fill('Real User');
    await page.getByLabel(/^Email/).fill(email);
    await page.getByLabel(/^Password/).fill(PASSWORD);
    await page.getByRole('button', { name: 'Create account' }).click();
    await expect(page).toHaveURL(/\/chat/);

    await page.reload();
    await expect(page).toHaveURL(/\/chat/);

    await page.getByRole('button', { name: 'Account menu' }).click();
    await page.getByRole('menuitem', { name: 'Sign out' }).click();
    await expect(page).toHaveURL(/\/auth\/login/);
  });
});
