import { TestBed } from '@angular/core/testing';
import type { ActivatedRouteSnapshot, RouterStateSnapshot, UrlTree } from '@angular/router';
import { Router, convertToParamMap, provideRouter } from '@angular/router';
import { patchState } from '@ngrx/signals';
import { provideApiTesting } from '../../../testing/api-testing';
import { AuthStore } from '../auth/auth.store';
import { authGuard, guestGuard } from './auth.guards';

describe('route guards', () => {
  let store: InstanceType<typeof AuthStore>;
  let router: Router;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideApiTesting(), provideRouter([])] });
    store = TestBed.inject(AuthStore);
    router = TestBed.inject(Router);
  });

  const run = (guard: typeof authGuard, url: string, query: Record<string, string> = {}) =>
    TestBed.runInInjectionContext(() =>
      guard(
        { queryParamMap: convertToParamMap(query) } as ActivatedRouteSnapshot,
        { url } as RouterStateSnapshot,
      ),
    );

  const signIn = () => patchState(store as never, { status: 'authenticated' } as never);

  it('lets an authenticated user through', () => {
    signIn();

    expect(run(authGuard, '/documents')).toBe(true);
  });

  it('redirects anonymous visitors to the login page with a return url', () => {
    const result = run(authGuard, '/documents?page=2') as UrlTree;

    expect(router.serializeUrl(result)).toBe('/auth/login?returnUrl=%2Fdocuments%3Fpage%3D2');
  });

  it('lets guests reach the auth pages', () => {
    expect(run(guestGuard, '/auth/login')).toBe(true);
  });

  it('sends signed-in users away from the auth pages', () => {
    signIn();

    const result = run(guestGuard, '/auth/login', { returnUrl: '/documents' }) as UrlTree;

    expect(router.serializeUrl(result)).toBe('/documents');
  });

  it('ignores an unsafe return url', () => {
    signIn();

    const result = run(guestGuard, '/auth/login', { returnUrl: '//evil.example' }) as UrlTree;

    expect(router.serializeUrl(result)).toBe('/chat');
  });
});
