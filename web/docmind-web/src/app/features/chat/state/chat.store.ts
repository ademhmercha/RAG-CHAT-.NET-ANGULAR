import { computed, effect, inject } from '@angular/core';
import {
  patchState,
  signalStore,
  withComputed,
  withHooks,
  withMethods,
  withState,
} from '@ngrx/signals';
import { firstValueFrom } from 'rxjs';
import type { Citation, Message } from '../../../core/api/api-types';
import { AuthStore } from '../../../core/auth/auth.store';
import { ProblemMessages } from '../../../core/http/problem';
import { ChatStreamService } from '../data/chat-stream.service';
import type { ToolStatus } from '../data/chat-stream.service';
import { ConversationsApi } from '../data/conversations.api';
import { ConversationsStore } from './conversations.store';

export type ChatMessageStatus = 'complete' | 'streaming' | 'stopped' | 'failed';

export interface ChatMessage {
  readonly id: string;
  readonly role: 'user' | 'assistant';
  readonly content: string;
  readonly citations: readonly Citation[];
  readonly createdAt: string;
  readonly status: ChatMessageStatus;
  readonly error: string | null;
}

export interface ToolActivity {
  readonly name: string;
  readonly status: ToolStatus;
}

interface ChatState {
  conversationId: string | null;
  messages: ChatMessage[];
  loading: boolean;
  missing: boolean;
  streaming: boolean;
  tool: ToolActivity | null;
}

const initialState: ChatState = {
  conversationId: null,
  messages: [],
  loading: false,
  missing: false,
  streaming: false,
  tool: null,
};

export function toChatMessage(message: Message): ChatMessage {
  return {
    id: message.id,
    role: message.role === 'User' ? 'user' : 'assistant',
    content: message.content,
    citations: message.citations,
    createdAt: message.createdAt,
    status: 'complete',
    error: null,
  };
}

export const ChatStore = signalStore(
  { providedIn: 'root' },
  withState(initialState),
  withComputed(({ messages, streaming }) => ({
    isEmpty: computed(() => messages().length === 0),
    canSend: computed(() => !streaming()),
  })),
  withMethods((store) => {
    const stream = inject(ChatStreamService);
    const conversationsApi = inject(ConversationsApi);
    const conversations = inject(ConversationsStore);
    const problems = inject(ProblemMessages);

    let controller: AbortController | null = null;
    let sequence = 0;
    let openRequest = 0;

    const patchMessage = (id: string, change: (message: ChatMessage) => ChatMessage): void =>
      patchState(store, {
        messages: store
          .messages()
          .map((message) => (message.id === id ? change(message) : message)),
      });

    const abortStream = (): void => controller?.abort();

    const reset = (): void => {
      abortStream();
      openRequest++;
      patchState(store, initialState);
    };

    const consume = async (
      conversationId: string,
      assistantId: string,
      content: string,
      documentIds: readonly string[] | null,
      signal: AbortSignal,
    ): Promise<void> => {
      let finished = false;

      for await (const event of stream.send(
        conversationId,
        { content, documentIds: documentIds ? [...documentIds] : null },
        signal,
      )) {
        switch (event.type) {
          case 'token':
            patchMessage(assistantId, (message) => ({
              ...message,
              content: message.content + event.text,
            }));
            break;
          case 'tool':
            patchState(store, { tool: { name: event.name, status: event.status } });
            break;
          case 'citation':
            patchMessage(assistantId, (message) => ({
              ...message,
              citations: [...message.citations, event.citation],
            }));
            break;
          case 'done':
            finished = true;
            patchMessage(assistantId, (message) => ({
              ...message,
              id: event.messageId || message.id,
              status: 'complete',
            }));
            break;
          case 'error':
            finished = true;
            patchMessage(assistantId, (message) => ({
              ...message,
              status: 'failed',
              error: problems.describe(event.error),
            }));
            break;
        }
      }

      if (!finished) {
        patchMessage(assistantId, (message) => ({ ...message, status: 'complete' }));
      }
    };

    return {
      reset,

      startNew(): void {
        reset();
      },

      async open(conversationId: string): Promise<void> {
        if (store.conversationId() === conversationId) {
          return;
        }

        abortStream();
        const request = ++openRequest;
        patchState(store, { ...initialState, conversationId, loading: true });

        try {
          const conversation = await firstValueFrom(conversationsApi.get(conversationId));
          if (request === openRequest) {
            patchState(store, { messages: conversation.messages.map(toChatMessage) });
          }
        } catch {
          if (request === openRequest) {
            patchState(store, { ...initialState, missing: true });
          }
        } finally {
          if (request === openRequest) {
            patchState(store, { loading: false });
          }
        }
      },

      async send(content: string, documentIds: readonly string[] | null): Promise<string | null> {
        const text = content.trim();
        if (!text || store.streaming()) {
          return store.conversationId();
        }

        let conversationId = store.conversationId();
        if (!conversationId) {
          try {
            conversationId = (await conversations.create()).id;
          } catch {
            return null;
          }
          patchState(store, { conversationId, missing: false });
        }

        const now = new Date().toISOString();
        const assistantId = `local-assistant-${++sequence}`;
        patchState(store, {
          streaming: true,
          tool: null,
          messages: [
            ...store.messages(),
            {
              id: `local-user-${sequence}`,
              role: 'user',
              content: text,
              citations: [],
              createdAt: now,
              status: 'complete',
              error: null,
            },
            {
              id: assistantId,
              role: 'assistant',
              content: '',
              citations: [],
              createdAt: now,
              status: 'streaming',
              error: null,
            },
          ],
        });

        const current = new AbortController();
        controller = current;

        try {
          await consume(conversationId, assistantId, text, documentIds, current.signal);
        } catch (error) {
          patchMessage(assistantId, (message) =>
            current.signal.aborted
              ? { ...message, status: 'stopped' }
              : { ...message, status: 'failed', error: problems.describe(error) },
          );
        } finally {
          if (controller === current) {
            controller = null;
            patchState(store, { streaming: false, tool: null });
          }
          void conversations.refresh();
        }

        return conversationId;
      },

      stop(): void {
        abortStream();
      },
    };
  }),
  withHooks({
    onInit(store) {
      const auth = inject(AuthStore);

      effect(() => {
        if (auth.status() === 'anonymous') {
          store.reset();
        }
      });
    },
  }),
);
