import { HttpErrorResponse } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { TranslocoService } from '@jsverse/transloco';
import type { ProblemDetails } from '../api/api-types';

export function toProblem(error: unknown): ProblemDetails | null {
  if (
    !(error instanceof HttpErrorResponse) ||
    typeof error.error !== 'object' ||
    error.error === null
  ) {
    return null;
  }

  return error.error as ProblemDetails;
}

export function statusOf(error: unknown): number | null {
  if (error instanceof HttpErrorResponse) {
    return error.status;
  }

  if (error instanceof ApiProblemError) {
    return error.status;
  }

  return null;
}

export class ApiProblemError extends Error {
  constructor(
    readonly status: number,
    readonly problem: ProblemDetails | null,
  ) {
    super(problem?.detail ?? problem?.title ?? `Request failed with status ${status}`);
    this.name = 'ApiProblemError';
  }
}

const STATUS_KEYS: Readonly<Record<number, string>> = {
  400: 'errors.status.400',
  401: 'errors.status.401',
  403: 'errors.status.403',
  404: 'errors.status.404',
  409: 'errors.status.409',
  413: 'errors.status.413',
  415: 'errors.status.415',
  429: 'errors.status.429',
};

@Injectable({ providedIn: 'root' })
export class ProblemMessages {
  private readonly transloco = inject(TranslocoService);

  describe(error: unknown): string {
    const status = statusOf(error);
    const problem = error instanceof ApiProblemError ? error.problem : toProblem(error);

    if (status === 0) {
      return this.transloco.translate('errors.network');
    }

    const byCode = problem?.code ? this.translateIfPresent(`errors.codes.${problem.code}`) : null;
    if (byCode) {
      return this.withReference(byCode, status, problem);
    }

    const byStatus = status !== null ? STATUS_KEYS[status] : undefined;
    if (byStatus) {
      return this.withReference(this.transloco.translate(byStatus), status, problem);
    }

    const generic = this.transloco.translate(
      status !== null && status >= 500 ? 'errors.server' : 'errors.unknown',
    );
    return this.withReference(generic, status, problem);
  }

  private translateIfPresent(key: string): string | null {
    const translated = this.transloco.translate(key);
    return translated === key ? null : translated;
  }

  private withReference(
    message: string,
    status: number | null,
    problem: ProblemDetails | null,
  ): string {
    return status !== null && status >= 500 && problem?.traceId
      ? `${message} (${this.transloco.translate('errors.reference')}: ${problem.traceId.slice(0, 8)})`
      : message;
  }
}
