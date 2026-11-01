import { describe, it, expect, vi, afterEach, beforeEach } from 'vitest';
import { render, screen, waitFor, cleanup, fireEvent } from '@testing-library/react';

vi.mock('../api/client', () => ({
  apiFetch: vi.fn(),
  apiJson: vi.fn(),
}));

vi.mock('react-router-dom', () => ({
  useParams: () => ({ id: '42' }),
  useNavigate: () => vi.fn(),
}));

import GamePage from './GamePage';
import { apiFetch, apiJson } from '../api/client';

describe('GamePage publish results', () => {
  beforeEach(() => {
    // Admin game detail + players load on mount.
    vi.mocked(apiFetch).mockImplementation(async (path: string) => {
      if (path === '/admin/games/42') {
        return { date: '02-01-2024', gameType: 'texas-holdem-no-limit', buyInAmount: 50, checksum: 0, entries: [] };
      }
      if (path === '/admin/players') return [];
      return undefined;
    });
    vi.mocked(apiJson).mockResolvedValue({ ok: true });
    vi.spyOn(window, 'confirm').mockReturnValue(true);
  });

  afterEach(() => {
    cleanup();
    vi.resetAllMocks();
  });

  it('POSTs to the publish endpoint and shows success', async () => {
    render(<GamePage />);

    const button = await screen.findByText('Publish results');
    fireEvent.click(button);

    await waitFor(() => {
      const call = vi.mocked(apiJson).mock.calls.find(c => c[0] === '/admin/games/42/publish');
      expect(call).toBeDefined();
      expect(call![1]).toBe('POST');
    });

    await waitFor(() =>
      expect(screen.getByText('Results published to the group.')).toBeInTheDocument(),
    );
  });

  it('does nothing when the confirm is cancelled', async () => {
    vi.spyOn(window, 'confirm').mockReturnValue(false);
    render(<GamePage />);

    const button = await screen.findByText('Publish results');
    fireEvent.click(button);

    await waitFor(() => {
      const call = vi.mocked(apiJson).mock.calls.find(c => c[0] === '/admin/games/42/publish');
      expect(call).toBeUndefined();
    });
  });

  it('shows an error when publishing fails', async () => {
    vi.mocked(apiJson).mockRejectedValue(new Error('boom'));
    render(<GamePage />);

    const button = await screen.findByText('Publish results');
    fireEvent.click(button);

    await waitFor(() =>
      expect(screen.getByText('Failed to publish results. Please try again.')).toBeInTheDocument(),
    );
  });
});
