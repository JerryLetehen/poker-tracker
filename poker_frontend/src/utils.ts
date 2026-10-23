export function formatValue(val: number): string {
  return `${val >= 0 ? '+' : ''}${val.toFixed(2)}`;
}

export function valueColor(val: number): string {
  if (val > 0) return 'text-green-600';
  if (val < 0) return 'text-red-600';
  return '';
}

export function findBest<T>(data: T[], selector: (item: T) => number): T | null {
  if (data.length === 0) return null;
  return data.reduce((max, item) => selector(item) > selector(max) ? item : max, data[0]);
}

export function findWorst<T>(data: T[], selector: (item: T) => number): T | null {
  if (data.length === 0) return null;
  return data.reduce((min, item) => selector(item) < selector(min) ? item : min, data[0]);
}

export function calculatePositionDelta(
  playerName: string,
  currentRanks: string[],
  previousRanks: string[]
): number {
  const currentIdx = currentRanks.indexOf(playerName);
  const prevIdx = previousRanks.indexOf(playerName);
  if (currentIdx === -1 || prevIdx === -1) return 0;
  return prevIdx - currentIdx;
}
