import { getInitData } from './telegram';

// This build is the ADMIN Mini App; it identifies as "admin" so the API validates
// against the admin bot token and enforces admin-scope access.
const APP_KIND = 'admin';

const API_BASE_URL = (import.meta.env.VITE_API_BASE_URL ?? '/api').replace(/\/$/, '');

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
 * Performs an authenticated admin API request. Attaches the Telegram initData and the
 * X-App: admin header, and maps 401/403 responses to ApiAuthError.
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

/** Convenience helper for JSON POST/PUT requests. */
export function apiJson<T>(path: string, method: string, body: unknown): Promise<T> {
  return apiFetch<T>(path, {
    method,
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(body),
  });
}

async function safeJson<T>(response: Response): Promise<T | undefined> {
  try {
    return (await response.json()) as T;
  } catch {
    return undefined;
  }
}
