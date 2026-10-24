import { getWebApp } from '../api/telegram';
import { translations, DEFAULT_LANG, type Lang, type TranslationKey } from './translations';

/**
 * Resolves the UI language from a Telegram language code.
 * Ukrainian ("uk") and Russian ("ru") both map to the "ru" translation set;
 * everything else falls back to the default ("en").
 */
export function resolveLang(languageCode: string | undefined): Lang {
  const code = (languageCode ?? '').toLowerCase();
  if (code === 'ru' || code === 'uk' || code.startsWith('ru-') || code.startsWith('uk-')) {
    return 'ru';
  }
  return DEFAULT_LANG;
}

/** The active language, derived from the Telegram user's language_code. */
export function detectLang(): Lang {
  const code = getWebApp()?.initDataUnsafe?.user?.language_code;
  // Optional dev override for local testing without Telegram.
  const override = import.meta.env.VITE_FORCE_LANG as Lang | undefined;
  if (override === 'en' || override === 'ru') {
    return override;
  }
  return resolveLang(code);
}

/** Translate a key for the given language, falling back to English then the key itself. */
export function translate(key: TranslationKey, lang: Lang): string {
  const entry = translations[key];
  if (!entry) return key;
  return entry[lang] ?? entry[DEFAULT_LANG] ?? key;
}

export type { Lang, TranslationKey };
