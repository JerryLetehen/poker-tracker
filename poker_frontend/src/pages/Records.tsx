import { useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { CrownName } from '../hooks/useTopPlayer';
import { apiFetch } from '../api/client';
import { useI18n } from '../i18n/I18nProvider';

interface WinLossRecord {
  playerName: string;
  gameId: number;
  date: string;
  net: number;
}

interface GameByPlayers {
  id: number;
  date: string;
  playerCount: number;
}

interface GameByPot {
  id: number;
  date: string;
  pot: number;
}

interface BuyInRecord {
  playerName: string;
  gameId: number;
  date: string;
  buyIn: number;
}

interface RecordsData {
  bestWins: WinLossRecord[];
  worstLosses: WinLossRecord[];
  topByPlayers: GameByPlayers[];
  topByPot: GameByPot[];
  topBuyIns: BuyInRecord[];
}

export default function Records() {
  const [data, setData] = useState<RecordsData | null>(null);
  const { t } = useI18n();

  useEffect(() => {
    apiFetch<RecordsData>('/records')
      .then(setData)
      .catch(() => {});
  }, []);

  if (!data) return <div>{t('common.loading')}</div>;

  return (
    <div>
      <h1 className="text-2xl font-bold mb-6">{t('records.title')}</h1>

      <section className="mb-8">
        <h2 className="text-xl font-semibold mb-3">{t('records.biggestWin')}</h2>
        <table className="w-full text-left border-collapse">
          <thead>
            <tr className="border-b">
              <th className="p-2">#</th>
              <th className="p-2">{t('records.col.player')}</th>
              <th className="p-2">{t('records.col.game')}</th>
              <th className="p-2">{t('records.col.net')}</th>
            </tr>
          </thead>
          <tbody>
            {data.bestWins.map((r, i) => (
              <tr key={r.playerName} className="border-b">
                <td className="p-2">{i + 1}</td>
                <td className="p-2">
                  <Link to={`/players/${r.playerName}`} className="text-blue-600 hover:underline"><CrownName name={r.playerName} /></Link>
                </td>
                <td className="p-2">
                  <Link to={`/games/${r.gameId}`} className="text-blue-600 hover:underline">{r.date}</Link>
                </td>
                <td className="p-2 text-green-600">+{r.net.toFixed(2)}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </section>

      <section className="mb-8">
        <h2 className="text-xl font-semibold mb-3">{t('records.biggestLoss')}</h2>
        <table className="w-full text-left border-collapse">
          <thead>
            <tr className="border-b">
              <th className="p-2">#</th>
              <th className="p-2">{t('records.col.player')}</th>
              <th className="p-2">{t('records.col.game')}</th>
              <th className="p-2">{t('records.col.net')}</th>
            </tr>
          </thead>
          <tbody>
            {data.worstLosses.map((r, i) => (
              <tr key={r.playerName} className="border-b">
                <td className="p-2">{i + 1}</td>
                <td className="p-2">
                  <Link to={`/players/${r.playerName}`} className="text-blue-600 hover:underline"><CrownName name={r.playerName} /></Link>
                </td>
                <td className="p-2">
                  <Link to={`/games/${r.gameId}`} className="text-blue-600 hover:underline">{r.date}</Link>
                </td>
                <td className="p-2 text-red-600">{r.net.toFixed(2)}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </section>

      <section className="mb-8">
        <h2 className="text-xl font-semibold mb-3">{t('records.biggestBuyIn')}</h2>
        <table className="w-full text-left border-collapse">
          <thead>
            <tr className="border-b">
              <th className="p-2">#</th>
              <th className="p-2">{t('records.col.player')}</th>
              <th className="p-2">{t('records.col.game')}</th>
              <th className="p-2">{t('records.col.buyIn')}</th>
            </tr>
          </thead>
          <tbody>
            {data.topBuyIns.map((r, i) => (
              <tr key={r.playerName} className="border-b">
                <td className="p-2">{i + 1}</td>
                <td className="p-2">
                  <Link to={`/players/${r.playerName}`} className="text-blue-600 hover:underline"><CrownName name={r.playerName} /></Link>
                </td>
                <td className="p-2">
                  <Link to={`/games/${r.gameId}`} className="text-blue-600 hover:underline">{r.date}</Link>
                </td>
                <td className="p-2">{r.buyIn.toFixed(2)}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </section>

      <section className="mb-8">
        <h2 className="text-xl font-semibold mb-3">{t('records.biggestPot')}</h2>
        <table className="w-full text-left border-collapse">
          <thead>
            <tr className="border-b">
              <th className="p-2">#</th>
              <th className="p-2">{t('records.col.game')}</th>
              <th className="p-2">{t('records.col.pot')}</th>
            </tr>
          </thead>
          <tbody>
            {data.topByPot.map((g, i) => (
              <tr key={g.id} className="border-b">
                <td className="p-2">{i + 1}</td>
                <td className="p-2">
                  <Link to={`/games/${g.id}`} className="text-blue-600 hover:underline">{g.date}</Link>
                </td>
                <td className="p-2">{g.pot.toFixed(2)}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </section>
    </div>
  );
}
