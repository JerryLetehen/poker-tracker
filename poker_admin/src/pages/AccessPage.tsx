import { useEffect, useState } from 'react';
import { apiFetch, apiJson } from '../api/client';

interface AccessUser {
  id: number;
  telegramUserId: number;
  scope: 'public' | 'admin';
  label: string | null;
  addedAt: string;
}

interface PendingRequest {
  id: number;
  telegramUserId: number;
  firstName: string | null;
  lastName: string | null;
  username: string | null;
  firstSeenAt: string;
  lastSeenAt: string;
}

/**
 * Lets an admin manage who can use the public and admin apps:
 *   - Approve people who "knocked" (pending requests) with one tap.
 *   - Add someone manually by Telegram id.
 *   - Remove existing allow-list entries.
 */
export default function AccessPage() {
  const [users, setUsers] = useState<AccessUser[]>([]);
  const [pending, setPending] = useState<PendingRequest[]>([]);
  const [newId, setNewId] = useState('');
  const [newLabel, setNewLabel] = useState('');
  const [newScope, setNewScope] = useState<'public' | 'admin'>('public');
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    load();
  }, []);

  async function load() {
    try {
      const [u, p] = await Promise.all([
        apiFetch<AccessUser[]>('/admin/access/users'),
        apiFetch<PendingRequest[]>('/admin/access/pending'),
      ]);
      setUsers(u);
      setPending(p);
    } catch {
      setError('Failed to load access data.');
    }
  }

  async function approve(id: number, scope: 'public' | 'admin') {
    await apiJson(`/admin/access/pending/${id}/approve`, 'POST', { scope });
    load();
  }

  async function dismiss(id: number) {
    await apiFetch(`/admin/access/pending/${id}`, { method: 'DELETE' });
    load();
  }

  async function addUser() {
    const id = Number(newId.trim());
    if (!Number.isInteger(id) || id <= 0) {
      setError('Enter a valid numeric Telegram ID.');
      return;
    }
    setError(null);
    await apiJson('/admin/access/users', 'POST', { telegramUserId: id, scope: newScope, label: newLabel.trim() || null });
    setNewId('');
    setNewLabel('');
    load();
  }

  async function removeUser(id: number) {
    if (!confirm('Remove this user from the allow-list?')) return;
    await apiFetch(`/admin/access/users/${id}`, { method: 'DELETE' });
    load();
  }

  function displayName(p: PendingRequest): string {
    const name = [p.firstName, p.lastName].filter(Boolean).join(' ');
    if (name && p.username) return `${name} (@${p.username})`;
    return name || (p.username ? `@${p.username}` : `ID ${p.telegramUserId}`);
  }

  return (
    <div>
      <h1 className="text-xl font-bold mb-4">Access</h1>

      {error && <p className="text-red-600 text-sm mb-3">{error}</p>}

      {/* Pending requests */}
      <section className="mb-6">
        <h2 className="font-semibold mb-2">Pending requests</h2>
        {pending.length === 0 ? (
          <p className="text-gray-400 text-sm">No one is waiting for approval.</p>
        ) : (
          <div className="space-y-2">
            {pending.map((p) => (
              <div key={p.id} className="border rounded-lg p-3">
                <div className="flex justify-between items-center">
                  <span className="font-medium">{displayName(p)}</span>
                  <span className="text-xs text-gray-400 font-mono">{p.telegramUserId}</span>
                </div>
                <div className="flex gap-2 mt-2">
                  <button
                    className="flex-1 py-1 bg-green-600 text-white rounded text-sm active:bg-green-700"
                    onClick={() => approve(p.id, 'public')}
                  >
                    Approve → Public
                  </button>
                  <button
                    className="flex-1 py-1 bg-blue-600 text-white rounded text-sm active:bg-blue-700"
                    onClick={() => approve(p.id, 'admin')}
                  >
                    Approve → Admin
                  </button>
                  <button
                    className="px-3 py-1 bg-gray-200 rounded text-sm active:bg-gray-300"
                    onClick={() => dismiss(p.id)}
                  >
                    Dismiss
                  </button>
                </div>
              </div>
            ))}
          </div>
        )}
      </section>

      {/* Add manually */}
      <section className="mb-6">
        <h2 className="font-semibold mb-2">Add by Telegram ID</h2>
        <div className="space-y-2 border rounded-lg p-3">
          <input
            type="number"
            placeholder="Telegram numeric ID"
            value={newId}
            onChange={(e) => setNewId(e.target.value)}
            className="w-full px-3 py-2 border rounded"
          />
          <input
            type="text"
            placeholder="Label (optional)"
            value={newLabel}
            onChange={(e) => setNewLabel(e.target.value)}
            className="w-full px-3 py-2 border rounded"
          />
          <div className="flex gap-2">
            <button
              className={`flex-1 py-2 rounded font-medium ${newScope === 'public' ? 'bg-blue-600 text-white' : 'bg-gray-100'}`}
              onClick={() => setNewScope('public')}
            >
              Public
            </button>
            <button
              className={`flex-1 py-2 rounded font-medium ${newScope === 'admin' ? 'bg-blue-600 text-white' : 'bg-gray-100'}`}
              onClick={() => setNewScope('admin')}
            >
              Admin
            </button>
          </div>
          <button
            className="w-full py-2 bg-green-600 text-white rounded font-medium active:bg-green-700"
            onClick={addUser}
          >
            Add
          </button>
        </div>
      </section>

      {/* Current allow-list */}
      <section>
        <h2 className="font-semibold mb-2">Allowed users</h2>
        {users.length === 0 ? (
          <p className="text-gray-400 text-sm">No one is allow-listed yet.</p>
        ) : (
          <div className="space-y-2">
            {users.map((u) => (
              <div key={u.id} className="flex justify-between items-center border rounded-lg p-3">
                <div>
                  <span className="font-medium">{u.label || `ID ${u.telegramUserId}`}</span>
                  <span className={`ml-2 text-xs px-2 py-0.5 rounded ${u.scope === 'admin' ? 'bg-purple-100 text-purple-700' : 'bg-gray-100 text-gray-600'}`}>
                    {u.scope}
                  </span>
                  <div className="text-xs text-gray-400 font-mono">{u.telegramUserId}</div>
                </div>
                <button
                  className="px-3 py-1 text-red-500 hover:bg-red-50 rounded text-sm"
                  onClick={() => removeUser(u.id)}
                >
                  Remove
                </button>
              </div>
            ))}
          </div>
        )}
      </section>
    </div>
  );
}
