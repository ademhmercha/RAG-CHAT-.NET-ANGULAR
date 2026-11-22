import { TestBed } from '@angular/core/testing';
import type { ComponentFixture } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { patchState } from '@ngrx/signals';
import { AuthStore } from '../../core/auth/auth.store';
import { ThemeService } from '../../core/services/theme.service';
import { provideApiTesting } from '../../../testing/api-testing';
import { authResponse } from '../../../testing/auth-fixtures';
import { PREFERENCES_STORAGE } from '../../core/services/storage';
import { MemoryStorage } from '../../../testing/memory-storage';
import { provideTranslocoTesting } from '../../../testing/transloco-testing';
import { SettingsPage } from './settings.page';

describe('SettingsPage', () => {
  let fixture: ComponentFixture<SettingsPage>;
  const root = () => fixture.nativeElement as HTMLElement;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideApiTesting(),
        provideRouter([]),
        provideTranslocoTesting(),
        { provide: PREFERENCES_STORAGE, useValue: new MemoryStorage() },
      ],
    });

    const { user } = authResponse();
    patchState(TestBed.inject(AuthStore) as never, { user, status: 'authenticated' } as never);

    fixture = TestBed.createComponent(SettingsPage);
    fixture.detectChanges();
  });

  it('shows the signed-in profile', () => {
    expect(root().querySelector('[data-testid="profile-name"]')?.textContent).toBe('Ada Lovelace');
    expect(root().querySelector('[data-testid="profile-email"]')?.textContent).toBe(
      'ada@example.com',
    );
  });

  it('changes the theme', () => {
    const dark = Array.from(
      root().querySelectorAll<HTMLButtonElement>('mat-button-toggle button'),
    ).find((button) => button.textContent?.includes('Dark'));

    dark?.click();

    expect(TestBed.inject(ThemeService).preference()).toBe('dark');
  });
});
