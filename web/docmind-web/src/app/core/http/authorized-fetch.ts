import { Injectable, inject } from '@angular/core';
import type { ProblemDetails } from '../api/api-types';
import { ApiUrl } from '../api/api-url';
import { AuthStore } from '../auth/auth.store';
import { ApiProblemError } from './problem';

@Injectable({ providedIn: 'root' })
export class AuthorizedFetch {
  private readonly auth = inject(AuthStore);
  private readonly apiUrl = inject(ApiUrl);

  async open(path: string, init: RequestInit & { signal: AbortSignal }): Promise<Response> {
    const url = this.apiUrl.build(path);
    const token = await this.auth.getValidToken();

    let response = await this.send(url, init, token);

    if (response.status === 401) {
      const renewed = await this.auth.recoverFromUnauthorized(token);
      if (!renewed) {
        void this.auth.expireSession();
        throw await toProblemError(response);
      }

      response = await this.send(url, init, renewed);
    }

    if (!response.ok || !response.body) {
      throw await toProblemError(response);
    }

    return response;
  }

  private send(url: string, init: RequestInit, token: string | null): Promise<Response> {
    const headers = new Headers(init.headers);
    if (token) {
      headers.set('Authorization', `Bearer ${token}`);
    }

    return fetch(url, { ...init, headers, credentials: 'include' });
  }
}

async function toProblemError(response: Response): Promise<ApiProblemError> {
  const problem = await response.json().then(
    (body: unknown) => body as ProblemDetails,
    () => null,
  );

  return new ApiProblemError(response.status, problem);
}
