import { useEffect, useState } from 'react';
import { apiFetch } from '../api/client';
import { getInitData } from '../api/telegram';

interface DbStatus {
  dbSizeBytes: number;
  walSizeBytes: number;
}

const API_BASE_URL = (import.meta.env.VITE_API_BASE_URL ?? '/api').replace(/\/$/, '');

function formatBytes(bytes: number): string {
  if (bytes === 0) return '0 B';
  const units = ['B', 'KB', 'MB', 'GB'];
  const i = Math.floor(Math.log(bytes) / Math.log(1024));
  return `${(bytes / Math.pow(1024, i)).toFixed(i === 0 ? 0 : 1)} ${units[i]}`;
}

export default function DatabasePage() {
  const [status, setStatus] = useState<DbStatus | null>(null);
  const [checkpointing, setCheckpointing] = useState(false);

  useEffect(() => {
    loadStatus();
  }, []);

  async function loadStatus() {
    setStatus(await apiFetch<DbStatus>('/admin/db/status'));
  }

  async function checkpoint() {
    setCheckpointing(true);
    await apiFetch('/admin/db/checkpoint', { method: 'POST' });
    await loadStatus();
    setCheckpointing(false);
  }

  // The backup is a binary download. A plain <a href> cannot carry the Telegram auth
  // header, so fetch it with auth and save the resulting blob.
  async function downloadBackup() {
    const headers = new Headers({ 'X-App': 'admin' });
    const initData = getInitData();
    if (initData) headers.set('Authorization', `tma ${initData}`);

    const res = await fetch(`${API_BASE_URL}/admin/db/backup`, { headers });
    if (!res.ok) return;

    const blob = await res.blob();
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = 'poker-backup.zip';
    a.click();
    URL.revokeObjectURL(url);
  }

  // Large WAL = worth checkpointing (> 4 MB is the SQLite auto-checkpoint threshold)
  const walIsLarge = status ? status.walSizeBytes > 4 * 1024 * 1024 : false;

  return (
    <div>
      <h1 className="text-xl font-bold mb-4">Database</h1>

      {!status ? (
        <p className="text-gray-400">Loading...</p>
      ) : (
        <>
          <div className="space-y-2 mb-6">
            <div className="flex justify-between border rounded-lg p-3">
              <span className="text-gray-500">Database size</span>
              <span className="font-medium">{formatBytes(status.dbSizeBytes)}</span>
            </div>
            <div className="flex justify-between border rounded-lg p-3">
              <span className="text-gray-500">WAL size</span>
              <span className={`font-medium ${walIsLarge ? 'text-orange-600' : ''}`}>
                {formatBytes(status.walSizeBytes)}
              </span>
            </div>
          </div>

          <div className="space-y-3">
            <button
              className="w-full py-3 bg-blue-600 text-white rounded-lg font-medium active:bg-blue-700 disabled:opacity-50"
              onClick={checkpoint}
              disabled={checkpointing}
            >
              {checkpointing ? 'Checkpointing...' : 'Checkpoint WAL'}
            </button>

            <button
              onClick={downloadBackup}
              className="block w-full py-3 bg-green-600 text-white rounded-lg font-medium active:bg-green-700 text-center"
            >
              Download Backup (.zip)
            </button>
          </div>

          <p className="text-xs text-gray-400 mt-4">
            The backup contains the database. Checkpoint merges pending
            writes (WAL) into the main database file.
          </p>
        </>
      )}
    </div>
  );
}
