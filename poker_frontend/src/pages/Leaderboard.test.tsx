import { describe, it, expect, vi, afterEach } from 'vitest';
import { render, screen, waitFor, within, fireEvent, cleanup } from '@testing-library/react';
import type { ReactElement } from 'react';
import { MemoryRouter } from 'react-router-dom';
import Leaderboard from './Leaderboard';
import { I18nProvider } from '../i18n/I18nProvider';

// Mock the client module so we control the leaderboard fetch result.
vi.mock('../api/client', async () => {
  const actual = await vi.importActual<typeof import('../api/client')>('../api/client');
  return { ...actual, apiFetch: vi.fn() };
});

import { apiFetch } from '../api/client';

type Entry = { name: string; games: number; result: number; rate: number };

function entry(name: string, overrides: Partial<Entry> = {}): Entry {
  return { name, games: 10, result: 100, rate: 10, ...overrides };
}

// Render the page inside a router (for <Link>) and the i18n provider.
function renderLeaderboard(lang: 'en' | 'ru' = 'en'): ReactElement {
  render(
    <I18nProvider lang={lang}>
      <MemoryRouter>
        <Leaderboard />
      </MemoryRouter>
    </I18nProvider>,
  );
  // Placeholder return keeps the helper usable as an expression if needed.
  return <></>;
}

describe('Leaderboard', () => {
  afterEach(() => {
    cleanup();
    vi.resetAllMocks();
  });

  // 13.1 Layout, columns, and ranks (Req 6.1, 6.4, 4.6)
  describe('layout, columns, and ranks', () => {
    it('renders the Active section above the All section', async () => {
      vi.mocked(apiFetch).mockResolvedValue({
        active: [entry('Alice'), entry('Bob')],
        all: [entry('Carol'), entry('Dave'), entry('Erin')],
      });
      renderLeaderboard('en');

      await screen.findByText('Active Players');

      const headings = screen.getAllByRole('heading', { level: 2 });
      expect(headings.map((h) => h.textContent)).toEqual(['Active Players', 'All Players']);
    });

    it('renders columns in order: #, Name, Games, Net, Rate', async () => {
      vi.mocked(apiFetch).mockResolvedValue({
        active: [entry('Alice')],
        all: [entry('Carol')],
      });
      renderLeaderboard('en');

      await screen.findByText('Active Players');

      const tables = screen.getAllByRole('table');
      expect(tables.length).toBe(2);

      const activeHeaders = within(tables[0])
        .getAllByRole('columnheader')
        .map((th) => th.textContent);
      expect(activeHeaders).toEqual(['#', 'Name', 'Games', 'Net', 'Rate']);
    });

    it('numbers ranks 1..n independently for each list', async () => {
      vi.mocked(apiFetch).mockResolvedValue({
        active: [entry('Alice'), entry('Bob')],
        all: [entry('Carol'), entry('Dave'), entry('Erin')],
      });
      renderLeaderboard('en');

      await screen.findByText('Active Players');

      const tables = screen.getAllByRole('table');

      // Active list: two rows ranked 1, 2.
      const activeRanks = within(tables[0])
        .getAllByRole('row')
        .slice(1) // drop the header row
        .map((row) => within(row).getAllByRole('cell')[0].textContent);
      expect(activeRanks).toEqual(['1', '2']);

      // All list: three rows ranked 1, 2, 3 — ranks restart at 1.
      const allRanks = within(tables[1])
        .getAllByRole('row')
        .slice(1)
        .map((row) => within(row).getAllByRole('cell')[0].textContent);
      expect(allRanks).toEqual(['1', '2', '3']);
    });
  });

  // 13.2 Localized headings (Req 6.2, 6.3)
  describe('localized headings', () => {
    it('shows English headings when lang is "en"', async () => {
      vi.mocked(apiFetch).mockResolvedValue({ active: [entry('Alice')], all: [entry('Carol')] });
      renderLeaderboard('en');

      expect(await screen.findByRole('heading', { level: 2, name: 'Active Players' })).toBeInTheDocument();
      expect(screen.getByRole('heading', { level: 2, name: 'All Players' })).toBeInTheDocument();
    });

    it('shows Russian headings when lang is "ru"', async () => {
      vi.mocked(apiFetch).mockResolvedValue({ active: [entry('Alice')], all: [entry('Carol')] });
      renderLeaderboard('ru');

      expect(await screen.findByRole('heading', { level: 2, name: 'Активные игроки' })).toBeInTheDocument();
      expect(screen.getByRole('heading', { level: 2, name: 'Все игроки' })).toBeInTheDocument();
    });
  });

  // 13.3 Shared sort behavior (Req 6.5, 6.6)
  describe('shared sort behavior', () => {
    it('defaults to rate on load', async () => {
      vi.mocked(apiFetch).mockResolvedValue({ active: [entry('Alice')], all: [entry('Carol')] });
      renderLeaderboard('en');

      await waitFor(() =>
        expect(vi.mocked(apiFetch)).toHaveBeenCalledWith('/leaderboard?sortBy=rate&minGames=5'),
      );

      // The Rate control carries the active styling.
      const rateButton = screen.getByRole('button', { name: 'Rate' });
      expect(rateButton.className).toContain('bg-blue-600');
      expect(rateButton.className).toContain('text-white');
    });

    it('re-fetches with sortBy=net when the Net control is clicked', async () => {
      vi.mocked(apiFetch).mockResolvedValue({ active: [entry('Alice')], all: [entry('Carol')] });
      renderLeaderboard('en');

      await screen.findByText('Active Players');

      fireEvent.click(screen.getByRole('button', { name: 'Net' }));

      await waitFor(() =>
        expect(vi.mocked(apiFetch)).toHaveBeenCalledWith('/leaderboard?sortBy=net&minGames=5'),
      );

      // The Net control becomes the active one.
      expect(screen.getByRole('button', { name: 'Net' }).className).toContain('bg-blue-600');
    });
  });

  // 13.4 Independent empty states (Req 7.1, 7.2, 7.3, 7.4, 7.5)
  describe('independent empty states', () => {
    it('shows the Active empty state while the All list renders its rows', async () => {
      vi.mocked(apiFetch).mockResolvedValue({ active: [], all: [entry('Carol')] });
      renderLeaderboard('en');

      // Active empty-state message is shown.
      expect(await screen.findByText('No active players yet')).toBeInTheDocument();

      // All list still renders its row.
      expect(screen.getByText('Carol')).toBeInTheDocument();
      expect(screen.getByRole('table')).toBeInTheDocument(); // exactly one table (the All list)

      // Active heading and the shared controls remain visible.
      expect(screen.getByRole('heading', { level: 2, name: 'Active Players' })).toBeInTheDocument();
      expect(screen.getByRole('button', { name: 'Net' })).toBeInTheDocument();
      expect(screen.getByRole('button', { name: 'Rate' })).toBeInTheDocument();
    });

    it('shows the All empty state while the Active list renders its rows', async () => {
      vi.mocked(apiFetch).mockResolvedValue({ active: [entry('Alice')], all: [] });
      renderLeaderboard('en');

      // All empty-state message is shown.
      expect(await screen.findByText('No players yet')).toBeInTheDocument();

      // Active list still renders its row.
      expect(screen.getByText('Alice')).toBeInTheDocument();
      expect(screen.getByRole('table')).toBeInTheDocument(); // exactly one table (the Active list)

      // All heading and the shared controls remain visible.
      expect(screen.getByRole('heading', { level: 2, name: 'All Players' })).toBeInTheDocument();
      expect(screen.getByRole('button', { name: 'Net' })).toBeInTheDocument();
      expect(screen.getByRole('button', { name: 'Rate' })).toBeInTheDocument();
    });
  });
});
