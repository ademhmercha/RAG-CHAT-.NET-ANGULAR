import { HttpTestingController } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { authResponse } from '../../../testing/auth-fixtures';
import { provideApiTesting } from '../../../testing/api-testing';
import { AUTH_NOW, AuthStore, sanitizeReturnUrl } from './auth.store';

describe('AuthStore', () => {
  let http: HttpTestingController;
  let store: InstanceType<typeof AuthStore>;
  let clock: number;

  beforeEach(() => {
    clock = Date.parse('2030-01-01T00:00:00Z');
    TestBed.configureTestingModule({
      providers: [
        provideApiTesting(),
        provideRouter([]),
        { provide: AUTH_NOW, useValue: () => clock },
      ],
    });
    http = TestBed.inject(HttpTestingController);
    store = TestBed.inject(AuthStore);
  });

  afterEach(() => http.verify());

  it('starts unknown and becomes anonymous when no session can be restored', async () => {
    expect(store.status()).toBe('unknown');

    const restored = store.restoreSession();
    http.expectOne('/api/v1/auth/refresh').flush({}, { status: 401, statusText: 'Unauthorized' });
    await restored;

    expect(store.status()).toBe('anonymous');
    expect(store.isAuthenticated()).toBe(false);
  });

  it('restores a session from the refresh cookie', async () => {
    const restored = store.restoreSession();
    http.expectOne('/api/v1/auth/refresh').flush(authResponse());
    await restored;

    expect(store.isAuthenticated()).toBe(true);
    expect(store.user()?.email).toBe('ada@example.com');
    expect(store.initials()).toBe('AL');
  });

  it('logs in and keeps the token in memory', async () => {
    const pending = store.login({ email: 'ada@example.com', password: 'Passw0rdPassw0rd' });
    const request = http.expectOne('/api/v1/auth/login');
    request.flush(authResponse('abc'));
    await pending;

    expect(request.request.method).toBe('POST');
    expect(store.accessToken()).toBe('abc');
  });

  it('registers then signs in', async () => {
    const pending = store.register({
      email: 'ada@example.com',
      password: 'Passw0rdPassw0rd',
      displayName: 'Ada Lovelace',
    });
    http.expectOne('/api/v1/auth/register').flush({});
    await Promise.resolve();
    http.expectOne('/api/v1/auth/login').flush(authResponse());
    await pending;

    expect(store.isAuthenticated()).toBe(true);
  });

  it('shares a single refresh between concurrent callers', async () => {
    const login = store.login({ email: 'a@b.co', password: 'x' });
    http.expectOne('/api/v1/auth/login').flush(authResponse('old', '2030-01-01T00:00:10Z'));
    await login;

    const first = store.getValidToken();
    const second = store.getValidToken();
    http.expectOne('/api/v1/auth/refresh').flush(authResponse('new'));

    expect(await Promise.all([first, second])).toEqual(['new', 'new']);
  });

  it('returns the cached token while it is far from expiry', async () => {
    const login = store.login({ email: 'a@b.co', password: 'x' });
    http.expectOne('/api/v1/auth/login').flush(authResponse('cached'));
    await login;

    expect(await store.getValidToken()).toBe('cached');
  });

  it('does not refresh when anonymous', async () => {
    expect(await store.getValidToken()).toBeNull();
  });

  it('reuses a token that was already renewed after a 401', async () => {
    const login = store.login({ email: 'a@b.co', password: 'x' });
    http.expectOne('/api/v1/auth/login').flush(authResponse('fresh'));
    await login;

    expect(await store.recoverFromUnauthorized('stale')).toBe('fresh');
  });

  it('clears the session when the refresh is rejected', async () => {
    const login = store.login({ email: 'a@b.co', password: 'x' });
    http.expectOne('/api/v1/auth/login').flush(authResponse('t', '2030-01-01T00:00:10Z'));
    await login;

    const token = store.getValidToken();
    http.expectOne('/api/v1/auth/refresh').flush({}, { status: 401, statusText: 'Unauthorized' });

    expect(await token).toBeNull();
    expect(store.status()).toBe('anonymous');
    expect(store.user()).toBeNull();
  });

  it('keeps the session when the refresh fails because of the network', async () => {
    const login = store.login({ email: 'a@b.co', password: 'x' });
    http.expectOne('/api/v1/auth/login').flush(authResponse('t', '2030-01-01T00:00:10Z'));
    await login;

    const token = store.getValidToken();
    http.expectOne('/api/v1/auth/refresh').error(new ProgressEvent('error'));

    expect(await token).toBeNull();
    expect(store.status()).toBe('authenticated');
  });

  it('signs out locally even if the server is unreachable', async () => {
    const router = TestBed.inject(Router);
    const navigate = vi.spyOn(router, 'navigateByUrl').mockResolvedValue(true);

    const login = store.login({ email: 'a@b.co', password: 'x' });
    http.expectOne('/api/v1/auth/login').flush(authResponse());
    await login;

    const done = store.logout();
    http.expectOne('/api/v1/auth/logout').error(new ProgressEvent('error'));
    await done;

    expect(store.status()).toBe('anonymous');
    expect(navigate).toHaveBeenCalledWith('/auth/login');
  });
});

describe('sanitizeReturnUrl', () => {
  it.each([
    ['/documents', '/documents'],
    ['/chat?c=1', '/chat?c=1'],
    ['//evil.example', null],
    ['https://evil.example', null],
    ['/\\evil', null],
    ['/auth/login', null],
    ['', null],
    [null, null],
  ])('%s -> %s', (input, expected) => {
    expect(sanitizeReturnUrl(input)).toBe(expected);
  });
});
