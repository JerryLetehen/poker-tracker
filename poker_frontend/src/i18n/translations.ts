// =============================================================================
// Localization file — PUBLIC frontend.
//
// Review/edit translations here. Each key maps to a string per supported language.
// Supported languages: "en" (default) and "ru". Users whose Telegram language is
// Ukrainian ("uk") or Russian ("ru") see the "ru" set; everyone else sees "en".
//
// Guidelines followed:
//  - Poker terminology (buy-in = бай-ин, cash-out = кэш-аут, pot = банк, net = итог).
//  - Translations kept close in length to the English so layouts don't break.
// =============================================================================

export type Lang = 'en' | 'ru';

export const DEFAULT_LANG: Lang = 'en';

/** All UI strings, keyed by a stable id. Add new keys here with both languages. */
export const translations = {
  // --- Navigation (aria labels / tooltips; the nav itself uses emoji icons) ---
  'nav.leaderboard': { en: 'Leaderboard', ru: 'Рейтинг' },
  'nav.games': { en: 'Games', ru: 'Игры' },
  'nav.players': { en: 'Players', ru: 'Игроки' },
  'nav.records': { en: 'Records', ru: 'Рекорды' },
  'nav.animated': { en: 'Timeline', ru: 'Хроника' },
  'nav.race': { en: 'Chart', ru: 'График' },

  // --- Leaderboard ---
  'leaderboard.title': { en: 'Leaderboard', ru: 'Рейтинг' },
  'leaderboard.net': { en: 'Net', ru: 'Итог' },
  'leaderboard.rate': { en: 'Rate', ru: 'Среднее' },
  'leaderboard.col.name': { en: 'Name', ru: 'Имя' },
  'leaderboard.col.games': { en: 'Games', ru: 'Игры' },

  // --- Games list ---
  'games.title': { en: 'Games', ru: 'Игры' },
  'games.players': { en: 'players', ru: 'игроков' },
  'games.pot': { en: 'Pot', ru: 'Банк' },

  // --- Game detail ---
  'game.pot': { en: 'Pot', ru: 'Банк' },
  'game.players': { en: 'players', ru: 'игроков' },
  'game.in': { en: 'In', ru: 'Вход' },
  'game.out': { en: 'Out', ru: 'Выход' },

  // --- Players list ---
  'players.title': { en: 'Players', ru: 'Игроки' },

  // --- Player profile ---
  'player.games': { en: 'Games', ru: 'Игры' },
  'player.net': { en: 'Net', ru: 'Итог' },
  'player.rate': { en: 'Rate', ru: 'Ставка' },
  'player.best': { en: 'Best', ru: 'Лучший' },
  'player.worst': { en: 'Worst', ru: 'Худший' },
  'player.peak': { en: 'Peak', ru: 'Пик' },
  'player.low': { en: 'Low', ru: 'Дно' },
  'player.maxBuyIn': { en: 'Max Buy-in', ru: 'Макс. бай-ин' },
  'player.gamesList': { en: 'Games:', ru: 'Игры:' },
  'player.cumulative': { en: 'Cumulative Result', ru: 'Итог нарастающим' },

  // --- Records ---
  'records.title': { en: 'Records', ru: 'Рекорды' },
  'records.biggestWin': { en: '🏆 Biggest Win (single game)', ru: '🏆 Крупнейший выигрыш (за игру)' },
  'records.biggestLoss': { en: '💀 Biggest Loss (single game)', ru: '💀 Крупнейший проигрыш (за игру)' },
  'records.biggestBuyIn': { en: '🎰 Biggest Buy-in (top 5 players)', ru: '🎰 Крупнейший бай-ин (топ-5)' },
  'records.biggestPot': { en: '💰 Biggest Pot (top 5 games)', ru: '💰 Крупнейший банк (топ-5 игр)' },
  'records.col.player': { en: 'Player', ru: 'Игрок' },
  'records.col.game': { en: 'Game', ru: 'Игра' },
  'records.col.net': { en: 'Net', ru: 'Итог' },
  'records.col.buyIn': { en: 'Buy-in', ru: 'Бай-ин' },
  'records.col.pot': { en: 'Pot', ru: 'Банк' },

  // --- Timeline (animated leaderboard) & Race (chart) pages ---
  'timeline.title': { en: 'Timeline', ru: 'Хроника' },
  'race.title': { en: 'Race', ru: 'Гонка' },

  // --- Common ---
  'common.loading': { en: 'Loading...', ru: 'Загрузка...' },

  // --- Access screens (AuthGate) ---
  'access.loading': { en: 'Loading…', ru: 'Загрузка…' },
  'access.openFromTelegram.title': { en: 'Open from Telegram', ru: 'Откройте в Telegram' },
  'access.openFromTelegram.body': {
    en: 'This app is only available through its Telegram bot. Please open it from the bot.',
    ru: 'Приложение доступно только через Telegram-бота. Откройте его из бота.',
  },
  'access.pending.title': { en: 'Access pending', ru: 'Ожидает доступа' },
  'access.pending.body': { en: 'Your request is waiting for approval.', ru: 'Ваш запрос ожидает одобрения.' },
  'access.denied.title': { en: 'No access', ru: 'Нет доступа' },
  'access.denied.body': { en: 'You do not have access to this app.', ru: 'У вас нет доступа к приложению.' },
  'access.idHint': {
    en: 'Share this Telegram ID with the admin to get access:',
    ru: 'Отправьте этот Telegram ID админу для доступа:',
  },
} as const;

export type TranslationKey = keyof typeof translations;
