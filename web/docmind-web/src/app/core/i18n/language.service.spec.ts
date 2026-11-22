import en from '../../../../public/i18n/en.json';
import fr from '../../../../public/i18n/fr.json';
import { DEFAULT_LANGUAGE, isSupportedLanguage, pickInitialLanguage } from './language.service';

function keysOf(value: unknown, prefix = ''): string[] {
  if (typeof value !== 'object' || value === null) {
    return [prefix];
  }

  return Object.entries(value).flatMap(([key, child]) =>
    keysOf(child, prefix ? `${prefix}.${key}` : key),
  );
}

function valueAt(document: unknown, key: string): unknown {
  return key
    .split('.')
    .reduce<unknown>((node, part) => (node as Record<string, unknown>)[part], document);
}

describe('pickInitialLanguage', () => {
  it('prefers the stored language', () => {
    expect(pickInitialLanguage('fr', ['en-US'])).toBe('fr');
  });

  it('falls back to the first supported browser language', () => {
    expect(pickInitialLanguage(null, ['de-DE', 'fr-CA', 'en-US'])).toBe('fr');
  });

  it('uses the default language when nothing matches', () => {
    expect(pickInitialLanguage('xx', ['de', 'es'])).toBe(DEFAULT_LANGUAGE);
  });

  it('validates language codes', () => {
    expect(isSupportedLanguage('en')).toBe(true);
    expect(isSupportedLanguage('de')).toBe(false);
    expect(isSupportedLanguage(null)).toBe(false);
  });
});

describe('translation files', () => {
  it('define exactly the same keys in English and French', () => {
    expect(keysOf(fr).sort()).toEqual(keysOf(en).sort());
  });

  it('never leave a value empty', () => {
    for (const key of keysOf(en)) {
      expect(valueAt(en, key)).not.toBe('');
      expect(valueAt(fr, key)).not.toBe('');
    }
  });
});
