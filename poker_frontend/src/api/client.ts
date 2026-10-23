import { getInitData } from './telegram';

// Which Mini App this build represents. The public frontend always identifies as "public".
const APP_KIND = 'public';

// Base URL for API calls. Defaults to "/api" so the Vite dev proxy (and the Caddy
// reverse proxy in production) can forward to the backend. Override per build with
// VITE_API_BASE_URL when the API lives on a different origin.
const API_BASE_URL = (import.meta.env.VITE_API_BASE_URL ?? '/api').replace(/\/$/, '');

/** Distinguishes auth outcomes so the UI can show the right screen. */
export type AuthErrorReason = 'unauthenticated' | 'pending' | 'denied';

export class ApiAuthError extends Error {
  readonly reason: AuthErrorReason;
  readonly telegramUserId?: number;

  constructor(reason: AuthErrorReason, message: string, telegramUserId?: number) {
    super(message);
    this.name = 'ApiAuthError';
    this.reason = reason;
    this.telegramUserId = telegramUserId;
  }
}

export class ApiError extends Error {
  readonly status: number;

  constructor(status: number, message: string) {
    super(message);
    this.name = 'ApiError';
    this.status = status;
  }
}

interface AccessErrorBody {
  reason?: AuthErrorReason;
  message?: string;
  telegramUserId?: number;
}

/**
 * Performs an authenticated API request. Attaches the Telegram initData and the X-App
 * header, resolves JSON, and maps 401/403 responses to ApiAuthError so the app can
 * render the pending/denied/unauthenticated states.
 */
export async function apiFetch<T>(path: string, init?: RequestInit): Promise<T> {
  const headers = new Headers(init?.headers);
  headers.set('X-App', APP_KIND);

  const initData = getInitData();
  if (initData) {
    headers.set('Authorization', `tma ${initData}`);
  }

  const url = path.startsWith('http') ? path : `${API_BASE_URL}${path}`;
  const response = await fetch(url, { ...init, headers });

  if (response.status === 401 || response.status === 403) {
    const body = await safeJson<AccessErrorBody>(response);
    const reason: AuthErrorReason =
      body?.reason ?? (response.status === 401 ? 'unauthenticated' : 'denied');
    throw new ApiAuthError(reason, body?.message ?? 'Access error', body?.telegramUserId);
  }

  if (!response.ok) {
    throw new ApiError(response.status, `Request failed: ${response.status}`);
  }

  if (response.status === 204) {
    return undefined as T;
  }

  return (await response.json()) as T;
}

async function safeJson<T>(response: Response): Promise<T | undefined> {
  try {
    return (await response.json()) as T;
  } catch {
    return undefined;
  }
}
