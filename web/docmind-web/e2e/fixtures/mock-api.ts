import type { Page, Route } from '@playwright/test';

interface StubUser {
  id: string;
  email: string;
  displayName: string;
  password: string;
}

interface StubDocument {
  id: string;
  fileName: string;
  contentType: string;
  sizeBytes: number;
  status: 'Pending' | 'Processing' | 'Ready' | 'Failed';
  errorMessage: string | null;
  pageCount: number;
  chunkCount: number;
  createdAt: string;
  updatedAt: string;
}

interface StubMessage {
  id: string;
  role: 'User' | 'Assistant';
  content: string;
  citations: unknown[];
  usage: null;
  createdAt: string;
}

interface StubConversation {
  id: string;
  title: string;
  createdAt: string;
  updatedAt: string;
  messages: StubMessage[];
}

export const ANSWER_TOKENS = ['The contract ', 'lasts **12 months** ', 'and renews yearly. [1]'];

export const CITATION = {
  documentName: 'contract.pdf',
  page: 3,
  excerpt: 'This agreement is concluded for a period of twelve (12) months, renewable yearly.',
};

const sse = (event: string, data: unknown): string =>
  `event: ${event}\ndata: ${JSON.stringify(data)}\n\n`;

const problem = (status: number, code: string) => ({
  status,
  contentType: 'application/problem+json',
  body: JSON.stringify({ status, title: code, code }),
});

export class StubApi {
  private readonly users = new Map<string, StubUser>();
  private readonly conversations: StubConversation[] = [];
  private readonly documents: StubDocument[] = [];
  private sessionUserId: string | null = null;
  private sequence = 0;

  readonly requests: string[] = [];

  async install(page: Page): Promise<void> {
    await page.route('**/api/v1/**', (route) => this.handle(route));
  }

  private id(prefix: string): string {
    this.sequence += 1;
    return `${prefix}-${this.sequence}`;
  }

  private authResponse(user: StubUser) {
    return {
      accessToken: `token-${user.id}`,
      expiresAt: new Date(Date.now() + 60 * 60 * 1000).toISOString(),
      user: { id: user.id, email: user.email, displayName: user.displayName },
    };
  }

  private async handle(route: Route): Promise<void> {
    const request = route.request();
    const url = new URL(request.url());
    const path = url.pathname.replace('/api/v1', '');
    const method = request.method();
    this.requests.push(`${method} ${path}`);

    const json = (status: number, body: unknown) =>
      route.fulfill({
        status,
        contentType: 'application/json',
        body: JSON.stringify(body),
      });

    if (path === '/auth/register' && method === 'POST') {
      const body = request.postDataJSON() as Omit<StubUser, 'id'>;
      if (body.email === 'taken@example.com') {
        return route.fulfill(problem(409, 'auth.email_taken'));
      }

      const user = { ...body, id: this.id('user') };
      this.users.set(user.email, user);
      return json(201, { id: user.id, email: user.email, displayName: user.displayName });
    }

    if (path === '/auth/login' && method === 'POST') {
      const body = request.postDataJSON() as { email: string; password: string };
      const user = this.users.get(body.email);
      if (user?.password !== body.password) {
        return route.fulfill(problem(401, 'auth.invalid_credentials'));
      }

      this.sessionUserId = user.id;
      return json(200, this.authResponse(user));
    }

    if (path === '/auth/refresh' && method === 'POST') {
      const user = [...this.users.values()].find(
        (candidate) => candidate.id === this.sessionUserId,
      );
      return user
        ? json(200, this.authResponse(user))
        : route.fulfill(problem(401, 'auth.invalid_refresh_token'));
    }

    if (path === '/auth/logout' && method === 'POST') {
      this.sessionUserId = null;
      return route.fulfill({ status: 204 });
    }

    if (path === '/conversations' && method === 'GET') {
      return json(
        200,
        this.conversations.map(({ messages: _messages, ...summary }) => summary),
      );
    }

    if (path === '/conversations' && method === 'POST') {
      const now = new Date().toISOString();
      const conversation: StubConversation = {
        id: this.id('conversation'),
        title: 'New conversation',
        createdAt: now,
        updatedAt: now,
        messages: [],
      };
      this.conversations.unshift(conversation);
      const { messages: _messages, ...summary } = conversation;
      return json(201, summary);
    }

    const conversationMatch = /^\/conversations\/([^/]+)(\/messages)?$/.exec(path);
    if (conversationMatch) {
      return this.handleConversation(route, conversationMatch[1] ?? '', !!conversationMatch[2]);
    }

    if (path === '/documents' && method === 'GET') {
      const status = url.searchParams.get('status');
      const items = this.documents.filter((document) => !status || document.status === status);
      return json(200, {
        items,
        page: 1,
        pageSize: Number(url.searchParams.get('pageSize') ?? 20),
        totalCount: items.length,
        totalPages: items.length > 0 ? 1 : 0,
      });
    }

    if (path === '/documents' && method === 'POST') {
      const payload = request.postDataBuffer()?.toString('latin1') ?? '';
      const fileName = /filename="([^"]+)"/.exec(payload)?.[1] ?? 'upload.pdf';
      const now = new Date().toISOString();
      const document: StubDocument = {
        id: this.id('document'),
        fileName,
        contentType: 'application/pdf',
        sizeBytes: payload.length,
        status: 'Processing',
        errorMessage: null,
        pageCount: 4,
        chunkCount: 12,
        createdAt: now,
        updatedAt: now,
      };
      this.documents.unshift(document);
      return json(201, document);
    }

    if (path === '/documents/events' && method === 'GET') {
      const events = this.documents
        .filter((document) => document.status === 'Processing')
        .map((document) => {
          document.status = 'Ready';
          return sse('status', {
            documentId: document.id,
            status: 'Ready',
            errorMessage: null,
            updatedAt: new Date().toISOString(),
          });
        });

      return route.fulfill({
        status: 200,
        contentType: 'text/event-stream',
        body: `: connected\n\n${events.join('')}`,
      });
    }

    const documentMatch = /^\/documents\/([^/]+)$/.exec(path);
    if (documentMatch) {
      const document = this.documents.find((candidate) => candidate.id === documentMatch[1]);
      if (!document) {
        return route.fulfill(problem(404, 'documents.not_found'));
      }

      if (method === 'DELETE') {
        this.documents.splice(this.documents.indexOf(document), 1);
        return route.fulfill({ status: 204 });
      }

      return json(200, document);
    }

    return route.fulfill(problem(404, 'not_found'));
  }

  private async handleConversation(route: Route, id: string, isMessages: boolean): Promise<void> {
    const request = route.request();
    const method = request.method();
    const conversation = this.conversations.find((candidate) => candidate.id === id);

    if (!conversation) {
      return route.fulfill(problem(404, 'conversations.not_found'));
    }

    const { messages: _messages, ...summary } = conversation;

    if (isMessages && method === 'POST') {
      const { content } = request.postDataJSON() as { content: string };
      const now = new Date().toISOString();
      conversation.messages.push({
        id: this.id('message'),
        role: 'User',
        content,
        citations: [],
        usage: null,
        createdAt: now,
      });

      const citation = { documentId: this.documents[0]?.id ?? 'document-0', ...CITATION };
      const answer = ANSWER_TOKENS.join('');
      const messageId = this.id('message');
      conversation.messages.push({
        id: messageId,
        role: 'Assistant',
        content: answer,
        citations: [citation],
        usage: null,
        createdAt: now,
      });
      if (conversation.title === 'New conversation') {
        conversation.title = content.slice(0, 60);
      }
      conversation.updatedAt = now;

      const body = [
        sse('tool', { name: 'search_documents', status: 'Running' }),
        sse('tool', { name: 'search_documents', status: 'Completed' }),
        sse('citation', citation),
        ...ANSWER_TOKENS.map((text) => sse('token', { text })),
        sse('done', { messageId, usage: { inputTokens: 10, outputTokens: 12, totalTokens: 22 } }),
      ].join('');

      return route.fulfill({ status: 200, contentType: 'text/event-stream', body });
    }

    if (method === 'PATCH') {
      const { title } = request.postDataJSON() as { title: string };
      conversation.title = title;
      return route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ ...summary, title }),
      });
    }

    if (method === 'DELETE') {
      this.conversations.splice(this.conversations.indexOf(conversation), 1);
      return route.fulfill({ status: 204 });
    }

    return route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify(conversation),
    });
  }
}
