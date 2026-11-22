import { InjectionToken } from '@angular/core';

export interface KeyValueStorage {
  get(key: string): string | null;
  set(key: string, value: string): void;
  remove(key: string): void;
}

export class SafeLocalStorage implements KeyValueStorage {
  get(key: string): string | null {
    try {
      return localStorage.getItem(key);
    } catch {
      return null;
    }
  }

  set(key: string, value: string): void {
    try {
      localStorage.setItem(key, value);
    } catch {
      // Storage can be unavailable (private mode, quota): preferences simply do not persist.
    }
  }

  remove(key: string): void {
    try {
      localStorage.removeItem(key);
    } catch {
      // Nothing to clean up when storage is unavailable.
    }
  }
}

export const PREFERENCES_STORAGE = new InjectionToken<KeyValueStorage>('PREFERENCES_STORAGE', {
  providedIn: 'root',
  factory: () => new SafeLocalStorage(),
});
