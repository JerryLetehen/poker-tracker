import { useEffect, useState } from 'react';
import { Link } from 'react-router-dom';

export default function Players() {
  const [players, setPlayers] = useState<string[]>([]);

  useEffect(() => {
    fetch('/api/player-list')
      .then(res => res.json())
      .then(setPlayers);
  }, []);

  return (
    <div>
      <h1 className="text-2xl font-bold mb-4">Players</h1>
      <ul className="space-y-2">
        {players.map(name => (
          <li key={name}>
            <Link to={`/players/${name}`} className="text-blue-600 hover:underline text-lg">
              {name}
            </Link>
          </li>
        ))}
      </ul>
    </div>
  );
}
