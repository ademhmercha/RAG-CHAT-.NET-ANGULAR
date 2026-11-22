import { TestBed } from '@angular/core/testing';
import { MemoryStorage } from '../../../testing/memory-storage';
import { PREFERENCES_STORAGE } from './storage';
import { THEME_STORAGE_KEY, ThemeService } from './theme.service';

function createService(stored?: string): { service: ThemeService; storage: MemoryStorage } {
  const storage = new MemoryStorage();
  if (stored) {
    storage.set(THEME_STORAGE_KEY, stored);
  }

  TestBed.configureTestingModule({
    providers: [{ provide: PREFERENCES_STORAGE, useValue: storage }],
  });
  return { service: TestBed.inject(ThemeService), storage };
}

describe('ThemeService', () => {
  afterEach(() => {
    document.documentElement.classList.remove('theme-light', 'theme-dark');
  });

  it('follows the system by default', () => {
    const { service } = createService();

    expect(service.preference()).toBe('system');
    TestBed.tick();
    expect(document.documentElement.classList.contains('theme-light')).toBe(false);
    expect(document.documentElement.classList.contains('theme-dark')).toBe(false);
  });

  it('restores a stored preference', () => {
    const { service } = createService('dark');

    expect(service.preference()).toBe('dark');
    expect(service.resolved()).toBe('dark');
  });

  it('ignores a corrupted stored value', () => {
    const { service } = createService('purple');

    expect(service.preference()).toBe('system');
  });

  it('applies and persists an explicit choice', () => {
    const { service, storage } = createService();

    service.set('dark');
    TestBed.tick();

    expect(document.documentElement.classList.contains('theme-dark')).toBe(true);
    expect(storage.get(THEME_STORAGE_KEY)).toBe('dark');

    service.set('light');
    TestBed.tick();

    expect(document.documentElement.classList.contains('theme-dark')).toBe(false);
    expect(document.documentElement.classList.contains('theme-light')).toBe(true);
  });
});
