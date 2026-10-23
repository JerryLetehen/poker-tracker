// Minimal typings and helpers for the Telegram Mini App (Web App) SDK.
// The SDK is loaded from https://telegram.org/js/telegram-web-app.js (added in index.html)
// and exposes window.Telegram.WebApp when the page runs inside Telegram.

export interface TelegramWebApp {
  initData: string;
  initDataUnsafe?: {
    user?: {
      id: number;
      first_name?: string;
      last_name?: string;
      username?: string;
    };
  };
  ready?: () => void;
  expand?: () => void;
}

declare global {
  interface Window {
    Telegram?: { WebApp?: TelegramWebApp };
  }
}

/** Returns the Telegram WebApp instance if the page is running inside Telegram. */
export function getWebApp(): TelegramWebApp | undefined {
  return window.Telegram?.WebApp;
}

/**
 * Returns the signed initData string to send to the API, or an empty string when
 * not running inside Telegram. In local dev (VITE_DEV_MOCK_AUTH=true) this can be
 * empty — the API's dev-mock auth will accept the request.
 */
export function getInitData(): string {
  const webApp = getWebApp();
  if (webApp?.initData) {
    return webApp.initData;
  }
  // Allow a dev override so the app can run in a plain browser during development.
  if (import.meta.env.VITE_DEV_MOCK_AUTH === 'true') {
    return import.meta.env.VITE_DEV_INIT_DATA ?? '';
  }
  return '';
}

/** True when the page is actually running inside the Telegram client. */
export function isInsideTelegram(): boolean {
  return Boolean(getWebApp()?.initData);
}

/** Signals readiness and expands the viewport (no-op outside Telegram). */
export function initTelegram(): void {
  const webApp = getWebApp();
  webApp?.ready?.();
  webApp?.expand?.();
}
