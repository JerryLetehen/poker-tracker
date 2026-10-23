import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { apiFetch, ApiAuthError, ApiError } from './client';

describe('apiFetch', () => {
  const originalTelegram = window.Telegram;

  beforeEach(() => {
    // Simulate running inside Telegram with a signed initData blob.
    window.Telegram = { WebApp: { initData: 'auth_date=1&user=%7B%22id%22%3A1%7D&hash=abc' } };
  });

  afterEach(() => {
    window.Telegram = originalTelegram;
    vi.restoreAllMocks();
  });

  it('adds X-App and Authorization headers and returns parsed JSON', async () => {
    const fetchMock = vi.fn().mockResolvedValue(
      new Response(JSON.stringify(['a', 'b']), { status: 200, headers: { 'Content-Type': 'application/json' } }),
    );
    vi.stubGlobal('fetch', fetchMock);

    const result = await apiFetch<string[]>('/player-list');

    expect(result).toEqual(['a', 'b']);
    const [, init] = fetchMock.mock.calls[0];
    const headers = init.headers as Headers;
    expect(headers.get('X-App')).toBe('public');
    expect(headers.get('Authorization')).toBe('tma auth_date=1&user=%7B%22id%22%3A1%7D&hash=abc');
  });

  it('maps 401 to ApiAuthError(unauthenticated)', async () => {
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(
      new Response(JSON.stringify({ reason: 'unauthenticated', message: 'nope' }), { status: 401 }),
    ));

    await expect(apiFetch('/player-list')).rejects.toMatchObject({
      name: 'ApiAuthError',
      reason: 'unauthenticated',
    });
  });

  it('maps 403 pending to ApiAuthError(pending) with telegramUserId', async () => {
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(
      new Response(JSON.stringify({ reason: 'pending', message: 'wait', telegramUserId: 555 }), { status: 403 }),
    ));

    try {
      await apiFetch('/player-list');
      expect.unreachable('should have thrown');
    } catch (err) {
      expect(err).toBeInstanceOf(ApiAuthError);
      const authErr = err as ApiAuthError;
      expect(authErr.reason).toBe('pending');
      expect(authErr.telegramUserId).toBe(555);
    }
  });

  it('maps 403 denied to ApiAuthError(denied)', async () => {
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(
      new Response(JSON.stringify({ reason: 'denied', message: 'no' }), { status: 403 }),
    ));

    await expect(apiFetch('/admin/x')).rejects.toMatchObject({ reason: 'denied' });
  });

  it('throws ApiError for other non-ok statuses', async () => {
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(new Response('boom', { status: 500 })));

    await expect(apiFetch('/player-list')).rejects.toBeInstanceOf(ApiError);
  });
});
