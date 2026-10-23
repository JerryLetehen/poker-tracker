import { useEffect, useState } from 'react';
import { useParams, useNavigate } from 'react-router-dom';
import { CHIPS, NOMINAL_TO_VALUE, calculateChipValue, getPlayersForCashOut, getPlayersForBuyIn, updateChipCount } from '../utils';
import { apiFetch, apiJson } from '../api/client';

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

const CHIP_COLORS: Record<number, string> = {
  5000: 'bg-red-600 text-white',
  1000: 'bg-yellow-400 text-black',
  500: 'bg-purple-600 text-white',
  100: 'bg-black text-white',
  50: 'bg-blue-500 text-white',
  25: 'bg-green-600 text-white',
};

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

  // Chip counter state
  const [chipCounts, setChipCounts] = useState<Record<number, number>>({});

  const chipValue = calculateChipValue(chipCounts);

  useEffect(() => {
    loadGame();
    loadPlayers();
  }, [id]);

  async function loadGame() {
    const data = await apiFetch<GameData & { buyInAmount: number }>(`/admin/games/${id}`);
    setGame(data);
    if (data.buyInAmount) setDefaultBuyIn(data.buyInAmount);
  }

  async function loadPlayers() {
    setPlayers(await apiFetch<Player[]>('/admin/players'));
  }

  async function addEntry(playerId: number, type: 'buy-in' | 'cash-out', amount: number) {
    const endpoint = type === 'buy-in'
      ? `/admin/games/${id}/buy-in`
      : `/admin/games/${id}/cash-out`;

    await apiJson(endpoint, 'POST', { playerId, amount });
    resetForm();
    loadGame();
  }

  async function submitCashOut() {
    if (!selectedPlayer) return;
    await addEntry(selectedPlayer, 'cash-out', chipValue);
  }

  function resetForm() {
    setShowAddEntry(false);
    setSelectedPlayer(null);
    setCustomAmount('');
    setChipCounts({});
    setEntryType('buy-in');
  }

  function updateChip(nominal: number, delta: number) {
    setChipCounts(prev => updateChipCount(prev, nominal, delta));
  }

  async function deleteEntry(entryId: number) {
    if (!confirm('Delete this entry?')) return;
    await apiFetch(`/admin/game-entries/${entryId}`, { method: 'DELETE' });
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
            <div className="flex items-center gap-2">
              <div>
                <span className="font-medium">{entry.playerName}</span>
                <span className={`ml-2 text-sm ${entry.type === 'buy-in' ? 'text-red-600' : 'text-green-600'}`}>
                  {entry.type === 'buy-in' ? '← buy-in' : '→ cash-out'} {entry.amount.toFixed(2)}
                </span>
              </div>
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
            <div className="grid grid-cols-1 gap-2">
              {(entryType === 'cash-out'
                ? getPlayersForCashOut(players, game.entries)
                : getPlayersForBuyIn(players, game.entries)
              ).map(p => (
                <button
                  key={p.id}
                  className={`py-2 px-2 rounded text-sm font-medium ${selectedPlayer === p.id ? 'bg-blue-600 text-white' : game.entries.some(e => e.type === 'buy-in' && e.playerId === p.id) ? 'bg-gray-200 active:bg-gray-300' : 'bg-gray-100 active:bg-gray-200'}`}
                  onClick={() => setSelectedPlayer(p.id)}
                >
                  {p.name}
                </button>
              ))}
            </div>
          </div>

          {/* Amount - depends on type */}
          {selectedPlayer && entryType === 'buy-in' && (
            <div className="space-y-2">
              <p className="text-sm text-gray-500">Amount:</p>
              <button
                className="w-full py-3 bg-green-600 text-white rounded font-medium active:bg-green-700"
                onClick={() => addEntry(selectedPlayer, 'buy-in', defaultBuyIn)}
              >
                Default: {defaultBuyIn}
              </button>
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
                    if (customAmount) addEntry(selectedPlayer, 'buy-in', Number(customAmount));
                  }}
                >
                  Add
                </button>
              </div>
            </div>
          )}

          {/* Chip counter for cash-out */}
          {selectedPlayer && entryType === 'cash-out' && (
            <div className="space-y-3">
              <p className="text-sm text-gray-500">Count chips:</p>
              <div className="space-y-2">
                {CHIPS.map(chip => (
                  <div key={chip.nominal} className="flex items-center gap-2">
                    <span className={`w-12 text-center py-1 rounded text-xs font-bold ${CHIP_COLORS[chip.nominal]}`}>
                      {chip.label}
                    </span>
                    <button
                      className="w-8 h-8 bg-gray-200 rounded font-bold active:bg-gray-300"
                      onClick={() => updateChip(chip.nominal, -1)}
                    >
                      −
                    </button>
                    <span className="w-8 text-center font-medium">
                      {chipCounts[chip.nominal] || 0}
                    </span>
                    <button
                      className="w-8 h-8 bg-gray-200 rounded font-bold active:bg-gray-300"
                      onClick={() => updateChip(chip.nominal, 1)}
                    >
                      +
                    </button>
                    <button
                      className="w-8 h-8 bg-gray-200 rounded text-xs font-bold active:bg-gray-300"
                      onClick={() => updateChip(chip.nominal, 5)}
                    >
                      +5
                    </button>
                    <button
                      className="w-8 h-8 bg-gray-200 rounded text-xs font-bold active:bg-gray-300"
                      onClick={() => updateChip(chip.nominal, 10)}
                    >
                      +10
                    </button>
                    <span className="text-xs text-gray-400 ml-auto">
                      = {((chipCounts[chip.nominal] || 0) * chip.nominal * NOMINAL_TO_VALUE).toFixed(2)}
                    </span>
                  </div>
                ))}
              </div>

              {/* Total */}
              <div className="flex justify-between items-center border-t pt-2">
                <span className="font-medium">Total:</span>
                <span className="text-lg font-bold">{chipValue.toFixed(2)}</span>
              </div>

              {/* Submit */}
              <button
                className="w-full py-3 bg-orange-600 text-white rounded font-medium active:bg-orange-700 disabled:opacity-50"
                disabled={chipValue < 0}
                onClick={submitCashOut}
              >
                Cash-out: {chipValue.toFixed(2)}
              </button>
            </div>
          )}

          <button
            className="w-full py-2 text-sm text-gray-500"
            onClick={resetForm}
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
          await apiFetch(`/admin/games/${id}`, { method: 'DELETE' });
          navigate('/');
        }}
      >
        🗑 Delete Game
      </button>
    </div>
  );
}
