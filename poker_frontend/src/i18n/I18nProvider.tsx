import { createContext, useContext, useMemo, useState, type ReactNode } from 'react';
import { detectLang, translate, type Lang, type TranslationKey } from './index';

interface I18nContextValue {
  lang: Lang;
  t: (key: TranslationKey) => string;
  /** Dev-only: switch language at runtime (used by the dev toggle). */
  setLang: (lang: Lang) => void;
}

const I18nContext = createContext<I18nContextValue | null>(null);

/** Provides the active language (detected from Telegram) and a `t` translator. */
export function I18nProvider({ children, lang }: { children: ReactNode; lang?: Lang }) {
  const [override, setOverride] = useState<Lang | null>(null);

  const value = useMemo<I18nContextValue>(() => {
    const active = override ?? lang ?? detectLang();
    return { lang: active, t: (key) => translate(key, active), setLang: setOverride };
  }, [override, lang]);

  return <I18nContext.Provider value={value}>{children}</I18nContext.Provider>;
}

/** Access the translator: `const { t } = useI18n(); t('leaderboard.title')`. */
export function useI18n(): I18nContextValue {
  const ctx = useContext(I18nContext);
  if (!ctx) {
    throw new Error('useI18n must be used within an I18nProvider');
  }
  return ctx;
}
