import { Injectable, inject } from '@angular/core';
import type { DocumentItem, ProblemDetails } from '../../../core/api/api-types';
import { ApiUrl } from '../../../core/api/api-url';
import { AuthStore } from '../../../core/auth/auth.store';
import { ApiProblemError } from '../../../core/http/problem';

@Injectable({ providedIn: 'root' })
export class DocumentUploader {
  private readonly auth = inject(AuthStore);
  private readonly apiUrl = inject(ApiUrl);

  async upload(
    file: File,
    onProgress: (percent: number) => void,
    signal: AbortSignal,
  ): Promise<DocumentItem> {
    const url = this.apiUrl.build('/documents');
    const token = await this.auth.getValidToken();

    try {
      return await send(url, file, token, onProgress, signal);
    } catch (error) {
      if (!(error instanceof ApiProblemError) || error.status !== 401) {
        throw error;
      }

      const renewed = await this.auth.recoverFromUnauthorized(token);
      if (!renewed) {
        void this.auth.expireSession();
        throw error;
      }

      return send(url, file, renewed, onProgress, signal);
    }
  }
}

function send(
  url: string,
  file: File,
  token: string | null,
  onProgress: (percent: number) => void,
  signal: AbortSignal,
): Promise<DocumentItem> {
  return new Promise<DocumentItem>((resolve, reject) => {
    if (signal.aborted) {
      reject(new DOMException('Aborted', 'AbortError'));
      return;
    }

    const request = new XMLHttpRequest();
    request.open('POST', url);
    request.withCredentials = true;
    request.responseType = 'json';
    if (token) {
      request.setRequestHeader('Authorization', `Bearer ${token}`);
    }

    request.upload.onprogress = (event) => {
      if (event.lengthComputable) {
        onProgress(Math.min(100, Math.round((event.loaded / event.total) * 100)));
      }
    };

    request.onload = () => {
      const body = request.response as unknown;

      if (request.status >= 200 && request.status < 300) {
        resolve(body as DocumentItem);
      } else {
        reject(
          new ApiProblemError(
            request.status,
            typeof body === 'object' && body !== null ? (body as ProblemDetails) : null,
          ),
        );
      }
    };

    request.onerror = () => reject(new ApiProblemError(0, null));
    request.onabort = () => reject(new DOMException('Aborted', 'AbortError'));
    signal.addEventListener('abort', () => request.abort(), { once: true });

    const form = new FormData();
    form.append('file', file, file.name);
    request.send(form);
  });
}
