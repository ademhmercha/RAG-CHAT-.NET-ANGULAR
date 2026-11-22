import { parseSse } from './sse-parser';
import type { SseMessage } from './sse-parser';

function streamOf(...chunks: string[]): ReadableStream<Uint8Array> {
  const encoder = new TextEncoder();
  return new ReadableStream({
    start(controller) {
      for (const chunk of chunks) {
        controller.enqueue(encoder.encode(chunk));
      }
      controller.close();
    },
  });
}

async function collect(stream: ReadableStream<Uint8Array>): Promise<SseMessage[]> {
  const messages: SseMessage[] = [];
  for await (const message of parseSse(stream)) {
    messages.push(message);
  }
  return messages;
}

describe('parseSse', () => {
  it('parses named events', async () => {
    const messages = await collect(
      streamOf('event: token\ndata: {"text":"Hi"}\n\nevent: done\ndata: {}\n\n'),
    );

    expect(messages).toEqual([
      { event: 'token', data: '{"text":"Hi"}' },
      { event: 'done', data: '{}' },
    ]);
  });

  it('reassembles events split across chunks, even inside a UTF-8 character', async () => {
    const bytes = new TextEncoder().encode('event: token\ndata: é\n\n');
    const stream = new ReadableStream<Uint8Array>({
      start(controller) {
        controller.enqueue(bytes.slice(0, 20));
        controller.enqueue(bytes.slice(20));
        controller.close();
      },
    });

    expect(await collect(stream)).toEqual([{ event: 'token', data: 'é' }]);
  });

  it('joins multi-line data and handles CRLF', async () => {
    const messages = await collect(streamOf('event: token\r\ndata: a\r\ndata: b\r\n\r\n'));

    expect(messages).toEqual([{ event: 'token', data: 'a\nb' }]);
  });

  it('ignores keep-alive comments and blocks without data', async () => {
    const messages = await collect(
      streamOf(': keep-alive\n\nevent: ping\n\nevent: x\ndata: 1\n\n'),
    );

    expect(messages).toEqual([{ event: 'x', data: '1' }]);
  });

  it('defaults the event name to message and flushes a trailing block', async () => {
    expect(await collect(streamOf('data: tail'))).toEqual([{ event: 'message', data: 'tail' }]);
  });

  it('stops when the signal is aborted', async () => {
    const controller = new AbortController();
    controller.abort();

    await expect(async () => {
      for await (const message of parseSse(streamOf('data: 1\n\n'), controller.signal)) {
        expect(message).toBeUndefined();
      }
    }).rejects.toThrow();
  });
});
