import { useEffect, useState } from 'react';
import { useParams, Link } from 'react-router-dom';
import { CrownName } from '../hooks/useTopPlayer';
import { formatValue, valueColor, findBest, findWorst } from '../utils';
import { apiFetch } from '../api/client';
import { useI18n } from '../i18n/I18nProvider';
import { Line } from 'react-chartjs-2';
import {
  Chart as ChartJS,
  CategoryScale,
  LinearScale,
  PointElement,
  LineElement,
  Title,
  Tooltip,
  Legend,
  Filler,
  type Plugin,
} from 'chart.js';

ChartJS.register(CategoryScale, LinearScale, PointElement, LineElement, Title, Tooltip, Legend, Filler);

const zeroLineBackground: Plugin<'line'> = {
  id: 'zeroLineBackground',
  beforeDraw(chart) {
    const { ctx, chartArea, scales } = chart;
    if (!chartArea || !scales.y) return;
    const yScale = scales.y;
    const zeroY = yScale.getPixelForValue(0);
    const top = chartArea.top;
    const bottom = chartArea.bottom;
    const left = chartArea.left;
    const right = chartArea.right;
    if (zeroY > top) {
      ctx.save();
      ctx.fillStyle = 'rgba(34, 197, 94, 0.05)';
      ctx.fillRect(left, top, right - left, Math.min(zeroY, bottom) - top);
      ctx.restore();
    }
    if (zeroY < bottom) {
      ctx.save();
      ctx.fillStyle = 'rgba(239, 68, 68, 0.05)';
      ctx.fillRect(left, Math.max(zeroY, top), right - left, bottom - Math.max(zeroY, top));
      ctx.restore();
    }
  },
};

interface ProgressionPoint {
  gameId: number;
  date: string;
  result: number;
  cumulativeResult: number;
  rate: number;
  buyIn: number;
}

export default function PlayerProfile() {
  const { name } = useParams();
  const [data, setData] = useState<ProgressionPoint[]>([]);
  const { t } = useI18n();

  useEffect(() => {
    apiFetch<ProgressionPoint[]>(`/players/${name}/progression`)
      .then(setData)
      .catch(() => {});
  }, [name]);

  const lastPoint = data.length > 0 ? data[data.length - 1] : null;
  const bestWin = findBest(data, p => p.result);
  const worstLoss = findWorst(data, p => p.result);
  const maxCumulative = findBest(data, p => p.cumulativeResult);
  const minCumulative = findWorst(data, p => p.cumulativeResult);
  const biggestBuyIn = findBest(data, p => p.buyIn);

  const chartData = {
    labels: data.map(p => p.date),
    datasets: [
      {
        label: t('player.cumulative'),
        data: data.map(p => p.cumulativeResult),
        borderColor: 'rgb(59, 130, 246)',
        tension: 0.1,
      },
    ],
  };

  return (
    <div>
      <h1 className="text-lg font-bold mb-3"><CrownName name={name || ''} /></h1>

      {lastPoint && (
        <div className="space-y-1 mb-4">
          <div className="flex justify-between border rounded-lg p-2">
            <span className="text-gray-500 text-sm">{t('player.games')}</span>
            <span className="font-medium text-sm">{data.length}</span>
          </div>
          <div className="flex justify-between border rounded-lg p-2">
            <span className="text-gray-500 text-sm">{t('player.net')}</span>
            <span className={`font-medium text-sm ${valueColor(lastPoint.cumulativeResult)}`}>{formatValue(lastPoint.cumulativeResult)}</span>
          </div>
          <div className="flex justify-between border rounded-lg p-2">
            <span className="text-gray-500 text-sm">{t('player.rate')}</span>
            <span className={`font-medium text-sm ${valueColor(lastPoint.rate)}`}>{formatValue(lastPoint.rate)}</span>
          </div>

          {bestWin && (
            <div className="flex justify-between border rounded-lg p-2">
              <span className="text-gray-500 text-sm">{t('player.best')}</span>
              <span className="text-sm">
                <span className={`font-medium ${valueColor(bestWin.result)}`}>{formatValue(bestWin.result)}</span>
                {' '}<Link to={`/games/${bestWin.gameId}`} className="text-blue-600 text-xs">({bestWin.date})</Link>
              </span>
            </div>
          )}
          {worstLoss && (
            <div className="flex justify-between border rounded-lg p-2">
              <span className="text-gray-500 text-sm">{t('player.worst')}</span>
              <span className="text-sm">
                <span className={`font-medium ${valueColor(worstLoss.result)}`}>{formatValue(worstLoss.result)}</span>
                {' '}<Link to={`/games/${worstLoss.gameId}`} className="text-blue-600 text-xs">({worstLoss.date})</Link>
              </span>
            </div>
          )}
          {maxCumulative && (
            <div className="flex justify-between border rounded-lg p-2">
              <span className="text-gray-500 text-sm">{t('player.peak')}</span>
              <span className="text-sm">
                <span className={`font-medium ${valueColor(maxCumulative.cumulativeResult)}`}>{formatValue(maxCumulative.cumulativeResult)}</span>
                {' '}<Link to={`/games/${maxCumulative.gameId}`} className="text-blue-600 text-xs">({maxCumulative.date})</Link>
              </span>
            </div>
          )}
          {minCumulative && (
            <div className="flex justify-between border rounded-lg p-2">
              <span className="text-gray-500 text-sm">{t('player.low')}</span>
              <span className="text-sm">
                <span className={`font-medium ${valueColor(minCumulative.cumulativeResult)}`}>{formatValue(minCumulative.cumulativeResult)}</span>
                {' '}<Link to={`/games/${minCumulative.gameId}`} className="text-blue-600 text-xs">({minCumulative.date})</Link>
              </span>
            </div>
          )}
          {biggestBuyIn && (
            <div className="flex justify-between border rounded-lg p-2">
              <span className="text-gray-500 text-sm">{t('player.maxBuyIn')}</span>
              <span className="text-sm">
                <span className="font-medium">{biggestBuyIn.buyIn.toFixed(2)}</span>
                {' '}<Link to={`/games/${biggestBuyIn.gameId}`} className="text-blue-600 text-xs">({biggestBuyIn.date})</Link>
              </span>
            </div>
          )}
        </div>
      )}

      <div className="h-64">
        <Line data={chartData} plugins={[zeroLineBackground]} options={{ responsive: true, maintainAspectRatio: false }} />
      </div>

      {/* Player's games list */}
      {data.length > 0 && (
        <div className="mt-4 space-y-2">
          <p className="text-sm text-gray-500 font-medium">{t('player.gamesList')}</p>
          {[...data].reverse().map(point => (
            <Link key={point.gameId} to={`/games/${point.gameId}`} className="block">
              <div className="flex justify-between items-center border rounded-lg p-2 active:bg-gray-50">
                <span className="text-sm">{point.date}</span>
                <span className={`text-sm font-medium ${valueColor(point.result)}`}>{formatValue(point.result)}</span>
              </div>
            </Link>
          ))}
        </div>
      )}
    </div>
  );
}
