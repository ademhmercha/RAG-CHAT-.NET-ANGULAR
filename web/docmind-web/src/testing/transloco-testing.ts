import { importProvidersFrom, type EnvironmentProviders } from '@angular/core';
import { TranslocoTestingModule } from '@jsverse/transloco';
import en from '../../public/i18n/en.json';
import fr from '../../public/i18n/fr.json';

export function provideTranslocoTesting(lang: 'en' | 'fr' = 'en'): EnvironmentProviders {
  return importProvidersFrom(
    TranslocoTestingModule.forRoot({
      langs: { en, fr },
      translocoConfig: { availableLangs: ['en', 'fr'], defaultLang: lang, fallbackLang: 'en' },
      preloadLangs: true,
    }),
  );
}
