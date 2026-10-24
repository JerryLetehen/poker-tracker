import { describe, it, expect } from 'vitest';
import { resolveLang, translate } from './index';
import { translations } from './translations';

describe('resolveLang', () => {
  it('maps Russian to ru', () => {
    expect(resolveLang('ru')).toBe('ru');
    expect(resolveLang('RU')).toBe('ru');
    expect(resolveLang('ru-RU')).toBe('ru');
  });

  it('maps Ukrainian to ru (per product decision)', () => {
    expect(resolveLang('uk')).toBe('ru');
    expect(resolveLang('uk-UA')).toBe('ru');
  });

  it('falls back to en for everything else', () => {
    expect(resolveLang('en')).toBe('en');
    expect(resolveLang('en-US')).toBe('en');
    expect(resolveLang('de')).toBe('en');
    expect(resolveLang('')).toBe('en');
    expect(resolveLang(undefined)).toBe('en');
  });
});

describe('translate', () => {
  it('returns the requested language string', () => {
    expect(translate('leaderboard.title', 'en')).toBe('Leaderboard');
    expect(translate('leaderboard.title', 'ru')).toBe('Рейтинг');
  });
});

describe('translations completeness', () => {
  it('every key has both en and ru, both non-empty', () => {
    for (const [key, entry] of Object.entries(translations)) {
      expect(entry.en, `${key}.en`).toBeTruthy();
      expect(entry.ru, `${key}.ru`).toBeTruthy();
    }
  });
});
