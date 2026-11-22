import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import type { Observable } from 'rxjs';
import { ApiUrl } from '../../../core/api/api-url';
import type { Conversation, ConversationSummary } from '../../../core/api/api-types';

@Injectable({ providedIn: 'root' })
export class ConversationsApi {
  private readonly http = inject(HttpClient);
  private readonly apiUrl = inject(ApiUrl);

  list(): Observable<ConversationSummary[]> {
    return this.http.get<ConversationSummary[]>(this.apiUrl.build('/conversations'));
  }

  get(id: string): Observable<Conversation> {
    return this.http.get<Conversation>(this.apiUrl.build(`/conversations/${id}`));
  }

  create(title: string | null = null): Observable<ConversationSummary> {
    return this.http.post<ConversationSummary>(this.apiUrl.build('/conversations'), { title });
  }

  rename(id: string, title: string): Observable<ConversationSummary> {
    return this.http.patch<ConversationSummary>(this.apiUrl.build(`/conversations/${id}`), {
      title,
    });
  }

  remove(id: string): Observable<void> {
    return this.http.delete<void>(this.apiUrl.build(`/conversations/${id}`));
  }
}
