export function showStartupFailure(document: Document, error: unknown): void {
  const message = error instanceof Error ? error.message : 'The application could not start.';

  const container = document.createElement('main');
  container.setAttribute('role', 'alert');
  container.style.cssText =
    'display:grid;place-content:center;gap:.5rem;min-height:100dvh;padding:2rem;text-align:center;font-family:system-ui,sans-serif';

  const title = document.createElement('h1');
  title.textContent = 'DocMind could not start';
  title.style.cssText = 'margin:0;font-size:1.5rem';

  const details = document.createElement('p');
  details.textContent = message;
  details.style.cssText = 'margin:0;opacity:.75';

  container.append(title, details);
  document.body.replaceChildren(container);
  console.error(error);
}
