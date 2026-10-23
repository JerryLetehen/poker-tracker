import { useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { CrownName } from '../hooks/useTopPlayer';
import { apiFetch } from '../api/client';

interface LeaderboardEntry {
  name: string;
  games: number;
  result: number;
  rate: number;
  positionDelta: number;
}

export default function Leaderboard() {
  const [data, setData] = useState<LeaderboardEntry[]>([]);
  const [sortBy, setSortBy] = useState<'net' | 'rate'>('rate');

  useEffect(() => {
    apiFetch<LeaderboardEntry[]>(`/leaderboard?sortBy=${sortBy}&minGames=5`)
      .then(setData)
      .catch(() => {});
  }, [sortBy]);

  return (
    <div>
      <div className="flex justify-between items-center mb-3">
        <h1 className="text-lg font-bold">Leaderboard</h1>
        <div className="flex gap-1">
          <button
            className={`px-2 py-1 text-xs rounded ${sortBy === 'net' ? 'bg-blue-600 text-white' : 'bg-gray-200'}`}
            onClick={() => setSortBy('net')}
          >
            Net
          </button>
          <button
              className={`px-2 py-1 text-xs rounded ${sortBy === 'rate' ? 'bg-blue-600 text-white' : 'bg-gray-200'}`}
              onClick={() => setSortBy('rate')}
          >
            Rate
          </button>
        </div>
      </div>
      <div className="overflow-x-auto">
        <table className="w-full text-sm border-collapse">
          <thead>
            <tr className="border-b text-gray-500">
              <th className="py-2 pr-1 text-left">#</th>
              <th className="py-2 pr-2 text-left">Name</th>
              <th className="py-2 pr-2 text-right">Games</th>
              <th className="py-2 pr-2 text-right">Net</th>
              <th className="py-2 text-right">Rate</th>
            </tr>
          </thead>
          <tbody>
            {data.map((entry, i) => (
              <tr key={entry.name} className="border-b">
                <td className="py-2 pr-1 text-gray-400">{i + 1}</td>
                <td className="py-2 pr-2">
                  <Link to={`/players/${entry.name}`} className="font-medium text-blue-600">
                    <CrownName name={entry.name} />
                  </Link>
                </td>
                <td className="py-2 pr-2 text-right text-gray-500">{entry.games}</td>
                <td className={`py-2 pr-2 text-right ${entry.result > 0 ? 'text-green-600' : entry.result < 0 ? 'text-red-600' : ''}`}>
                  {entry.result >= 0 ? '+' : ''}{entry.result.toFixed(0)}
                </td>
                <td className={`py-2 text-right ${entry.rate > 0 ? 'text-green-600' : entry.rate < 0 ? 'text-red-600' : ''}`}>
                  {entry.rate >= 0 ? '+' : ''}{entry.rate.toFixed(2)}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  );
}
