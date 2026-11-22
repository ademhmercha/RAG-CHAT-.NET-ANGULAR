import type {
  HttpEvent,
  HttpHandlerFn,
  HttpInterceptorFn,
  HttpRequest,
} from '@angular/common/http';
import { HttpErrorResponse } from '@angular/common/http';
import { inject } from '@angular/core';
import type { Observable } from 'rxjs';
import { catchError, from, switchMap, throwError } from 'rxjs';
import { ApiUrl } from '../api/api-url';
import { AuthStore } from '../auth/auth.store';
import { AUTH_RETRIED } from '../http/http-context';

const CREDENTIAL_PATHS = ['/auth/login', '/auth/register', '/auth/refresh', '/auth/logout'];

function withBearer(request: HttpRequest<unknown>, token: string | null): HttpRequest<unknown> {
  return token ? request.clone({ setHeaders: { Authorization: `Bearer ${token}` } }) : request;
}

export const authInterceptor: HttpInterceptorFn = (request, next) => {
  const apiUrl = inject(ApiUrl);

  if (!apiUrl.isApiRequest(request.url)) {
    return next(request);
  }

  const withCredentials = request.clone({ withCredentials: true });
  if (CREDENTIAL_PATHS.some((path) => request.url.endsWith(path))) {
    return next(withCredentials);
  }

  const auth = inject(AuthStore);

  return from(auth.getValidToken()).pipe(
    switchMap((token) =>
      next(withBearer(withCredentials, token)).pipe(
        catchError((error: unknown) => recover(error, token, withCredentials, next, auth)),
      ),
    ),
  );
};

function recover(
  error: unknown,
  usedToken: string | null,
  request: HttpRequest<unknown>,
  next: HttpHandlerFn,
  auth: InstanceType<typeof AuthStore>,
): Observable<HttpEvent<unknown>> {
  if (
    !(error instanceof HttpErrorResponse) ||
    error.status !== 401 ||
    request.context.get(AUTH_RETRIED)
  ) {
    return throwError(() => error);
  }

  return from(auth.recoverFromUnauthorized(usedToken)).pipe(
    switchMap((token) => {
      if (!token) {
        void auth.expireSession();
        return throwError(() => error);
      }

      request.context.set(AUTH_RETRIED, true);
      return next(withBearer(request, token));
    }),
  );
}
