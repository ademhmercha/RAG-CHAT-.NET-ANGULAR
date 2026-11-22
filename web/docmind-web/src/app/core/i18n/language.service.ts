import { DOCUMENT } from '@angular/common';
import { Injectable, inject, signal } from '@angular/core';
import { TranslocoService } from '@jsverse/transloco';
import { firstValueFrom } from 'rxjs';
import { PREFERENCES_STORAGE } from '../services/storage';

export const SUPPORTED_LANGUAGES = ['en', 'fr'] as const;
export type AppLanguage = (typeof SUPPORTED_LANGUAGES)[number];

export const DEFAULT_LANGUAGE: AppLanguage = 'en';
export const LANGUAGE_STORAGE_KEY = 'dm.language';

export function isSupportedLanguage(value: string | null | undefined): value is AppLanguage {
  return (
    value !== null &&
    value !== undefined &&
    (SUPPORTED_LANGUAGES as readonly string[]).includes(value)
  );
}

export function pickInitialLanguage(
  stored: string | null,
  browserLanguages: readonly string[],
): AppLanguage {
  if (isSupportedLanguage(stored)) {
    return stored;
  }

  for (const candidate of browserLanguages) {
    const base = candidate.toLowerCase().split('-')[0];
    if (isSupportedLanguage(base)) {
      return base;
    }
  }

  return DEFAULT_LANGUAGE;
}

@Injectable({ providedIn: 'root' })
export class LanguageService {
  private readonly transloco = inject(TranslocoService);
  private readonly storage = inject(PREFERENCES_STORAGE);
  private readonly document = inject(DOCUMENT);

  readonly supported = SUPPORTED_LANGUAGES;
  readonly current = signal<AppLanguage>(DEFAULT_LANGUAGE);

  async initialize(): Promise<void> {
    const browserLanguages = this.document.defaultView?.navigator.languages ?? [];
    await this.apply(pickInitialLanguage(this.storage.get(LANGUAGE_STORAGE_KEY), browserLanguages));
  }

  async use(language: AppLanguage): Promise<void> {
    this.storage.set(LANGUAGE_STORAGE_KEY, language);
    await this.apply(language);
  }

  private async apply(language: AppLanguage): Promise<void> {
    this.transloco.setActiveLang(language);
    await firstValueFrom(this.transloco.load(language));
    this.current.set(language);
    this.document.documentElement.lang = language;
  }
}
