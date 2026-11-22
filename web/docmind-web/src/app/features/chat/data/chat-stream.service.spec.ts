import { decodeChatEvent } from './chat-stream.service';

describe('decodeChatEvent', () => {
  it('decodes tokens', () => {
    expect(decodeChatEvent('token', '{"text":"Bonjour"}')).toEqual({
      type: 'token',
      text: 'Bonjour',
    });
  });

  it('normalizes the tool status', () => {
    expect(decodeChatEvent('tool', '{"name":"search_documents","status":"Running"}')).toEqual({
      type: 'tool',
      name: 'search_documents',
      status: 'running',
    });
  });

  it('decodes citations and completion', () => {
    const citation = { documentId: 'd1', documentName: 'a.pdf', page: 3, excerpt: 'x' };

    expect(decodeChatEvent('citation', JSON.stringify(citation))).toEqual({
      type: 'citation',
      citation,
    });
    expect(decodeChatEvent('done', '{"messageId":"m1"}')).toEqual({
      type: 'done',
      messageId: 'm1',
    });
  });

  it('turns an error event into an API problem', () => {
    const event = decodeChatEvent(
      'error',
      '{"status":503,"code":"chat.provider_unavailable","detail":"down"}',
    );

    expect(event).toMatchObject({ type: 'error', error: { status: 503 } });
  });

  it('ignores malformed or unknown events', () => {
    expect(decodeChatEvent('token', 'not json')).toBeNull();
    expect(decodeChatEvent('token', '{}')).toBeNull();
    expect(decodeChatEvent('mystery', '{}')).toBeNull();
  });
});
