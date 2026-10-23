import { describe, it, expect } from 'vitest';
import { formatValue, valueColor, findBest, findWorst, calculatePositionDelta } from './utils';

describe('formatValue', () => {
  it('formats positive numbers with + prefix', () => {
    expect(formatValue(5.5)).toBe('+5.50');
  });

  it('formats negative numbers with - prefix', () => {
    expect(formatValue(-3.2)).toBe('-3.20');
  });

  it('formats zero with + prefix', () => {
    expect(formatValue(0)).toBe('+0.00');
  });

  it('formats -0 with + prefix', () => {
    expect(formatValue(-0)).toBe('+0.00');
  });

  it('formats large numbers', () => {
    expect(formatValue(12345.678)).toBe('+12345.68');
  });

  it('formats small decimals', () => {
    expect(formatValue(0.001)).toBe('+0.00');
    expect(formatValue(0.005)).toBe('+0.01');
    expect(formatValue(-0.009)).toBe('-0.01');
  });
});

describe('valueColor', () => {
  it('returns green for positive values', () => {
    expect(valueColor(5)).toBe('text-green-600');
  });

  it('returns red for negative values', () => {
    expect(valueColor(-3)).toBe('text-red-600');
  });

  it('returns empty string for zero', () => {
    expect(valueColor(0)).toBe('');
  });

  it('returns green for small positive boundary (0.001)', () => {
    expect(valueColor(0.001)).toBe('text-green-600');
  });

  it('returns red for small negative boundary (-0.001)', () => {
    expect(valueColor(-0.001)).toBe('text-red-600');
  });
});

describe('findBest', () => {
  it('finds the item with the highest selected value', () => {
    const data = [{ name: 'A', score: 5 }, { name: 'B', score: 10 }, { name: 'C', score: 3 }];
    expect(findBest(data, d => d.score)).toEqual({ name: 'B', score: 10 });
  });

  it('returns null for empty array', () => {
    expect(findBest([], (d: number) => d)).toBeNull();
  });

  it('returns the single element for single-element array', () => {
    const data = [{ val: 42 }];
    expect(findBest(data, d => d.val)).toEqual({ val: 42 });
  });

  it('returns the first element when all values are the same', () => {
    const data = [{ id: 1, val: 5 }, { id: 2, val: 5 }, { id: 3, val: 5 }];
    expect(findBest(data, d => d.val)).toEqual({ id: 1, val: 5 });
  });

  it('works with all negative values', () => {
    const data = [{ val: -10 }, { val: -3 }, { val: -7 }];
    expect(findBest(data, d => d.val)).toEqual({ val: -3 });
  });
});

describe('findWorst', () => {
  it('finds the item with the lowest selected value', () => {
    const data = [{ name: 'A', score: 5 }, { name: 'B', score: 10 }, { name: 'C', score: 3 }];
    expect(findWorst(data, d => d.score)).toEqual({ name: 'C', score: 3 });
  });

  it('returns null for empty array', () => {
    expect(findWorst([], (d: number) => d)).toBeNull();
  });

  it('returns the single element for single-element array', () => {
    const data = [{ val: 42 }];
    expect(findWorst(data, d => d.val)).toEqual({ val: 42 });
  });

  it('returns the first element when all values are the same', () => {
    const data = [{ id: 1, val: 5 }, { id: 2, val: 5 }, { id: 3, val: 5 }];
    expect(findWorst(data, d => d.val)).toEqual({ id: 1, val: 5 });
  });

  it('works with all negative values', () => {
    const data = [{ val: -10 }, { val: -3 }, { val: -7 }];
    expect(findWorst(data, d => d.val)).toEqual({ val: -10 });
  });
});

describe('calculatePositionDelta', () => {
  it('returns positive delta when player moved up', () => {
    const current = ['Alice', 'Bob', 'Charlie'];
    const previous = ['Bob', 'Charlie', 'Alice'];
    // Alice: prev index 2, current index 0, delta = 2 - 0 = 2
    expect(calculatePositionDelta('Alice', current, previous)).toBe(2);
  });

  it('returns negative delta when player moved down', () => {
    const current = ['Alice', 'Bob', 'Charlie'];
    const previous = ['Bob', 'Alice', 'Charlie'];
    // Bob: prev index 0, current index 1, delta = 0 - 1 = -1
    expect(calculatePositionDelta('Bob', current, previous)).toBe(-1);
  });

  it('returns 0 when player stayed in same position', () => {
    const current = ['Alice', 'Bob', 'Charlie'];
    const previous = ['Alice', 'Bob', 'Charlie'];
    expect(calculatePositionDelta('Bob', current, previous)).toBe(0);
  });

  it('returns 0 when player is not in previous ranks', () => {
    const current = ['Alice', 'Bob', 'Charlie'];
    const previous = ['Alice', 'Charlie'];
    expect(calculatePositionDelta('Bob', current, previous)).toBe(0);
  });

  it('returns 0 when player is not in current ranks', () => {
    const current = ['Alice', 'Charlie'];
    const previous = ['Alice', 'Bob', 'Charlie'];
    expect(calculatePositionDelta('Bob', current, previous)).toBe(0);
  });
});
