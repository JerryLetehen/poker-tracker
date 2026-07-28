import { useEffect, useState } from 'react';
import { useParams, useNavigate } from 'react-router-dom';

interface Entry {
  id: number;
  type: string;
  amount: number;
  playerName: string;
  playerId: number;
}

interface GameData {
  date: string;
  gameType: string;
  buyInAmount: number;
  checksum: number;
  entries: Entry[];
}

interface Player {
  id: number;
  name: string;
}

export default function GamePage() {
  const { id } = useParams();
  const navigate = useNavigate();
  const [game, setGame] = useState<GameData | null>(null);
  const [players, setPlayers] = useState<Player[]>([]);
  const [defaultBuyIn, setDefaultBuyIn] = useState(10);
  const [showAddEntry, setShowAddEntry] = useState(false);
  const [selectedPlayer, setSelectedPlayer] = useState<number | null>(null);
  const [customAmount, setCustomAmount] = useState('');
  const [entryType, setEntryType] = useState<'buy-in' | 'cash-out'>('buy-in');

  useEffect(() => {
    loadGame();
    loadPlayers();
  }, [id]);

  async function loadGame() {
    const res = await fetch(`/api/admin/games/${id}`);
    const data = await res.json();
    setGame(data);
    if (data.buyInAmount) setDefaultBuyIn(data.buyInAmount);
  }

  async function loadPlayers() {
    const res = await fetch('/api/admin/players');
    const data = await res.json();
    setPlayers(data);
  }

  async function addEntry(playerId: number, type: 'buy-in' | 'cash-out', amount: number) {
    const endpoint = type === 'buy-in'
      ? `/api/admin/games/${id}/buy-in`
      : `/api/admin/games/${id}/cash-out`;

    await fetch(endpoint, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ playerId, amount }),
    });
    setShowAddEntry(false);
    setSelectedPlayer(null);
    setCustomAmount('');
    loadGame();
  }

  async function deleteEntry(entryId: number) {
    if (!confirm('Delete this entry?')) return;
    await fetch(`/api/admin/game-entries/${entryId}`, { method: 'DELETE' });
    loadGame();
  }

  if (!game) return <div className="p-4 text-center">Loading...</div>;

  return (
    <div>
      <div className="flex justify-between items-center mb-4">
        <div>
          <h1 className="text-xl font-bold">{game.date}</h1>
          <span className="text-sm text-gray-500">{game.gameType}</span>
        </div>
        <span className={`text-lg font-medium ${game.checksum === 0 ? 'text-green-600' : 'text-red-600'}`}>
          Σ {game.checksum.toFixed(2)}
        </span>
      </div>

      {/* Entries list */}
      <div className="space-y-2 mb-4">
        {game.entries.length === 0 && (
          <p className="text-gray-400 text-center py-4">No entries yet</p>
        )}
        {game.entries.map(entry => (
          <div key={entry.id} className="flex justify-between items-center border rounded-lg p-3">
            <div>
              <span className="font-medium">{entry.playerName}</span>
              <span className={`ml-2 text-sm ${entry.type === 'buy-in' ? 'text-red-600' : 'text-green-600'}`}>
                {entry.type === 'buy-in' ? '← buy-in' : '→ cash-out'} {entry.amount.toFixed(2)}
              </span>
            </div>
            <button
              className="px-2 py-1 text-red-500 hover:bg-red-50 rounded text-sm"
              onClick={() => deleteEntry(entry.id)}
            >
              ✕
            </button>
          </div>
        ))}
      </div>

      {/* Add entry */}
      {!showAddEntry ? (
        <button
          className="w-full py-3 bg-blue-600 text-white rounded-lg font-medium active:bg-blue-700"
          onClick={() => setShowAddEntry(true)}
        >
          + Add Entry
        </button>
      ) : (
        <div className="border rounded-lg p-4 space-y-3">
          {/* Entry type toggle */}
          <div className="flex gap-2">
            <button
              className={`flex-1 py-2 rounded font-medium ${entryType === 'buy-in' ? 'bg-blue-600 text-white' : 'bg-gray-100'}`}
              onClick={() => setEntryType('buy-in')}
            >
              Buy-in
            </button>
            <button
              className={`flex-1 py-2 rounded font-medium ${entryType === 'cash-out' ? 'bg-orange-600 text-white' : 'bg-gray-100'}`}
              onClick={() => setEntryType('cash-out')}
            >
              Cash-out
            </button>
          </div>

          {/* Player selection */}
          <div>
            <p className="text-sm text-gray-500 mb-2">Player:</p>
            <div className="grid grid-cols-3 gap-2">
              {players.map(p => (
                <button
                  key={p.id}
                  className={`py-2 px-2 rounded text-sm font-medium ${selectedPlayer === p.id ? 'bg-blue-600 text-white' : 'bg-gray-100 active:bg-gray-200'}`}
                  onClick={() => setSelectedPlayer(p.id)}
                >
                  {p.name}
                </button>
              ))}
            </div>
          </div>

          {/* Amount */}
          {selectedPlayer && (
            <div className="space-y-2">
              <p className="text-sm text-gray-500">Amount:</p>
              {entryType === 'buy-in' && (
                <button
                  className="w-full py-3 bg-green-600 text-white rounded font-medium active:bg-green-700"
                  onClick={() => addEntry(selectedPlayer, 'buy-in', defaultBuyIn)}
                >
                  Default: {defaultBuyIn}
                </button>
              )}
              <div className="flex gap-2">
                <input
                  type="number"
                  placeholder="Custom amount"
                  value={customAmount}
                  onChange={e => setCustomAmount(e.target.value)}
                  className="flex-1 px-3 py-2 border rounded"
                />
                <button
                  className="px-4 py-2 bg-blue-600 text-white rounded font-medium"
                  onClick={() => {
                    if (customAmount) addEntry(selectedPlayer, entryType, Number(customAmount));
                  }}
                >
                  Add
                </button>
              </div>
            </div>
          )}

          <button
            className="w-full py-2 text-sm text-gray-500"
            onClick={() => { setShowAddEntry(false); setSelectedPlayer(null); setCustomAmount(''); }}
          >
            Cancel
          </button>
        </div>
      )}

      {/* Delete game */}
      <button
        className="w-full mt-6 py-3 border-2 border-red-300 text-red-600 rounded-lg font-medium active:bg-red-50"
        onClick={async () => {
          const confirmed = window.confirm(
            '⚠️ Delete this game?\n\nThis will permanently delete the game and ALL its entries. This action cannot be undone.'
          );
          if (!confirmed) return;
          const doubleConfirm = window.confirm(
            'Are you absolutely sure? This cannot be undone.'
          );
          if (!doubleConfirm) return;
          await fetch(`/api/admin/games/${id}`, { method: 'DELETE' });
          navigate('/');
        }}
      >
        🗑 Delete Game
      </button>
    </div>
  );
}
