import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatMenuModule } from '@angular/material/menu';
import { MatTooltipModule } from '@angular/material/tooltip';
import { TranslocoPipe } from '@jsverse/transloco';
import { LanguageService, type AppLanguage } from '../../core/i18n/language.service';
import {
  THEME_PREFERENCES,
  ThemeService,
  type ThemePreference,
} from '../../core/services/theme.service';

const THEME_ICONS: Record<ThemePreference, string> = {
  system: 'brightness_auto',
  light: 'light_mode',
  dark: 'dark_mode',
};

@Component({
  selector: 'dm-preferences-menu',
  imports: [MatButtonModule, MatIconModule, MatMenuModule, MatTooltipModule, TranslocoPipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <button
      mat-icon-button
      type="button"
      [matMenuTriggerFor]="languageMenu"
      [matTooltip]="'layout.language' | transloco"
      [attr.aria-label]="'layout.language' | transloco"
    >
      <mat-icon>translate</mat-icon>
    </button>
    <mat-menu #languageMenu="matMenu">
      @for (code of languages; track code) {
        <button
          mat-menu-item
          type="button"
          (click)="setLanguage(code)"
          [attr.aria-current]="code === language() ? 'true' : null"
        >
          <mat-icon>{{ code === language() ? 'check' : '' }}</mat-icon>
          <span>{{ 'language.' + code | transloco }}</span>
        </button>
      }
    </mat-menu>

    <button
      mat-icon-button
      type="button"
      [matMenuTriggerFor]="themeMenu"
      [matTooltip]="'layout.theme' | transloco"
      [attr.aria-label]="'layout.theme' | transloco"
    >
      <mat-icon>{{ themeIcon() }}</mat-icon>
    </button>
    <mat-menu #themeMenu="matMenu">
      @for (option of themePreferences; track option) {
        <button
          mat-menu-item
          type="button"
          (click)="setTheme(option)"
          [attr.aria-current]="option === themePreference() ? 'true' : null"
        >
          <mat-icon>{{ themeIconFor(option) }}</mat-icon>
          <span>{{ 'theme.' + option | transloco }}</span>
        </button>
      }
    </mat-menu>
  `,
  styles: `
    :host {
      display: inline-flex;
      align-items: center;
    }
  `,
})
export class PreferencesMenuComponent {
  private readonly theme = inject(ThemeService);
  private readonly languageService = inject(LanguageService);

  protected readonly themePreferences = THEME_PREFERENCES;
  protected readonly themePreference = this.theme.preference;
  protected readonly themeIcon = computed(() => THEME_ICONS[this.theme.preference()]);

  protected readonly languages = this.languageService.supported;
  protected readonly language = this.languageService.current;

  protected themeIconFor(preference: ThemePreference): string {
    return THEME_ICONS[preference];
  }

  protected setTheme(preference: ThemePreference): void {
    this.theme.set(preference);
  }

  protected setLanguage(language: AppLanguage): void {
    void this.languageService.use(language);
  }
}
