import { AppConfigError, loadAppConfig, parseAppConfig } from './app-config';

function jsonResponse(body: unknown, status = 200): Response {
  return new Response(JSON.stringify(body), { status });
}

describe('parseAppConfig', () => {
  it('accepts a same-origin configuration', () => {
    expect(parseAppConfig({ apiBaseUrl: '', environment: 'development' })).toEqual({
      apiBaseUrl: '',
      environment: 'development',
    });
  });

  it('removes trailing slashes from an absolute API URL', () => {
    const config = parseAppConfig({ apiBaseUrl: 'https://api.example.com//', environment: 'prod' });

    expect(config.apiBaseUrl).toBe('https://api.example.com');
  });

  it.each([
    [null, 'JSON object'],
    ['text', 'JSON object'],
    [{ environment: 'x' }, 'apiBaseUrl'],
    [{ apiBaseUrl: 42, environment: 'x' }, 'apiBaseUrl'],
    [{ apiBaseUrl: 'api.example.com', environment: 'x' }, 'absolute http(s) URL'],
    [{ apiBaseUrl: 'ftp://api.example.com', environment: 'x' }, 'absolute http(s) URL'],
    [{ apiBaseUrl: '' }, 'environment'],
    [{ apiBaseUrl: '', environment: '  ' }, 'environment'],
  ])('rejects %j', (raw, expectedMessage) => {
    expect(() => parseAppConfig(raw)).toThrowError(expectedMessage);
  });
});

describe('loadAppConfig', () => {
  it('fetches config.json without using the HTTP cache', async () => {
    const fetcher = vi
      .fn()
      .mockResolvedValue(jsonResponse({ apiBaseUrl: '', environment: 'test' }));

    const config = await loadAppConfig(fetcher, 'config.json');

    expect(config.environment).toBe('test');
    expect(fetcher).toHaveBeenCalledWith('config.json', { cache: 'no-store' });
  });

  it('reports HTTP failures', async () => {
    const fetcher = vi.fn().mockResolvedValue(jsonResponse({}, 404));

    await expect(loadAppConfig(fetcher)).rejects.toThrowError('HTTP 404');
  });

  it('reports network failures', async () => {
    const fetcher = vi.fn().mockRejectedValue(new TypeError('offline'));

    await expect(loadAppConfig(fetcher)).rejects.toBeInstanceOf(AppConfigError);
  });

  it('reports invalid JSON', async () => {
    const fetcher = vi.fn().mockResolvedValue(new Response('{ not json', { status: 200 }));

    await expect(loadAppConfig(fetcher)).rejects.toThrowError('not valid JSON');
  });
});
