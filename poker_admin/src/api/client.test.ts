import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { apiFetch, apiJson, ApiAuthError } from './client';

describe('admin apiFetch', () => {
  const originalTelegram = window.Telegram;

  beforeEach(() => {
    window.Telegram = { WebApp: { initData: 'auth_date=1&user=%7B%22id%22%3A1%7D&hash=abc' } };
  });

  afterEach(() => {
    window.Telegram = originalTelegram;
    vi.restoreAllMocks();
  });

  it('sends X-App: admin and the auth header', async () => {
    const fetchMock = vi.fn().mockResolvedValue(
      new Response(JSON.stringify([]), { status: 200, headers: { 'Content-Type': 'application/json' } }),
    );
    vi.stubGlobal('fetch', fetchMock);

    await apiFetch('/admin/players');

    const headers = fetchMock.mock.calls[0][1].headers as Headers;
    expect(headers.get('X-App')).toBe('admin');
    expect(headers.get('Authorization')).toContain('tma ');
  });

  it('apiJson posts a JSON body', async () => {
    const fetchMock = vi.fn().mockResolvedValue(new Response('{}', { status: 200, headers: { 'Content-Type': 'application/json' } }));
    vi.stubGlobal('fetch', fetchMock);

    await apiJson('/admin/access/users', 'POST', { telegramUserId: 5, scope: 'public' });

    const init = fetchMock.mock.calls[0][1];
    expect(init.method).toBe('POST');
    expect(JSON.parse(init.body)).toEqual({ telegramUserId: 5, scope: 'public' });
  });

  it('maps 403 denied to ApiAuthError', async () => {
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(
      new Response(JSON.stringify({ reason: 'denied', message: 'no' }), { status: 403 }),
    ));

    await expect(apiFetch('/admin/players')).rejects.toBeInstanceOf(ApiAuthError);
  });
});
