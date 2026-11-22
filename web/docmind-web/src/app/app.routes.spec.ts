import type { Route } from '@angular/router';
import { routes } from './app.routes';

async function resolveChildren(route: Route | undefined): Promise<Route[]> {
  const loaded = await route?.loadChildren?.();
  return Array.isArray(loaded) ? loaded : [];
}

describe('application routes', () => {
  const shell = routes.find((route) => route.component);
  const features = ['chat', 'documents', 'settings'];

  it('loads every feature lazily', async () => {
    for (const path of features) {
      const route = shell?.children?.find((child) => child.path === path);

      expect(route?.loadChildren, `${path} must be lazy`).toBeTypeOf('function');
      expect((await resolveChildren(route)).length).toBeGreaterThan(0);
    }

    const auth = routes.find((route) => route.path === 'auth');
    const [layout] = await resolveChildren(auth);
    const authPaths = (layout?.children ?? []).map((route) => route.path);

    expect(authPaths).toEqual(['', 'login', 'register']);
  });

  it('opens on the chat and ends with a not-found route', () => {
    expect(shell?.children?.[0]).toMatchObject({ path: '', redirectTo: 'chat' });
    expect(routes.at(-1)?.path).toBe('**');
  });

  it('gives every page a translatable title', async () => {
    const pages = (
      await Promise.all(
        features.map((path) => resolveChildren(shell?.children?.find((c) => c.path === path))),
      )
    ).flat();

    expect(pages.every((route) => typeof route.title === 'string')).toBe(true);
  });
});
