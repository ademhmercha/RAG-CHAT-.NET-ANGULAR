import { defineConfig, devices } from '@playwright/test';

const port = process.env['E2E_PORT'] ?? '4200';
const baseURL = process.env['E2E_BASE_URL'] ?? `http://localhost:${port}`;

export default defineConfig({
  testDir: './e2e',
  fullyParallel: true,
  forbidOnly: !!process.env['CI'],
  retries: process.env['CI'] ? 2 : 0,
  workers: process.env['CI'] ? 2 : undefined,
  reporter: process.env['CI'] ? [['github'], ['html', { open: 'never' }]] : [['list']],
  timeout: 30_000,
  expect: { timeout: 8_000 },
  use: {
    baseURL,
    locale: 'en-US',
    trace: 'retain-on-failure',
    screenshot: 'only-on-failure',
  },
  projects: [{ name: 'chromium', use: { ...devices['Desktop Chrome'] } }],
  webServer: process.env['E2E_BASE_URL']
    ? undefined
    : {
        command: 'node scripts/serve.mjs',
        env: { PORT: port },
        url: baseURL,
        reuseExistingServer: !process.env['CI'],
        timeout: 180_000,
      },
});
