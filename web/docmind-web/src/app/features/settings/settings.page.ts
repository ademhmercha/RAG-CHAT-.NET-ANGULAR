import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatButtonToggleModule } from '@angular/material/button-toggle';
import { MatIconModule } from '@angular/material/icon';
import { TranslocoPipe } from '@jsverse/transloco';
import { AuthStore } from '../../core/auth/auth.store';
import { LanguageService } from '../../core/i18n/language.service';
import type { AppLanguage } from '../../core/i18n/language.service';
import { THEME_PREFERENCES, ThemeService } from '../../core/services/theme.service';
import type { ThemePreference } from '../../core/services/theme.service';

const THEME_ICONS: Readonly<Record<ThemePreference, string>> = {
  system: 'brightness_auto',
  light: 'light_mode',
  dark: 'dark_mode',
};

@Component({
  selector: 'dm-settings-page',
  imports: [MatButtonModule, MatButtonToggleModule, MatIconModule, TranslocoPipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './settings.page.html',
  styleUrl: './settings.page.scss',
})
export class SettingsPage {
  protected readonly auth = inject(AuthStore);
  protected readonly theme = inject(ThemeService);
  private readonly languageService = inject(LanguageService);

  protected readonly themes = THEME_PREFERENCES;
  protected readonly languages = this.languageService.supported;
  protected readonly language = this.languageService.current;

  protected themeIcon(preference: ThemePreference): string {
    return THEME_ICONS[preference];
  }

  protected setTheme(preference: ThemePreference): void {
    this.theme.set(preference);
  }

  protected setLanguage(language: AppLanguage): void {
    void this.languageService.use(language);
  }

  protected signOut(): void {
    void this.auth.logout();
  }
}
