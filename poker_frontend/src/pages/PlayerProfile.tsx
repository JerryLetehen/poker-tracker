import { useEffect, useState } from 'react';
import { useParams, Link } from 'react-router-dom';
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
  median: number;
  buyIn: number;
}

function formatValue(val: number) {
  return `${val >= 0 ? '+' : ''}${val.toFixed(2)}`;
}

function valueColor(val: number) {
  if (val > 0) return 'text-green-600';
  if (val < 0) return 'text-red-600';
  return '';
}

export default function PlayerProfile() {
  const { name } = useParams();
  const [data, setData] = useState<ProgressionPoint[]>([]);

  useEffect(() => {
    fetch(`/api/players/${name}/progression`)
      .then(res => res.json())
      .then(setData);
  }, [name]);

  const lastPoint = data.length > 0 ? data[data.length - 1] : null;
  const bestWin = data.length > 0 ? data.reduce((max, p) => p.result > max.result ? p : max, data[0]) : null;
  const worstLoss = data.length > 0 ? data.reduce((min, p) => p.result < min.result ? p : min, data[0]) : null;
  const maxCumulative = data.length > 0 ? data.reduce((max, p) => p.cumulativeResult > max.cumulativeResult ? p : max, data[0]) : null;
  const minCumulative = data.length > 0 ? data.reduce((min, p) => p.cumulativeResult < min.cumulativeResult ? p : min, data[0]) : null;
  const biggestBuyIn = data.length > 0 ? data.reduce((max, p) => p.buyIn > max.buyIn ? p : max, data[0]) : null;

  const chartData = {
    labels: data.map(p => p.date),
    datasets: [
      {
        label: 'Cumulative Result',
        data: data.map(p => p.cumulativeResult),
        borderColor: 'rgb(59, 130, 246)',
        tension: 0.1,
      },
    ],
  };

  return (
    <div>
      <h1 className="text-lg font-bold mb-3">{name}</h1>

      {lastPoint && (
        <div className="space-y-1 mb-4">
          <div className="flex justify-between border rounded-lg p-2">
            <span className="text-gray-500 text-sm">Games</span>
            <span className="font-medium text-sm">{data.length}</span>
          </div>
          <div className="flex justify-between border rounded-lg p-2">
            <span className="text-gray-500 text-sm">Rate</span>
            <span className={`font-medium text-sm ${valueColor(lastPoint.rate)}`}>{formatValue(lastPoint.rate)}</span>
          </div>
          <div className="flex justify-between border rounded-lg p-2">
            <span className="text-gray-500 text-sm">Median</span>
            <span className={`font-medium text-sm ${valueColor(lastPoint.median)}`}>{formatValue(lastPoint.median)}</span>
          </div>
          {bestWin && (
            <div className="flex justify-between border rounded-lg p-2">
              <span className="text-gray-500 text-sm">Best</span>
              <span className="text-sm">
                <span className={`font-medium ${valueColor(bestWin.result)}`}>{formatValue(bestWin.result)}</span>
                {' '}<Link to={`/games/${bestWin.gameId}`} className="text-blue-600 text-xs">({bestWin.date})</Link>
              </span>
            </div>
          )}
          {worstLoss && (
            <div className="flex justify-between border rounded-lg p-2">
              <span className="text-gray-500 text-sm">Worst</span>
              <span className="text-sm">
                <span className={`font-medium ${valueColor(worstLoss.result)}`}>{formatValue(worstLoss.result)}</span>
                {' '}<Link to={`/games/${worstLoss.gameId}`} className="text-blue-600 text-xs">({worstLoss.date})</Link>
              </span>
            </div>
          )}
          {maxCumulative && (
            <div className="flex justify-between border rounded-lg p-2">
              <span className="text-gray-500 text-sm">Peak</span>
              <span className="text-sm">
                <span className={`font-medium ${valueColor(maxCumulative.cumulativeResult)}`}>{formatValue(maxCumulative.cumulativeResult)}</span>
                {' '}<Link to={`/games/${maxCumulative.gameId}`} className="text-blue-600 text-xs">({maxCumulative.date})</Link>
              </span>
            </div>
          )}
          {minCumulative && (
            <div className="flex justify-between border rounded-lg p-2">
              <span className="text-gray-500 text-sm">Low</span>
              <span className="text-sm">
                <span className={`font-medium ${valueColor(minCumulative.cumulativeResult)}`}>{formatValue(minCumulative.cumulativeResult)}</span>
                {' '}<Link to={`/games/${minCumulative.gameId}`} className="text-blue-600 text-xs">({minCumulative.date})</Link>
              </span>
            </div>
          )}
          {biggestBuyIn && (
            <div className="flex justify-between border rounded-lg p-2">
              <span className="text-gray-500 text-sm">Max Buy-in</span>
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
    </div>
  );
}
