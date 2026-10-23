export const CHIPS = [
  { label: '5K', nominal: 5000 },
  { label: '1K', nominal: 1000 },
  { label: '500', nominal: 500 },
  { label: '100', nominal: 100 },
  { label: '50', nominal: 50 },
  { label: '25', nominal: 25 },
];

export const NOMINAL_TO_VALUE = 0.01;

export function calculateChipTotal(chipCounts: Record<number, number>): number {
  return Object.entries(chipCounts).reduce(
    (sum, [nominal, count]) => sum + Number(nominal) * count, 0
  );
}

export function calculateChipValue(chipCounts: Record<number, number>): number {
  return calculateChipTotal(chipCounts) * NOMINAL_TO_VALUE;
}

export interface GameEntry {
  type: string;
  playerId: number;
}

export function getPlayersForCashOut(
  players: { id: number; name: string }[],
  entries: GameEntry[]
): { id: number; name: string }[] {
  return players.filter(p =>
    entries.some(e => e.type === 'buy-in' && e.playerId === p.id) &&
    !entries.some(e => e.type === 'cash-out' && e.playerId === p.id)
  );
}

export function getPlayersForBuyIn(
  players: { id: number; name: string }[],
  entries: GameEntry[]
): { id: number; name: string }[] {
  return [...players].sort((a, b) => {
    const aIn = entries.some(e => e.type === 'buy-in' && e.playerId === a.id);
    const bIn = entries.some(e => e.type === 'buy-in' && e.playerId === b.id);
    if (aIn && !bIn) return -1;
    if (!aIn && bIn) return 1;
    return a.name.localeCompare(b.name);
  });
}

export function updateChipCount(current: Record<number, number>, nominal: number, delta: number): Record<number, number> {
  const count = current[nominal] || 0;
  const next = Math.max(0, count + delta);
  return { ...current, [nominal]: next };
}
