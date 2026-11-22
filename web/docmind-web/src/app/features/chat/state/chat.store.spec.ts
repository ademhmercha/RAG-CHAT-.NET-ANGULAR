import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { patchState } from '@ngrx/signals';
import { of, throwError } from 'rxjs';
import type { Conversation, ConversationSummary } from '../../../core/api/api-types';
import { AuthStore } from '../../../core/auth/auth.store';
import { ApiProblemError } from '../../../core/http/problem';
import { provideApiTesting } from '../../../../testing/api-testing';
import { provideTranslocoTesting } from '../../../../testing/transloco-testing';
import type { ChatStreamEvent } from '../data/chat-stream.service';
import { ChatStreamService } from '../data/chat-stream.service';
import { ConversationsApi } from '../data/conversations.api';
import { ChatStore } from './chat.store';
import { ConversationsStore } from './conversations.store';

const created = {
  id: 'c-1',
  title: 'New conversation',
  createdAt: '2030-01-01T00:00:00Z',
  updatedAt: '2030-01-01T00:00:00Z',
} as ConversationSummary;

function scripted(events: ChatStreamEvent[]) {
  return async function* () {
    for (const event of events) {
      yield event;
    }
  };
}

describe('ChatStore', () => {
  const stream = { send: vi.fn() };
  const api = { list: vi.fn(), create: vi.fn(), get: vi.fn(), rename: vi.fn(), remove: vi.fn() };
  let chat: InstanceType<typeof ChatStore>;

  beforeEach(() => {
    stream.send.mockReset();
    Object.values(api).forEach((fn) => fn.mockReset());
    api.list.mockReturnValue(of([created]));
    api.create.mockReturnValue(of(created));

    TestBed.configureTestingModule({
      providers: [
        provideApiTesting(),
        provideRouter([]),
        provideTranslocoTesting(),
        { provide: ChatStreamService, useValue: stream },
        { provide: ConversationsApi, useValue: api },
      ],
    });
    chat = TestBed.inject(ChatStore);
  });

  it('creates a conversation on the first question and streams the answer', async () => {
    stream.send.mockImplementation(
      scripted([
        { type: 'tool', name: 'search_documents', status: 'running' },
        { type: 'tool', name: 'search_documents', status: 'completed' },
        {
          type: 'citation',
          citation: { documentId: 'd1', documentName: 'a.pdf', page: 2, excerpt: 'x' },
        },
        { type: 'token', text: 'Hello ' },
        { type: 'token', text: 'world' },
        { type: 'done', messageId: 'm-9' },
      ]),
    );

    const id = await chat.send('  What is this?  ', ['d1']);

    expect(id).toBe('c-1');
    expect(api.create).toHaveBeenCalledOnce();
    expect(stream.send).toHaveBeenCalledWith(
      'c-1',
      { content: 'What is this?', documentIds: ['d1'] },
      expect.any(AbortSignal),
    );

    const [user, assistant] = chat.messages();
    expect(user).toMatchObject({ role: 'user', content: 'What is this?' });
    expect(assistant).toMatchObject({
      id: 'm-9',
      role: 'assistant',
      content: 'Hello world',
      status: 'complete',
    });
    expect(assistant?.citations).toHaveLength(1);
    expect(chat.streaming()).toBe(false);
    expect(chat.tool()).toBeNull();
  });

  it('reuses the current conversation for follow-up questions', async () => {
    stream.send.mockImplementation(scripted([{ type: 'done', messageId: 'm-1' }]));

    await chat.send('one', null);
    await chat.send('two', null);

    expect(api.create).toHaveBeenCalledOnce();
    expect(chat.messages()).toHaveLength(4);
  });

  it('ignores blank questions', async () => {
    await chat.send('   ', null);

    expect(stream.send).not.toHaveBeenCalled();
    expect(chat.messages()).toEqual([]);
  });

  it('refreshes the conversation list after an answer', async () => {
    stream.send.mockImplementation(scripted([{ type: 'done', messageId: 'm-1' }]));
    const conversations = TestBed.inject(ConversationsStore);
    const refresh = vi.spyOn(conversations, 'refresh');

    await chat.send('hello', null);

    expect(refresh).toHaveBeenCalled();
  });

  it('shows a translated error when the server reports one', async () => {
    stream.send.mockImplementation(
      scripted([
        { type: 'token', text: 'Par' },
        {
          type: 'error',
          error: new ApiProblemError(503, { code: 'chat.provider_unavailable' } as never),
        },
      ]),
    );

    await chat.send('hello', null);

    expect(chat.messages()[1]).toMatchObject({
      status: 'failed',
      content: 'Par',
      error: 'The AI service is unavailable right now. Please try again.',
    });
    expect(chat.streaming()).toBe(false);
  });

  it('reports a transport failure on the answer', async () => {
    stream.send.mockImplementation(() => {
      throw new ApiProblemError(429, { code: 'rate_limit.exceeded' } as never);
    });

    await chat.send('hello', null);

    expect(chat.messages()[1]).toMatchObject({
      status: 'failed',
      error: 'You are going too fast. Please wait a few seconds.',
    });
  });

  it('keeps the partial answer when the user stops the generation', async () => {
    stream.send.mockImplementation(async function* (
      _id: string,
      _body: unknown,
      signal: AbortSignal,
    ) {
      yield { type: 'token', text: 'Partial' } satisfies ChatStreamEvent;
      await new Promise((_resolve, reject) => {
        signal.addEventListener('abort', () => reject(new DOMException('Aborted', 'AbortError')));
      });
    });

    const pending = chat.send('hello', null);
    await vi.waitFor(() => expect(chat.messages()[1]?.content).toBe('Partial'));
    expect(chat.streaming()).toBe(true);

    chat.stop();
    await pending;

    expect(chat.messages()[1]).toMatchObject({ status: 'stopped', content: 'Partial' });
    expect(chat.streaming()).toBe(false);
  });

  it('returns null when the conversation cannot be created', async () => {
    api.create.mockReturnValue(throwError(() => new Error('down')));

    expect(await chat.send('hello', null)).toBeNull();
    expect(chat.messages()).toEqual([]);
  });

  it('loads an existing conversation', async () => {
    api.get.mockReturnValue(
      of({
        ...created,
        messages: [
          { id: 'm1', role: 'User', content: 'Q', citations: [], usage: null, createdAt: 'x' },
          { id: 'm2', role: 'Assistant', content: 'A', citations: [], usage: null, createdAt: 'x' },
        ],
      } as Conversation),
    );

    await chat.open('c-1');

    expect(chat.conversationId()).toBe('c-1');
    expect(chat.messages().map((message) => message.role)).toEqual(['user', 'assistant']);
    expect(chat.loading()).toBe(false);
  });

  it('does not reload the conversation it is already showing', async () => {
    api.get.mockReturnValue(of({ ...created, messages: [] } as Conversation));

    await chat.open('c-1');
    await chat.open('c-1');

    expect(api.get).toHaveBeenCalledOnce();
  });

  it('flags a missing conversation', async () => {
    api.get.mockReturnValue(throwError(() => new Error('404')));

    await chat.open('ghost');

    expect(chat.missing()).toBe(true);
    expect(chat.conversationId()).toBeNull();
  });

  it('forgets everything when the session ends', async () => {
    stream.send.mockImplementation(scripted([{ type: 'done', messageId: 'm-1' }]));
    await chat.send('hello', null);

    patchState(TestBed.inject(AuthStore) as never, { status: 'anonymous' } as never);
    TestBed.tick();

    expect(chat.messages()).toEqual([]);
    expect(chat.conversationId()).toBeNull();
  });
});
