import { useEffect, useState } from 'react';
import { Link, useNavigate } from 'react-router-dom';

interface Game {
  id: number;
  date: string;
  gameType: string;
  playerCount: number;
  checksum: number;
}

export default function GamesList() {
  const [games, setGames] = useState<Game[]>([]);
  const navigate = useNavigate();

  useEffect(() => {
    fetch('/api/games')
      .then(res => res.json())
      .then(setGames);
  }, []);

  const [showCreateForm, setShowCreateForm] = useState(false);
  const [newGameType, setNewGameType] = useState('texas-holdem-no-limit');
  const [newBuyIn, setNewBuyIn] = useState('50');

  async function createGame() {
    if (!newBuyIn) return;

    const res = await fetch('/api/admin/games', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ gameType: newGameType, buyInAmount: Number(newBuyIn) }),
    });
    if (res.ok) {
      const game = await res.json();
      navigate(`/games/${game.id}`);
    }
  }

  return (
    <div>
      <div className="flex justify-between items-center mb-4">
        <h1 className="text-xl font-bold">Games</h1>
        <button
          className="px-4 py-2 bg-green-600 text-white rounded-lg font-medium active:bg-green-700"
          onClick={() => setShowCreateForm(true)}
        >
          + New Game
        </button>
      </div>

      {showCreateForm && (
        <div className="border rounded-lg p-4 mb-4 space-y-3">
          <div>
            <label className="text-sm text-gray-500">Game type:</label>
            <div className="flex gap-2 mt-1">
              <button
                className={`flex-1 py-2 rounded font-medium ${newGameType === 'texas-holdem-no-limit' ? 'bg-blue-600 text-white' : 'bg-gray-100'}`}
                onClick={() => setNewGameType('texas-holdem-no-limit')}
              >
                Texas Hold'em
              </button>
              <button
                className={`flex-1 py-2 rounded font-medium ${newGameType === 'tournament' ? 'bg-blue-600 text-white' : 'bg-gray-100'}`}
                onClick={() => setNewGameType('tournament')}
              >
                Tournament
              </button>
            </div>
          </div>
          <div>
            <label className="text-sm text-gray-500">Default buy-in:</label>
            <input
              type="number"
              value={newBuyIn}
              onChange={e => setNewBuyIn(e.target.value)}
              className="w-full mt-1 px-3 py-2 border rounded"
            />
          </div>
          <div className="flex gap-2">
            <button
              className="flex-1 py-2 bg-green-600 text-white rounded font-medium active:bg-green-700"
              onClick={createGame}
            >
              Create
            </button>
            <button
              className="flex-1 py-2 bg-gray-200 rounded font-medium"
              onClick={() => setShowCreateForm(false)}
            >
              Cancel
            </button>
          </div>
        </div>
      )}
      <div className="space-y-2">
        {games.map(game => (
          <Link
            key={game.id}
            to={`/games/${game.id}`}
            className="block border rounded-lg p-3 hover:bg-gray-50 active:bg-gray-100"
          >
            <div className="flex justify-between items-center">
              <span className="font-medium">{game.date}</span>
              <div className="flex gap-3 text-sm">
                <span className="text-gray-500">{game.playerCount} players</span>
                <span className={game.checksum === 0 ? 'text-green-600' : 'text-red-600'}>
                  Σ {game.checksum.toFixed(2)}
                </span>
              </div>
            </div>
            <span className="text-sm text-gray-400">{game.gameType}</span>
          </Link>
        ))}
      </div>
    </div>
  );
}
