import { describe, it, expect } from 'vitest';
import {
  calculateChipTotal,
  calculateChipValue,
  getPlayersForCashOut,
  getPlayersForBuyIn,
  updateChipCount,
  NOMINAL_TO_VALUE,
} from './utils';

describe('calculateChipTotal', () => {
  it('returns 0 for empty chip counts', () => {
    expect(calculateChipTotal({})).toBe(0);
  });

  it('calculates total for a single chip type', () => {
    expect(calculateChipTotal({ 100: 5 })).toBe(500);
  });

  it('calculates total for all chip types', () => {
    const counts = { 5000: 1, 1000: 2, 500: 3, 100: 4, 50: 5, 25: 6 };
    // 5000 + 2000 + 1500 + 400 + 250 + 150 = 9300
    expect(calculateChipTotal(counts)).toBe(9300);
  });

  it('handles large counts', () => {
    expect(calculateChipTotal({ 5000: 100 })).toBe(500000);
  });
});

describe('calculateChipValue', () => {
  it('applies NOMINAL_TO_VALUE conversion factor correctly', () => {
    // 100 nominal * 5 chips = 500 nominal, * 0.01 = 5.00
    expect(calculateChipValue({ 100: 5 })).toBeCloseTo(5.0);
  });

  it('returns 0 for zero chips', () => {
    expect(calculateChipValue({})).toBe(0);
  });

  it('uses correct conversion factor (0.01)', () => {
    expect(NOMINAL_TO_VALUE).toBe(0.01);
    expect(calculateChipValue({ 1000: 1 })).toBeCloseTo(10.0);
  });

  it('handles mixed chip types', () => {
    const counts = { 5000: 1, 25: 4 };
    // (5000 + 100) * 0.01 = 51.00
    expect(calculateChipValue(counts)).toBeCloseTo(51.0);
  });
});

describe('getPlayersForCashOut', () => {
  const players = [
    { id: 1, name: 'Alice' },
    { id: 2, name: 'Bob' },
    { id: 3, name: 'Charlie' },
  ];

  it('returns players who have buy-in but no cash-out', () => {
    const entries = [
      { type: 'buy-in', playerId: 1 },
      { type: 'buy-in', playerId: 2 },
      { type: 'cash-out', playerId: 1 },
    ];
    const result = getPlayersForCashOut(players, entries);
    expect(result).toEqual([{ id: 2, name: 'Bob' }]);
  });

  it('excludes players who already cashed out', () => {
    const entries = [
      { type: 'buy-in', playerId: 1 },
      { type: 'cash-out', playerId: 1 },
    ];
    const result = getPlayersForCashOut(players, entries);
    expect(result).toEqual([]);
  });

  it('excludes players with no buy-in', () => {
    const entries = [
      { type: 'buy-in', playerId: 1 },
    ];
    const result = getPlayersForCashOut(players, entries);
    expect(result).toEqual([{ id: 1, name: 'Alice' }]);
    // Bob and Charlie have no buy-in, so they are not included
  });

  it('still shows player with multiple buy-ins and no cash-out', () => {
    const entries = [
      { type: 'buy-in', playerId: 2 },
      { type: 'buy-in', playerId: 2 },
    ];
    const result = getPlayersForCashOut(players, entries);
    expect(result).toEqual([{ id: 2, name: 'Bob' }]);
  });

  it('returns empty array for empty entries', () => {
    const result = getPlayersForCashOut(players, []);
    expect(result).toEqual([]);
  });

  it('returns empty array for empty players', () => {
    const entries = [{ type: 'buy-in', playerId: 1 }];
    const result = getPlayersForCashOut([], entries);
    expect(result).toEqual([]);
  });
});

describe('getPlayersForBuyIn', () => {
  it('sorts players already in game first, then alphabetical', () => {
    const players = [
      { id: 1, name: 'Charlie' },
      { id: 2, name: 'Alice' },
      { id: 3, name: 'Bob' },
    ];
    const entries = [
      { type: 'buy-in', playerId: 3 }, // Bob is in game
    ];
    const result = getPlayersForBuyIn(players, entries);
    // Bob first (already in game), then Alice, Charlie (alphabetical)
    expect(result).toEqual([
      { id: 3, name: 'Bob' },
      { id: 2, name: 'Alice' },
      { id: 1, name: 'Charlie' },
    ]);
  });

  it('sorts all new players alphabetically', () => {
    const players = [
      { id: 1, name: 'Charlie' },
      { id: 2, name: 'Alice' },
      { id: 3, name: 'Bob' },
    ];
    const result = getPlayersForBuyIn(players, []);
    expect(result).toEqual([
      { id: 2, name: 'Alice' },
      { id: 3, name: 'Bob' },
      { id: 1, name: 'Charlie' },
    ]);
  });

  it('sorts all already-in-game players alphabetically among themselves', () => {
    const players = [
      { id: 1, name: 'Charlie' },
      { id: 2, name: 'Alice' },
      { id: 3, name: 'Bob' },
    ];
    const entries = [
      { type: 'buy-in', playerId: 1 },
      { type: 'buy-in', playerId: 2 },
      { type: 'buy-in', playerId: 3 },
    ];
    const result = getPlayersForBuyIn(players, entries);
    expect(result).toEqual([
      { id: 2, name: 'Alice' },
      { id: 3, name: 'Bob' },
      { id: 1, name: 'Charlie' },
    ]);
  });

  it('returns empty array for empty players', () => {
    const result = getPlayersForBuyIn([], []);
    expect(result).toEqual([]);
  });
});

describe('updateChipCount', () => {
  it('increments chip count', () => {
    const result = updateChipCount({ 100: 3 }, 100, 1);
    expect(result[100]).toBe(4);
  });

  it('decrements chip count', () => {
    const result = updateChipCount({ 100: 3 }, 100, -1);
    expect(result[100]).toBe(2);
  });

  it('floors at zero (cannot go negative)', () => {
    const result = updateChipCount({ 100: 0 }, 100, -1);
    expect(result[100]).toBe(0);
  });

  it('floors at zero from positive count', () => {
    const result = updateChipCount({ 100: 2 }, 100, -5);
    expect(result[100]).toBe(0);
  });

  it('handles new nominal not in map', () => {
    const result = updateChipCount({}, 500, 1);
    expect(result[500]).toBe(1);
  });

  it('does not mutate original object', () => {
    const original = { 100: 3 };
    const result = updateChipCount(original, 100, 1);
    expect(original[100]).toBe(3);
    expect(result[100]).toBe(4);
  });
});
