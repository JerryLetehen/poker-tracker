import { useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { CrownName } from '../hooks/useTopPlayer';
import { apiFetch } from '../api/client';
import { useI18n } from '../i18n/I18nProvider';

export default function Players() {
  const [players, setPlayers] = useState<string[]>([]);
  const { t } = useI18n();

  useEffect(() => {
    apiFetch<string[]>('/player-list')
      .then(data => setPlayers([...data].sort()))
      .catch(() => {});
  }, []);

  return (
    <div>
      <h1 className="text-2xl font-bold mb-4">{t('players.title')}</h1>
      <ul className="space-y-2">
        {players.map(name => (
          <li key={name}>
            <Link to={`/players/${name}`} className="text-blue-600 hover:underline text-lg">
              <CrownName name={name} />
            </Link>
          </li>
        ))}
      </ul>
    </div>
  );
}
