import { describe, it, expect, vi, afterEach } from 'vitest';
import { render, screen, waitFor, cleanup } from '@testing-library/react';
import type { ReactNode } from 'react';
import AuthGate from './AuthGate';
import { ApiAuthError } from './client';
import { I18nProvider } from '../i18n/I18nProvider';

// Mock the client module so we control the access probe result.
vi.mock('./client', async () => {
  const actual = await vi.importActual<typeof import('./client')>('./client');
  return { ...actual, apiFetch: vi.fn() };
});

import { apiFetch } from './client';

// Render the gate inside the i18n provider forced to English for stable assertions.
function renderGate(children: ReactNode) {
  return render(
    <I18nProvider lang="en">
      <AuthGate>{children}</AuthGate>
    </I18nProvider>,
  );
}

describe('AuthGate', () => {
  afterEach(() => {
    cleanup();
    vi.resetAllMocks();
  });

  it('renders children when access is granted', async () => {
    vi.mocked(apiFetch).mockResolvedValue([]);
    renderGate(<div>Secret content</div>);
    await waitFor(() => expect(screen.getByText('Secret content')).toBeInTheDocument());
  });

  it('shows the pending screen with the telegram id', async () => {
    vi.mocked(apiFetch).mockRejectedValue(new ApiAuthError('pending', 'waiting', 424242));
    renderGate(<div>Secret content</div>);

    await waitFor(() => expect(screen.getByText('Access pending')).toBeInTheDocument());
    expect(screen.getByTestId('telegram-id')).toHaveTextContent('424242');
    expect(screen.queryByText('Secret content')).not.toBeInTheDocument();
  });

  it('shows the "open from Telegram" screen when unauthenticated', async () => {
    vi.mocked(apiFetch).mockRejectedValue(new ApiAuthError('unauthenticated', 'nope'));
    renderGate(<div>Secret content</div>);
    await waitFor(() => expect(screen.getByText('Open from Telegram')).toBeInTheDocument());
  });

  it('shows the denied screen for denied access', async () => {
    vi.mocked(apiFetch).mockRejectedValue(new ApiAuthError('denied', 'no access', 7));
    renderGate(<div>Secret content</div>);
    await waitFor(() => expect(screen.getByText('No access')).toBeInTheDocument());
  });
});
