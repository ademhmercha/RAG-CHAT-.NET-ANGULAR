import { Injectable, inject } from '@angular/core';
import type { Citation, ProblemDetails, SendMessageRequest } from '../../../core/api/api-types';
import { AuthorizedFetch } from '../../../core/http/authorized-fetch';
import { ApiProblemError } from '../../../core/http/problem';
import { parseSse } from '../../../core/http/sse-parser';

export type ToolStatus = 'running' | 'completed' | 'failed';

export type ChatStreamEvent =
  | { readonly type: 'token'; readonly text: string }
  | { readonly type: 'tool'; readonly name: string; readonly status: ToolStatus }
  | { readonly type: 'citation'; readonly citation: Citation }
  | { readonly type: 'done'; readonly messageId: string }
  | { readonly type: 'error'; readonly error: ApiProblemError };

export function decodeChatEvent(name: string, data: string): ChatStreamEvent | null {
  let payload: Record<string, unknown>;

  try {
    payload = JSON.parse(data) as Record<string, unknown>;
  } catch {
    return null;
  }

  switch (name) {
    case 'token':
      return typeof payload['text'] === 'string' ? { type: 'token', text: payload['text'] } : null;
    case 'tool':
      return typeof payload['name'] === 'string'
        ? {
            type: 'tool',
            name: payload['name'],
            status: String(payload['status']).toLowerCase() as ToolStatus,
          }
        : null;
    case 'citation':
      return { type: 'citation', citation: payload as unknown as Citation };
    case 'done':
      return { type: 'done', messageId: String(payload['messageId'] ?? '') };
    case 'error': {
      const problem = payload as ProblemDetails;
      return { type: 'error', error: new ApiProblemError(problem.status ?? 500, problem) };
    }
    default:
      return null;
  }
}

@Injectable({ providedIn: 'root' })
export class ChatStreamService {
  private readonly http = inject(AuthorizedFetch);

  async *send(
    conversationId: string,
    request: SendMessageRequest,
    signal: AbortSignal,
  ): AsyncGenerator<ChatStreamEvent> {
    const response = await this.http.open(`/conversations/${conversationId}/messages`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json', Accept: 'text/event-stream' },
      body: JSON.stringify(request),
      signal,
    });

    for await (const message of parseSse(response.body as ReadableStream<Uint8Array>, signal)) {
      const event = decodeChatEvent(message.event, message.data);
      if (event) {
        yield event;
      }
    }
  }
}
