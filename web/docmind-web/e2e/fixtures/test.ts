import { expect, test as base } from '@playwright/test';
import { StubApi } from './mock-api';

export const PASSWORD = 'Passw0rd-Secure1';

interface Fixtures {
  api: StubApi;
}

export const test = base.extend<Fixtures>({
  api: [
    async ({ page }, use) => {
      await page.addInitScript(() => {
        if (!localStorage.getItem('dm.language')) {
          localStorage.setItem('dm.language', 'en');
        }
      });

      const api = new StubApi();
      await api.install(page);
      await use(api);
    },
    { auto: true },
  ],
});

export { expect };

export function uniqueEmail(): string {
  return `user-${Date.now()}-${Math.floor(Math.random() * 1e6)}@example.com`;
}

export const pdf = (name: string) => ({
  name,
  mimeType: 'application/pdf',
  buffer: Buffer.from('%PDF-1.4\n1 0 obj<<>>endobj\ntrailer<<>>\n%%EOF'),
});
