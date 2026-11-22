import { Injectable, inject } from '@angular/core';
import type { DocumentStatus } from '../../../core/api/api-types';
import { AuthorizedFetch } from '../../../core/http/authorized-fetch';
import { parseSse } from '../../../core/http/sse-parser';

export interface DocumentStatusEvent {
  readonly type: 'status';
  readonly documentId: string;
  readonly status: DocumentStatus;
  readonly errorMessage: string | null;
  readonly updatedAt: string;
}

export type DocumentStreamEvent = { readonly type: 'open' } | DocumentStatusEvent;

const STATUSES: readonly DocumentStatus[] = ['Pending', 'Processing', 'Ready', 'Failed'];

export function decodeDocumentEvent(name: string, data: string): DocumentStatusEvent | null {
  if (name !== 'status') {
    return null;
  }

  try {
    const payload = JSON.parse(data) as Record<string, unknown>;
    const status = STATUSES.find(
      (candidate) => candidate.toLowerCase() === String(payload['status']).toLowerCase(),
    );

    if (typeof payload['documentId'] !== 'string' || !status) {
      return null;
    }

    return {
      type: 'status',
      documentId: payload['documentId'],
      status,
      errorMessage: typeof payload['errorMessage'] === 'string' ? payload['errorMessage'] : null,
      updatedAt: typeof payload['updatedAt'] === 'string' ? payload['updatedAt'] : '',
    };
  } catch {
    return null;
  }
}

@Injectable({ providedIn: 'root' })
export class DocumentEventsService {
  private readonly http = inject(AuthorizedFetch);

  async *stream(signal: AbortSignal): AsyncGenerator<DocumentStreamEvent> {
    const response = await this.http.open('/documents/events', {
      headers: { Accept: 'text/event-stream' },
      signal,
    });

    yield { type: 'open' };

    for await (const message of parseSse(response.body as ReadableStream<Uint8Array>, signal)) {
      const event = decodeDocumentEvent(message.event, message.data);
      if (event) {
        yield event;
      }
    }
  }
}
