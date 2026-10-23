import { describe, it, expect, vi, afterEach } from 'vitest';
import { render, screen, waitFor, cleanup, fireEvent } from '@testing-library/react';
import AccessPage from './AccessPage';

vi.mock('../api/client', () => ({
  apiFetch: vi.fn(),
  apiJson: vi.fn(),
}));

import { apiFetch, apiJson } from '../api/client';

describe('AccessPage', () => {
  afterEach(() => {
    cleanup();
    vi.resetAllMocks();
  });

  function mockData(pending: unknown[], users: unknown[]) {
    vi.mocked(apiFetch).mockImplementation(async (path: string) => {
      if (path === '/admin/access/pending') return pending;
      if (path === '/admin/access/users') return users;
      return undefined;
    });
  }

  it('renders pending requests with their telegram id', async () => {
    mockData(
      [{ id: 1, telegramUserId: 999, firstName: 'Jo', lastName: null, username: 'jo', firstSeenAt: '', lastSeenAt: '' }],
      [],
    );

    render(<AccessPage />);

    await waitFor(() => expect(screen.getByText(/Jo/)).toBeInTheDocument());
    expect(screen.getByText('999')).toBeInTheDocument();
    expect(screen.getByText('Approve → Public')).toBeInTheDocument();
  });

  it('approving a pending request calls the approve endpoint with the chosen scope', async () => {
    mockData(
      [{ id: 7, telegramUserId: 42, firstName: 'A', lastName: null, username: null, firstSeenAt: '', lastSeenAt: '' }],
      [],
    );
    vi.mocked(apiJson).mockResolvedValue(undefined);

    render(<AccessPage />);
    await waitFor(() => expect(screen.getByText('Approve → Admin')).toBeInTheDocument());

    fireEvent.click(screen.getByText('Approve → Admin'));

    await waitFor(() =>
      expect(apiJson).toHaveBeenCalledWith('/admin/access/pending/7/approve', 'POST', { scope: 'admin' }),
    );
  });

  it('renders the current allow-list', async () => {
    mockData(
      [],
      [{ id: 3, telegramUserId: 111, scope: 'admin', label: 'Me', addedAt: '' }],
    );

    render(<AccessPage />);

    await waitFor(() => expect(screen.getByText('Me')).toBeInTheDocument());
    expect(screen.getByText('admin')).toBeInTheDocument();
  });
});
