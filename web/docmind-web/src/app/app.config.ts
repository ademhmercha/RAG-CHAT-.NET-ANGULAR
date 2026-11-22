import { provideHttpClient, withFetch, withInterceptors } from '@angular/common/http';
import type { ApplicationConfig } from '@angular/core';
import {
  inject,
  isDevMode,
  provideAppInitializer,
  provideBrowserGlobalErrorListeners,
} from '@angular/core';
import { MAT_ICON_DEFAULT_OPTIONS } from '@angular/material/icon';
import { MAT_SNACK_BAR_DEFAULT_OPTIONS } from '@angular/material/snack-bar';
import {
  PreloadAllModules,
  TitleStrategy,
  provideRouter,
  withComponentInputBinding,
  withPreloading,
  withViewTransitions,
} from '@angular/router';
import { provideTransloco } from '@jsverse/transloco';
import { routes } from './app.routes';
import { AuthStore } from './core/auth/auth.store';
import { APP_CONFIG, type AppConfig } from './core/config/app-config';
import {
  DEFAULT_LANGUAGE,
  LanguageService,
  SUPPORTED_LANGUAGES,
} from './core/i18n/language.service';
import { TranslocoHttpLoader } from './core/i18n/transloco-loader';
import { authInterceptor } from './core/interceptors/auth.interceptor';
import { errorInterceptor } from './core/interceptors/error.interceptor';
import { AppTitleStrategy } from './core/services/title-strategy';

export function buildAppConfig(config: AppConfig): ApplicationConfig {
  return {
    providers: [
      { provide: APP_CONFIG, useValue: config },
      provideBrowserGlobalErrorListeners(),
      provideHttpClient(withFetch(), withInterceptors([errorInterceptor, authInterceptor])),
      provideRouter(
        routes,
        withComponentInputBinding(),
        withViewTransitions(),
        withPreloading(PreloadAllModules),
      ),
      { provide: TitleStrategy, useExisting: AppTitleStrategy },
      provideTransloco({
        config: {
          availableLangs: [...SUPPORTED_LANGUAGES],
          defaultLang: DEFAULT_LANGUAGE,
          fallbackLang: DEFAULT_LANGUAGE,
          reRenderOnLangChange: true,
          prodMode: !isDevMode(),
        },
        loader: TranslocoHttpLoader,
      }),
      provideAppInitializer(() => inject(LanguageService).initialize()),
      provideAppInitializer(() => inject(AuthStore).restoreSession()),
      { provide: MAT_ICON_DEFAULT_OPTIONS, useValue: { fontSet: 'material-icons-outlined' } },
      { provide: MAT_SNACK_BAR_DEFAULT_OPTIONS, useValue: { duration: 5000 } },
    ],
  };
}
