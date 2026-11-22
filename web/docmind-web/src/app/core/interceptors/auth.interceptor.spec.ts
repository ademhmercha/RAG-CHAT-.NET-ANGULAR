import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { authResponse } from '../../../testing/auth-fixtures';
import { APP_CONFIG } from '../config/app-config';
import { AuthStore } from '../auth/auth.store';
import { authInterceptor } from './auth.interceptor';

describe('authInterceptor', () => {
  let http: HttpClient;
  let controller: HttpTestingController;
  let store: InstanceType<typeof AuthStore>;

  beforeEach(async () => {
    TestBed.configureTestingModule({
      providers: [
        { provide: APP_CONFIG, useValue: { apiBaseUrl: '', environment: 'test' } },
        provideHttpClient(withInterceptors([authInterceptor])),
        provideHttpClientTesting(),
        provideRouter([{ path: 'auth/login', children: [] }]),
      ],
    });
    http = TestBed.inject(HttpClient);
    controller = TestBed.inject(HttpTestingController);
    store = TestBed.inject(AuthStore);

    const login = store.login({ email: 'a@b.co', password: 'x' });
    controller.expectOne('/api/v1/auth/login').flush(authResponse('token-1'));
    await login;
  });

  afterEach(() => controller.verify());

  function nextRequest(url: string) {
    return vi.waitFor(() => controller.expectOne(url));
  }

  it('adds the bearer token and credentials to API calls', async () => {
    const result = firstValueFrom(http.get('/api/v1/documents'));
    const request = await nextRequest('/api/v1/documents');

    expect(request.request.headers.get('Authorization')).toBe('Bearer token-1');
    expect(request.request.withCredentials).toBe(true);
    request.flush({});
    await result;
  });

  it('leaves foreign origins untouched', async () => {
    const result = firstValueFrom(http.get('https://other.example/data'));
    const request = controller.expectOne('https://other.example/data');

    expect(request.request.headers.has('Authorization')).toBe(false);
    request.flush({});
    await result;
  });

  it('refreshes once after a 401 and replays the request', async () => {
    const result = firstValueFrom(http.get('/api/v1/documents'));
    const first = await nextRequest('/api/v1/documents');
    first.flush({}, { status: 401, statusText: 'Unauthorized' });

    const refresh = await nextRequest('/api/v1/auth/refresh');
    refresh.flush(authResponse('token-2'));

    const replay = await nextRequest('/api/v1/documents');
    expect(replay.request.headers.get('Authorization')).toBe('Bearer token-2');
    replay.flush({ ok: true });

    expect(await result).toEqual({ ok: true });
  });

  it('shares one refresh between simultaneous 401 responses', async () => {
    const a = firstValueFrom(http.get('/api/v1/a'));
    const b = firstValueFrom(http.get('/api/v1/b'));
    const requestA = await nextRequest('/api/v1/a');
    const requestB = await nextRequest('/api/v1/b');

    requestA.flush({}, { status: 401, statusText: 'Unauthorized' });
    requestB.flush({}, { status: 401, statusText: 'Unauthorized' });

    const refresh = await nextRequest('/api/v1/auth/refresh');
    refresh.flush(authResponse('token-2'));

    (await nextRequest('/api/v1/a')).flush('a');
    (await nextRequest('/api/v1/b')).flush('b');

    expect(await Promise.all([a, b])).toEqual(['a', 'b']);
  });

  it('gives up when the replay is rejected again', async () => {
    const result = firstValueFrom(http.get('/api/v1/documents')).catch((error: unknown) => error);
    (await nextRequest('/api/v1/documents')).flush({}, { status: 401, statusText: 'Unauthorized' });
    (await nextRequest('/api/v1/auth/refresh')).flush(authResponse('token-2'));
    (await nextRequest('/api/v1/documents')).flush({}, { status: 401, statusText: 'Unauthorized' });

    expect(await result).toMatchObject({ status: 401 });
  });

  it('ends the session when the refresh fails', async () => {
    const result = firstValueFrom(http.get('/api/v1/documents')).catch((error: unknown) => error);
    (await nextRequest('/api/v1/documents')).flush({}, { status: 401, statusText: 'Unauthorized' });
    (await nextRequest('/api/v1/auth/refresh')).flush(
      {},
      { status: 401, statusText: 'Unauthorized' },
    );

    expect(await result).toMatchObject({ status: 401 });
    expect(store.status()).toBe('anonymous');
  });
});
