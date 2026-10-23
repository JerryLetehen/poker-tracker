import { useEffect, useRef, useState } from 'react';
import { Link } from 'react-router-dom';
import { CrownName } from '../hooks/useTopPlayer';
import { apiFetch } from '../api/client';

interface PlayerSnapshot {
  name: string;
  cumulativeNet: number;
  rate: number;
  games: number;
  rateRank: number;
  netRank: number;
}

interface Snapshot {
  date: string;
  players: PlayerSnapshot[];
}

interface LeaderboardRow {
  name: string;
  games: number;
  result: number;
  rate: number;
  positionDelta: number;
}

export default function TimelineLeaderboard() {
  const [snapshots, setSnapshots] = useState<Snapshot[]>([]);
  const [currentRound, setCurrentRound] = useState(0);
  const [rows, setRows] = useState<LeaderboardRow[]>([]);
  const [sortBy, setSortBy] = useState<'rate' | 'net'>('rate');
  const prevRanksRef = useRef<Record<string, number>>({});
  const sortByRef = useRef<'rate' | 'net'>('rate');

  useEffect(() => {
    apiFetch<Snapshot[]>('/leaderboard/history')
      .then((data) => {
        setSnapshots(data);
        if (data.length > 0) {
          goToRound(data, 0);
        }
      })
      .catch(() => {});
  }, []);

  useEffect(() => {
    sortByRef.current = sortBy;
    if (snapshots.length > 0) {
      goToRound(snapshots, currentRound);
    }
  }, [sortBy]);

  function goToRound(allSnapshots: Snapshot[], round: number) {
    const sortFn = sortByRef.current === 'rate'
      ? (a: PlayerSnapshot, b: PlayerSnapshot) => b.rate - a.rate
      : (a: PlayerSnapshot, b: PlayerSnapshot) => Number(b.cumulativeNet) - Number(a.cumulativeNet);

    let prevRanks: Record<string, number> = {};
    if (round > 0) {
      const prevSnapshot = allSnapshots[round - 1];
      const prevSorted = [...prevSnapshot.players].sort(sortFn);
      prevSorted.forEach((p, i) => {
        prevRanks[p.name] = i + 1;
      });
    }
    prevRanksRef.current = prevRanks;

    const snapshot = allSnapshots[round];
    const sorted = [...snapshot.players]
      .filter(p => round < 4 || (p.games || 0) >= 5)
      .sort(sortFn);

    const newRows: LeaderboardRow[] = sorted.map((player, i) => {
      const prevRank = prevRanks[player.name];
      const currentRank = i + 1;
      const positionDelta = prevRank !== undefined ? prevRank - currentRank : 0;

      return {
        name: player.name,
        games: player.games,
        result: Number(player.cumulativeNet),
        rate: player.rate,
        positionDelta,
      };
    });

    setRows(newRows);
    setCurrentRound(round);
  }

  function handleSlider(e: React.ChangeEvent<HTMLInputElement>) {
    const round = parseInt(e.target.value, 10);
    goToRound(snapshots, round);
  }

  function stepLeft() {
    if (currentRound > 0) goToRound(snapshots, currentRound - 1);
  }

  function stepRight() {
    if (currentRound < snapshots.length - 1) goToRound(snapshots, currentRound + 1);
  }

  const totalRounds = snapshots.length > 0 ? snapshots.length - 1 : 0;
  const currentDate = snapshots[currentRound]?.date || '';

  return (
    <div>
      <div className="flex justify-between items-center mb-2">
        <h1 className="text-lg font-bold">Timeline</h1>
        <div className="flex gap-1">
          <button
            className={`px-2 py-1 text-xs rounded ${sortBy === 'rate' ? 'bg-blue-600 text-white' : 'bg-gray-200'}`}
            onClick={() => setSortBy('rate')}
          >
            Rate
          </button>
          <button
            className={`px-2 py-1 text-xs rounded ${sortBy === 'net' ? 'bg-blue-600 text-white' : 'bg-gray-200'}`}
            onClick={() => setSortBy('net')}
          >
            Net
          </button>
        </div>
      </div>

      <p className="text-sm text-gray-500 mb-2">{currentDate}</p>

      <div className="flex gap-2 items-center mb-2">
        <button className="px-3 py-1 bg-gray-200 rounded active:bg-gray-300" onClick={stepLeft}>←</button>
        <input
          type="range"
          min={0}
          max={totalRounds}
          value={currentRound}
          onChange={handleSlider}
          className="flex-1"
        />
        <button className="px-3 py-1 bg-gray-200 rounded active:bg-gray-300" onClick={stepRight}>→</button>
        <span className="text-xs text-gray-400 w-12 text-right">{currentRound + 1}/{totalRounds + 1}</span>
      </div>

      <div className="space-y-2">
        {rows.map((row, i) => (
          <Link key={row.name} to={`/players/${row.name}`} className="block">
            <div className="border rounded-lg p-3 active:bg-gray-50">
              <div className="flex justify-between items-center">
                <div className="flex items-center gap-2">
                  <span className="text-gray-400 text-sm w-5">{i + 1}</span>
                  <span className="font-medium"><CrownName name={row.name} /></span>
                  <span className={`text-xs ${row.positionDelta > 0 ? 'text-green-600' : row.positionDelta < 0 ? 'text-red-600' : 'text-gray-400'}`}>
                    {row.positionDelta > 0 ? `↑${row.positionDelta}` : row.positionDelta < 0 ? `↓${Math.abs(row.positionDelta)}` : ''}
                  </span>
                </div>
                <span className="text-xs text-gray-400">{row.games} games</span>
              </div>
              <div className="flex gap-4 mt-1 text-sm">
                <span className={row.result > 0 ? 'text-green-600' : row.result < 0 ? 'text-red-600' : ''}>
                  Net: {row.result >= 0 ? '+' : ''}{row.result.toFixed(2)}
                </span>
                <span className={row.rate > 0 ? 'text-green-600' : row.rate < 0 ? 'text-red-600' : ''}>
                  Rate: {row.rate >= 0 ? '+' : ''}{row.rate.toFixed(2)}
                </span>
              </div>
            </div>
          </Link>
        ))}
      </div>
    </div>
  );
}
