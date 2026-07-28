import { useEffect, useState } from 'react';
import { Link } from 'react-router-dom';

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

  useEffect(() => {
    fetch('/api/records')
      .then(res => res.json())
      .then(setData);
  }, []);

  if (!data) return <div>Loading...</div>;

  return (
    <div>
      <h1 className="text-2xl font-bold mb-6">Records</h1>

      <section className="mb-8">
        <h2 className="text-xl font-semibold mb-3">🏆 Biggest Win (single game)</h2>
        <table className="w-full text-left border-collapse">
          <thead>
            <tr className="border-b">
              <th className="p-2">#</th>
              <th className="p-2">Player</th>
              <th className="p-2">Game</th>
              <th className="p-2">Net</th>
            </tr>
          </thead>
          <tbody>
            {data.bestWins.map((r, i) => (
              <tr key={r.playerName} className="border-b">
                <td className="p-2">{i + 1}</td>
                <td className="p-2">
                  <Link to={`/players/${r.playerName}`} className="text-blue-600 hover:underline">{r.playerName}</Link>
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
        <h2 className="text-xl font-semibold mb-3">💀 Biggest Loss (single game)</h2>
        <table className="w-full text-left border-collapse">
          <thead>
            <tr className="border-b">
              <th className="p-2">#</th>
              <th className="p-2">Player</th>
              <th className="p-2">Game</th>
              <th className="p-2">Net</th>
            </tr>
          </thead>
          <tbody>
            {data.worstLosses.map((r, i) => (
              <tr key={r.playerName} className="border-b">
                <td className="p-2">{i + 1}</td>
                <td className="p-2">
                  <Link to={`/players/${r.playerName}`} className="text-blue-600 hover:underline">{r.playerName}</Link>
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
        <h2 className="text-xl font-semibold mb-3">🎰 Biggest Buy-in (top 5 players)</h2>
        <table className="w-full text-left border-collapse">
          <thead>
            <tr className="border-b">
              <th className="p-2">#</th>
              <th className="p-2">Player</th>
              <th className="p-2">Game</th>
              <th className="p-2">Buy-in</th>
            </tr>
          </thead>
          <tbody>
            {data.topBuyIns.map((r, i) => (
              <tr key={r.playerName} className="border-b">
                <td className="p-2">{i + 1}</td>
                <td className="p-2">
                  <Link to={`/players/${r.playerName}`} className="text-blue-600 hover:underline">{r.playerName}</Link>
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
        <h2 className="text-xl font-semibold mb-3">💰 Biggest Pot (top 5 games)</h2>
        <table className="w-full text-left border-collapse">
          <thead>
            <tr className="border-b">
              <th className="p-2">#</th>
              <th className="p-2">Game</th>
              <th className="p-2">Pot</th>
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
