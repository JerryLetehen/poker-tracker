import { useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { apiFetch } from '../api/client';
import { useI18n } from '../i18n/I18nProvider';

interface Game {
  id: number;
  date: string;
  gameType: string;
  playerCount: number;
  pot: number;
  biggestBuyInPlayer: string;
  biggestBuyInAmount: number;
  biggestNetPlayer: string;
  biggestNetAmount: number;
  biggestLossPlayer: string;
  biggestLossAmount: number;
}

export default function Games() {
  const [games, setGames] = useState<Game[]>([]);
  const { t } = useI18n();

  useEffect(() => {
    apiFetch<Game[]>('/games')
      .then(setGames)
      .catch(() => {});
  }, []);

  return (
    <div>
      <h1 className="text-lg font-bold mb-3">{t('games.title')}</h1>
      <div className="space-y-2">
        {games.map(game => (
          <Link key={game.id} to={`/games/${game.id}`} className="block">
            <div className="border rounded-lg p-3 active:bg-gray-50">
              <div className="flex justify-between items-center">
                <span className="font-medium">{game.date}</span>
                <span className="text-xs text-gray-400">{game.gameType}</span>
              </div>
              <div className="flex gap-3 mt-1 text-sm text-gray-600">
                <span>{game.playerCount} {t('games.players')}</span>
                <span>{t('games.pot')}: {game.pot.toFixed(0)}</span>
              </div>
              <div className="flex gap-3 mt-1 text-xs">
                <span>🏆 <span className="text-green-600">{game.biggestNetPlayer} +{game.biggestNetAmount.toFixed(0)}</span></span>
                <span>💀 <span className="text-red-600">{game.biggestLossPlayer} {game.biggestLossAmount.toFixed(0)}</span></span>
              </div>
            </div>
          </Link>
        ))}
      </div>
    </div>
  );
}
