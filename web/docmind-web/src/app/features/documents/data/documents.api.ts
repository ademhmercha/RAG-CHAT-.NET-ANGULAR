import { HttpClient, HttpContext, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import type { Observable } from 'rxjs';
import { ApiUrl } from '../../../core/api/api-url';
import type { DocumentItem, DocumentPage, DocumentStatus } from '../../../core/api/api-types';
import { SKIP_ERROR_NOTIFICATION } from '../../../core/http/http-context';

export interface DocumentQuery {
  readonly page: number;
  readonly pageSize: number;
  readonly status: DocumentStatus | null;
}

@Injectable({ providedIn: 'root' })
export class DocumentsApi {
  private readonly http = inject(HttpClient);
  private readonly apiUrl = inject(ApiUrl);

  list(query: DocumentQuery): Observable<DocumentPage> {
    let params = new HttpParams().set('page', query.page).set('pageSize', query.pageSize);
    if (query.status) {
      params = params.set('status', query.status);
    }

    return this.http.get<DocumentPage>(this.apiUrl.build('/documents'), { params });
  }

  get(id: string, silent = false): Observable<DocumentItem> {
    const context = new HttpContext().set(SKIP_ERROR_NOTIFICATION, silent);
    return this.http.get<DocumentItem>(this.apiUrl.build(`/documents/${id}`), { context });
  }

  remove(id: string): Observable<void> {
    return this.http.delete<void>(this.apiUrl.build(`/documents/${id}`));
  }
}
