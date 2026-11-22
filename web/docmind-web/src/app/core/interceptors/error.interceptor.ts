import type { HttpInterceptorFn } from '@angular/common/http';
import { HttpErrorResponse } from '@angular/common/http';
import { inject } from '@angular/core';
import { catchError, throwError } from 'rxjs';
import { ApiUrl } from '../api/api-url';
import { SKIP_ERROR_NOTIFICATION } from '../http/http-context';
import { ProblemMessages } from '../http/problem';
import { NotificationService } from '../services/notification.service';

export const errorInterceptor: HttpInterceptorFn = (request, next) => {
  const apiUrl = inject(ApiUrl);
  const notifications = inject(NotificationService);
  const messages = inject(ProblemMessages);

  return next(request).pipe(
    catchError((error: unknown) => {
      const silent = request.context.get(SKIP_ERROR_NOTIFICATION);
      const handledElsewhere = error instanceof HttpErrorResponse && error.status === 401;

      if (apiUrl.isApiRequest(request.url) && !silent && !handledElsewhere) {
        notifications.error(messages.describe(error));
      }

      return throwError(() => error);
    }),
  );
};
