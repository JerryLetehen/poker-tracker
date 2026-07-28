import { useEffect, useState } from 'react';
import { useParams, Link } from 'react-router-dom';

interface PlayerEntry {
  playerName: string;
  buyIn: number;
  cashOut: number;
  net: number;
}

interface GameData {
  date: string;
  gameType: string;
  players: PlayerEntry[];
}

export default function GameDetail() {
  const { id } = useParams();
  const [game, setGame] = useState<GameData | null>(null);

  useEffect(() => {
    fetch(`/api/games/${id}`)
      .then(res => res.json())
      .then(setGame);
  }, [id]);

  if (!game) return <div className="p-4 text-center">Loading...</div>;

  const totalPot = game.players.reduce((sum, p) => sum + p.buyIn, 0);

  return (
    <div>
      <div className="mb-3">
        <h1 className="text-lg font-bold">{game.date}</h1>
        <div className="flex gap-3 text-sm text-gray-500">
          <span>{game.gameType}</span>
          <span>Pot: {totalPot.toFixed(0)}</span>
          <span>{game.players.length} players</span>
        </div>
      </div>
      <div className="space-y-2">
        {game.players.map((entry, i) => (
          <Link key={entry.playerName} to={`/players/${entry.playerName}`} className="block">
            <div className="border rounded-lg p-3 active:bg-gray-50">
              <div className="flex justify-between items-center">
                <div className="flex items-center gap-2">
                  <span className="text-gray-400 text-sm w-5">{i + 1}</span>
                  <span className="font-medium">{entry.playerName}</span>
                </div>
                <span className={`font-medium ${entry.net > 0 ? 'text-green-600' : entry.net < 0 ? 'text-red-600' : ''}`}>
                  {entry.net >= 0 ? '+' : ''}{entry.net.toFixed(2)}
                </span>
              </div>
              <div className="flex gap-4 mt-1 text-xs text-gray-500 ml-7">
                <span>In: {entry.buyIn.toFixed(2)}</span>
                <span>Out: {entry.cashOut.toFixed(2)}</span>
              </div>
            </div>
          </Link>
        ))}
      </div>
    </div>
  );
}
