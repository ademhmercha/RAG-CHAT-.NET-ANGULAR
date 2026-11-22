import { DOCUMENT } from '@angular/common';
import { Injectable, computed, effect, inject, signal } from '@angular/core';
import { PREFERENCES_STORAGE } from './storage';

export type ThemePreference = 'system' | 'light' | 'dark';

export const THEME_STORAGE_KEY = 'dm.theme';
export const THEME_PREFERENCES: readonly ThemePreference[] = ['system', 'light', 'dark'];

function isThemePreference(value: string | null): value is ThemePreference {
  return value !== null && (THEME_PREFERENCES as readonly string[]).includes(value);
}

@Injectable({ providedIn: 'root' })
export class ThemeService {
  private readonly document = inject(DOCUMENT);
  private readonly storage = inject(PREFERENCES_STORAGE);
  private readonly systemQuery = this.document.defaultView?.matchMedia?.(
    '(prefers-color-scheme: dark)',
  );

  private readonly systemIsDark = signal(this.systemQuery?.matches ?? false);
  private readonly stored = this.storage.get(THEME_STORAGE_KEY);

  readonly preference = signal<ThemePreference>(
    isThemePreference(this.stored) ? this.stored : 'system',
  );
  readonly resolved = computed<'light' | 'dark'>(() => {
    const preference = this.preference();
    return preference === 'system' ? (this.systemIsDark() ? 'dark' : 'light') : preference;
  });

  constructor() {
    this.systemQuery?.addEventListener('change', (event) => this.systemIsDark.set(event.matches));

    effect(() => {
      const preference = this.preference();
      const root = this.document.documentElement;
      root.classList.toggle('theme-light', preference === 'light');
      root.classList.toggle('theme-dark', preference === 'dark');
    });
  }

  set(preference: ThemePreference): void {
    this.preference.set(preference);
    this.storage.set(THEME_STORAGE_KEY, preference);
  }
}
