import type { Page } from '@playwright/test';
import { CITATION } from './fixtures/mock-api';
import { PASSWORD, expect, pdf, test, uniqueEmail } from './fixtures/test';

async function register(page: Page, email = uniqueEmail()): Promise<string> {
  await page.goto('/auth/register');
  await page.getByLabel(/^Full name/).fill('Ada Lovelace');
  await page.getByLabel(/^Email/).fill(email);
  await page.getByLabel(/^Password/).fill(PASSWORD);
  await page.getByRole('button', { name: 'Create account' }).click();
  await expect(page).toHaveURL(/\/chat/);
  return email;
}

async function signOut(page: Page): Promise<void> {
  await page.getByRole('button', { name: 'Account menu' }).click();
  await page.getByRole('menuitem', { name: 'Sign out' }).click();
  await expect(page).toHaveURL(/\/auth\/login/);
}

test.describe('authentication', () => {
  test('redirects visitors to the login page and back after signing in', async ({ page }) => {
    await page.goto('/documents');

    await expect(page).toHaveURL(/\/auth\/login\?returnUrl=%2Fdocuments/);
  });

  test('registers, signs out and signs in again', async ({ page }) => {
    const email = await register(page);
    await expect(
      page.getByRole('heading', { name: 'Ask anything about your documents' }),
    ).toBeVisible();

    await signOut(page);

    await page.getByLabel(/^Email/).fill(email);
    await page.getByLabel(/^Password/).fill(PASSWORD);
    await page.getByRole('button', { name: 'Sign in' }).click();

    await expect(page).toHaveURL(/\/chat/);
  });

  test('keeps the session across a reload through the refresh cookie', async ({ page }) => {
    await register(page);

    await page.reload();

    await expect(page).toHaveURL(/\/chat/);
    await expect(page.getByRole('button', { name: 'Account menu' })).toBeVisible();
  });

  test('explains wrong credentials and an email already in use', async ({ page }) => {
    await page.goto('/auth/login');
    await page.getByLabel(/^Email/).fill('nobody@example.com');
    await page.getByLabel(/^Password/).fill(PASSWORD);
    await page.getByRole('button', { name: 'Sign in' }).click();

    await expect(page.getByRole('alert')).toContainText('The email or password is incorrect.');

    await page.goto('/auth/register');
    await page.getByLabel(/^Full name/).fill('Taken');
    await page.getByLabel(/^Email/).fill('taken@example.com');
    await page.getByLabel(/^Password/).fill(PASSWORD);
    await page.getByRole('button', { name: 'Create account' }).click();

    await expect(page.getByText('An account with this email already exists.')).toBeVisible();
  });

  test('validates the password policy before submitting', async ({ page, api }) => {
    await page.goto('/auth/register');
    await page.getByLabel(/^Full name/).fill('Ada');
    await page.getByLabel(/^Email/).fill(uniqueEmail());
    await page.getByLabel(/^Password/).fill('short');
    await page.getByRole('button', { name: 'Create account' }).click();

    await expect(page.getByText('At least 10 characters', { exact: true })).toBeVisible();
    await expect(page.getByText('Use at least 10 characters.')).toBeVisible();
    expect(api.requests).not.toContain('POST /auth/register');
  });
});

test.describe('documents', () => {
  test('uploads a document and follows its status live', async ({ page }) => {
    await register(page);
    await page.getByRole('link', { name: 'Documents' }).click();

    await page.getByTestId('file-input').setInputFiles(pdf('contract.pdf'));

    const row = page.getByTestId('document-row').filter({ hasText: 'contract.pdf' });
    await expect(row).toBeVisible();
    await expect(row).toContainText('Ready');
    await expect(page.getByTestId('upload-item')).toContainText('Uploaded');
  });

  test('rejects unsupported files on the client', async ({ page, api }) => {
    await register(page);
    await page.goto('/documents');

    await page.getByTestId('file-input').setInputFiles({
      name: 'photo.png',
      mimeType: 'image/png',
      buffer: Buffer.from('png'),
    });

    await expect(page.getByTestId('upload-item')).toContainText(
      'Only PDF and DOCX files are supported.',
    );
    expect(api.requests).not.toContain('POST /documents');
  });

  test('deletes a document after confirmation', async ({ page }) => {
    await register(page);
    await page.goto('/documents');
    await page.getByTestId('file-input').setInputFiles(pdf('old.pdf'));
    await expect(page.getByTestId('document-row')).toContainText('old.pdf');

    await page.getByRole('button', { name: 'Delete old.pdf' }).click();
    await page.getByRole('dialog').getByRole('button', { name: 'Delete' }).click();

    await expect(page.getByText('No documents yet')).toBeVisible();
  });
});

test.describe('chat', () => {
  test('answers with streamed markdown and opens the cited source', async ({ page }) => {
    await register(page);
    await page.goto('/documents');
    await page.getByTestId('file-input').setInputFiles(pdf('contract.pdf'));
    await expect(page.getByTestId('document-row')).toContainText('Ready');

    await page.getByRole('link', { name: 'Chat' }).click();
    await page.getByRole('button', { name: 'Summarize the key points of my documents' }).click();

    await expect(page).toHaveURL(/\/chat\/conversation-/);
    await expect(page.locator('.dm-markdown strong')).toHaveText('12 months');

    await page.getByRole('button', { name: /^Open source 1: contract\.pdf/ }).click();

    const panel = page.getByTestId('citation-panel');
    await expect(panel).toContainText('contract.pdf');
    await expect(panel).toContainText(CITATION.excerpt);

    await panel.getByRole('button', { name: 'Close source panel' }).click();
    await expect(panel).toBeHidden();
  });

  test('lists, renames and deletes conversations', async ({ page }) => {
    await register(page);
    await page.getByLabel('Your question').fill('What is the notice period?');
    await page.getByLabel('Your question').press('Enter');

    const item = page.getByRole('link', { name: 'What is the notice period?' });
    await expect(item).toBeVisible();

    await page.getByRole('button', { name: /^Actions for/ }).click();
    await page.getByRole('menuitem', { name: 'Rename' }).click();
    await page.getByLabel('Title').fill('Notice period');
    await page.getByRole('button', { name: 'Rename' }).click();
    await expect(page.getByRole('link', { name: 'Notice period' })).toBeVisible();

    await page.getByRole('button', { name: /^Actions for/ }).click();
    await page.getByRole('menuitem', { name: 'Delete' }).click();
    await page.getByRole('dialog').getByRole('button', { name: 'Delete' }).click();

    await expect(page.getByRole('link', { name: 'Notice period' })).toHaveCount(0);
    await expect(page).toHaveURL(/\/chat$/);
  });

  test('sends with Enter and keeps Shift+Enter for new lines', async ({ page }) => {
    await register(page);
    const box = page.getByLabel('Your question');

    await box.fill('line one');
    await box.press('Shift+Enter');
    await box.pressSequentially('line two');
    await expect(box).toHaveValue('line one\nline two');

    await box.press('Enter');

    await expect(page.locator('.user-text')).toHaveText('line one\nline two');
  });
});

test.describe('preferences', () => {
  test('switches language and theme and remembers them', async ({ page }) => {
    await register(page);
    await page.goto('/settings');

    await page.getByRole('radio', { name: 'Français' }).click();
    await expect(page.getByRole('heading', { name: 'Paramètres' })).toBeVisible();

    await page.getByRole('radio', { name: /Sombre/ }).click();
    await expect(page.locator('html')).toHaveClass(/theme-dark/);

    await page.reload();

    await expect(page.getByRole('heading', { name: 'Paramètres' })).toBeVisible();
    await expect(page.locator('html')).toHaveClass(/theme-dark/);
  });

  test('shows the profile of the signed-in user', async ({ page }) => {
    const email = await register(page);
    await page.goto('/settings');

    await expect(page.getByTestId('profile-name')).toHaveText('Ada Lovelace');
    await expect(page.getByTestId('profile-email')).toHaveText(email);
  });
});

test.describe('responsive layout', () => {
  test.use({ viewport: { width: 390, height: 800 } });

  test('opens the navigation drawer on small screens', async ({ page }) => {
    await register(page);

    await page.getByRole('button', { name: 'Open navigation' }).click();
    await page.getByRole('link', { name: 'Documents' }).click();

    await expect(page).toHaveURL(/\/documents/);
    await expect(page.getByRole('heading', { name: 'Documents', level: 1 })).toBeVisible();
  });
});

test('shows a friendly page for unknown routes', async ({ page }) => {
  await register(page);
  await page.goto('/nope');

  await expect(page.getByRole('heading', { name: 'Page not found' })).toBeVisible();
});
