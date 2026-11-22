import { InjectionToken, computed, inject } from '@angular/core';
import { Router } from '@angular/router';
import { patchState, signalStore, withComputed, withMethods, withState } from '@ngrx/signals';
import { firstValueFrom } from 'rxjs';
import type { AuthResponse, LoginRequest, RegisterRequest, UserDto } from '../api/api-types';
import { statusOf } from '../http/problem';
import { AuthApi } from './auth.api';

export type AuthStatus = 'unknown' | 'authenticated' | 'anonymous';

interface AuthState {
  user: UserDto | null;
  accessToken: string | null;
  expiresAt: number | null;
  status: AuthStatus;
}

const initialState: AuthState = {
  user: null,
  accessToken: null,
  expiresAt: null,
  status: 'unknown',
};

export const AUTH_NOW = new InjectionToken<() => number>('AUTH_NOW', {
  providedIn: 'root',
  factory: () => () => Date.now(),
});

export const TOKEN_EXPIRY_SKEW_MS = 30_000;
export const LOGIN_PATH = '/auth/login';

export function sanitizeReturnUrl(candidate: string | null | undefined): string | null {
  if (
    !candidate ||
    !candidate.startsWith('/') ||
    candidate.startsWith('//') ||
    candidate.includes('\\')
  ) {
    return null;
  }

  return candidate.startsWith('/auth') ? null : candidate;
}

export const AuthStore = signalStore(
  { providedIn: 'root' },
  withState(initialState),
  withComputed(({ user, status }) => ({
    isAuthenticated: computed(() => status() === 'authenticated'),
    initials: computed(() => {
      const name = user()?.displayName.trim() ?? '';
      const parts = name.split(/\s+/).filter(Boolean);
      return (
        (parts[0]?.[0] ?? '') + (parts.length > 1 ? (parts.at(-1)?.[0] ?? '') : '')
      ).toUpperCase();
    }),
  })),
  withMethods((store) => {
    const api = inject(AuthApi);
    const router = inject(Router);
    const now = inject(AUTH_NOW);

    let refreshInFlight: Promise<string | null> | null = null;

    const applySession = (response: AuthResponse): void =>
      patchState(store, {
        user: response.user,
        accessToken: response.accessToken,
        expiresAt: Date.parse(response.expiresAt),
        status: 'authenticated',
      });

    const clear = (): void => patchState(store, { ...initialState, status: 'anonymous' });

    const runRefresh = async (): Promise<string | null> => {
      try {
        const response = await firstValueFrom(api.refresh());
        applySession(response);
        return response.accessToken;
      } catch (error) {
        if (store.status() !== 'authenticated' || statusOf(error) === 401) {
          clear();
        }

        return null;
      }
    };

    const refresh = (): Promise<string | null> => {
      refreshInFlight ??= runRefresh().finally(() => {
        refreshInFlight = null;
      });

      return refreshInFlight;
    };

    const login = async (request: LoginRequest): Promise<void> => {
      applySession(await firstValueFrom(api.login(request)));
    };

    return {
      login,

      async register(request: RegisterRequest): Promise<void> {
        await firstValueFrom(api.register(request));
        await login({ email: request.email, password: request.password });
      },

      refresh,

      async restoreSession(): Promise<void> {
        await refresh();
        if (store.status() === 'unknown') {
          clear();
        }
      },

      async getValidToken(): Promise<string | null> {
        const token = store.accessToken();
        const expiresAt = store.expiresAt();

        if (token && expiresAt !== null && expiresAt - now() > TOKEN_EXPIRY_SKEW_MS) {
          return token;
        }

        return store.status() === 'authenticated' ? refresh() : null;
      },

      async recoverFromUnauthorized(rejectedToken: string | null): Promise<string | null> {
        const current = store.accessToken();
        return current && current !== rejectedToken ? current : refresh();
      },

      async logout(): Promise<void> {
        try {
          await firstValueFrom(api.logout());
        } catch {
          // The session is dropped locally even when the server cannot be reached.
        }

        clear();
        await router.navigateByUrl(LOGIN_PATH);
      },

      async expireSession(): Promise<void> {
        clear();
        const returnUrl = sanitizeReturnUrl(router.url);
        await router.navigate([LOGIN_PATH], returnUrl ? { queryParams: { returnUrl } } : {});
      },
    };
  }),
);
