import { useEffect, useState } from 'react';

interface Player {
  id: number;
  name: string;
}

export default function PlayersPage() {
  const [players, setPlayers] = useState<Player[]>([]);
  const [editingId, setEditingId] = useState<number | null>(null);
  const [editName, setEditName] = useState('');
  const [newName, setNewName] = useState('');

  useEffect(() => {
    loadPlayers();
  }, []);

  async function loadPlayers() {
    const res = await fetch('/api/admin/players');
    setPlayers(await res.json());
  }

  function startEdit(player: Player) {
    setEditingId(player.id);
    setEditName(player.name);
  }

  async function saveEdit(playerId: number) {
    if (!editName.trim()) return;
    await fetch(`/api/admin/players/${playerId}`, {
      method: 'PUT',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ name: editName.trim() }),
    });
    setEditingId(null);
    loadPlayers();
  }

  async function createPlayer() {
    if (!newName.trim()) return;
    await fetch('/api/admin/players', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ name: newName.trim() }),
    });
    setNewName('');
    loadPlayers();
  }

  return (
    <div>
      <h1 className="text-xl font-bold mb-4">Players</h1>

      <div className="space-y-2 mb-4">
        {players.map(player => (
          <div key={player.id} className="flex items-center gap-2 border rounded-lg p-3">
            {editingId === player.id ? (
              <>
                <input
                  type="text"
                  value={editName}
                  onChange={e => setEditName(e.target.value)}
                  className="flex-1 px-3 py-1 border rounded"
                  autoFocus
                  onKeyDown={e => e.key === 'Enter' && saveEdit(player.id)}
                />
                <button
                  className="px-3 py-1 bg-green-600 text-white rounded text-sm"
                  onClick={() => saveEdit(player.id)}
                >
                  Save
                </button>
                <button
                  className="px-3 py-1 bg-gray-200 rounded text-sm"
                  onClick={() => setEditingId(null)}
                >
                  Cancel
                </button>
              </>
            ) : (
              <>
                <span className="flex-1 font-medium">{player.name}</span>
                <button
                  className="px-3 py-1 bg-gray-100 rounded text-sm active:bg-gray-200"
                  onClick={() => startEdit(player)}
                >
                  Rename
                </button>
              </>
            )}
          </div>
        ))}
      </div>

      <div className="flex gap-2">
        <input
          type="text"
          placeholder="New player name"
          value={newName}
          onChange={e => setNewName(e.target.value)}
          onKeyDown={e => e.key === 'Enter' && createPlayer()}
          className="flex-1 px-3 py-2 border rounded"
        />
        <button
          className="px-4 py-2 bg-green-600 text-white rounded font-medium"
          onClick={createPlayer}
        >
          Add
        </button>
      </div>
    </div>
  );
}
