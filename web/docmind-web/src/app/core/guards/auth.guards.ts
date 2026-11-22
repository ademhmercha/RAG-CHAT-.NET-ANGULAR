import { inject } from '@angular/core';
import type { CanActivateFn } from '@angular/router';
import { Router } from '@angular/router';
import { AuthStore, LOGIN_PATH, sanitizeReturnUrl } from '../auth/auth.store';

export const authGuard: CanActivateFn = (_route, state) => {
  const auth = inject(AuthStore);
  const router = inject(Router);

  if (auth.isAuthenticated()) {
    return true;
  }

  const returnUrl = sanitizeReturnUrl(state.url);
  return router.createUrlTree([LOGIN_PATH], returnUrl ? { queryParams: { returnUrl } } : {});
};

export const guestGuard: CanActivateFn = (route) => {
  const auth = inject(AuthStore);
  const router = inject(Router);

  if (!auth.isAuthenticated()) {
    return true;
  }

  const target = sanitizeReturnUrl(route.queryParamMap.get('returnUrl')) ?? '/chat';
  return router.parseUrl(target);
};
