import { InjectionToken } from '@angular/core';

export interface AppConfig {
  readonly apiBaseUrl: string;
  readonly environment: string;
}

export const APP_CONFIG = new InjectionToken<AppConfig>('APP_CONFIG');

export class AppConfigError extends Error {
  constructor(message: string) {
    super(message);
    this.name = 'AppConfigError';
  }
}

export function parseAppConfig(raw: unknown): AppConfig {
  if (typeof raw !== 'object' || raw === null) {
    throw new AppConfigError('config.json must contain a JSON object.');
  }

  const { apiBaseUrl, environment } = raw as Record<string, unknown>;

  if (typeof apiBaseUrl !== 'string') {
    throw new AppConfigError(
      'config.json: "apiBaseUrl" must be a string (empty for the same origin).',
    );
  }

  const normalizedBaseUrl = apiBaseUrl.trim().replace(/\/+$/, '');
  if (normalizedBaseUrl !== '' && !/^https?:\/\/[^\s/]+/i.test(normalizedBaseUrl)) {
    throw new AppConfigError('config.json: "apiBaseUrl" must be empty or an absolute http(s) URL.');
  }

  if (typeof environment !== 'string' || environment.trim() === '') {
    throw new AppConfigError('config.json: "environment" must be a non-empty string.');
  }

  return { apiBaseUrl: normalizedBaseUrl, environment: environment.trim() };
}

export async function loadAppConfig(
  fetcher: typeof fetch = fetch,
  url = 'config.json',
): Promise<AppConfig> {
  let response: Response;

  try {
    response = await fetcher(url, { cache: 'no-store' });
  } catch {
    throw new AppConfigError(`config.json could not be loaded (network error).`);
  }

  if (!response.ok) {
    throw new AppConfigError(`config.json could not be loaded (HTTP ${response.status}).`);
  }

  try {
    return parseAppConfig(await response.json());
  } catch (error) {
    if (error instanceof AppConfigError) {
      throw error;
    }

    throw new AppConfigError('config.json is not valid JSON.');
  }
}
