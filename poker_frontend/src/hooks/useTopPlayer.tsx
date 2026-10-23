import { useEffect, useState } from 'react';
import { apiFetch } from '../api/client';

let cachedTopPlayer: string | null = null;

interface LeaderboardRow { name: string }

export function useTopPlayer() {
  const [topPlayer, setTopPlayer] = useState<string | null>(cachedTopPlayer);

  useEffect(() => {
    if (cachedTopPlayer) return;
    apiFetch<LeaderboardRow[]>('/leaderboard?sortBy=net&minGames=1')
      .then(data => {
        if (data.length > 0) {
          cachedTopPlayer = data[0].name;
          setTopPlayer(data[0].name);
        }
      })
      .catch(() => {});
  }, []);

  return topPlayer;
}

export function CrownName({ name }: { name: string }) {
  const topPlayer = useTopPlayer();
  return <>{name}{topPlayer === name ? ' 👑' : ''}</>;
}
