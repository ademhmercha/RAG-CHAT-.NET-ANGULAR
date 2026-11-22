import { Injectable, inject } from '@angular/core';
import { APP_CONFIG } from '../config/app-config';

export const API_PREFIX = '/api/v1';

@Injectable({ providedIn: 'root' })
export class ApiUrl {
  private readonly baseUrl = inject(APP_CONFIG).apiBaseUrl;

  build(path: string): string {
    const normalized = path.startsWith('/') ? path : `/${path}`;
    return `${this.baseUrl}${API_PREFIX}${normalized}`;
  }

  isApiRequest(url: string): boolean {
    return url.startsWith(`${this.baseUrl}${API_PREFIX}/`);
  }
}
