import { useEffect, useRef, useState } from 'react';
import Highcharts from 'highcharts';
import 'highcharts/modules/annotations';
import { apiFetch } from '../api/client';
import { useI18n } from '../i18n/I18nProvider';

interface PlayerSnapshot {
  name: string;
  cumulativeNet: number;
}

interface Snapshot {
  date: string;
  players: PlayerSnapshot[];
}

const COLORS = [
  '#3b82f6', '#ef4444', '#10b981', '#f59e0b', '#8b5cf6',
  '#ec4899', '#06b6d4', '#84cc16', '#f97316', '#6366f1',
  '#14b8a6', '#e11d48', '#0ea5e9', '#a855f7', '#22c55e',
];

export default function ChartRace() {
  const containerRef = useRef<HTMLDivElement>(null);
  const [totalRounds, setTotalRounds] = useState(0);
  const { t } = useI18n();

  useEffect(() => {
    apiFetch<Snapshot[]>('/leaderboard/history')
      .then((snapshots) => {
        setTotalRounds(snapshots.length - 1);
        buildChart(snapshots);
      })
      .catch(() => {});
  }, []);

  function buildChart(snapshots: Snapshot[]) {
    if (!containerRef.current) return;

    const allPlayers = [...new Set(snapshots.flatMap(s => s.players.map(p => p.name)))];
    const startRound = 0;
    const endRound = snapshots.length - 1;
    const animationDuration = 1000;

    // Build lookup: formatPoints[playerIndex][roundIndex] = value
    const formatPoints: Record<number, number | null>[] = allPlayers.map((name) => {
      const points: Record<number, number | null> = {};
      snapshots.forEach((snapshot, round) => {
        const player = snapshot.players.find(p => p.name === name);
        points[round] = player ? Number(player.cumulativeNet) : null;
      });
      return points;
    });

    // Pre-calculate y-axis range
    let minVal = 0, maxVal = 0;
    for (const snapshot of snapshots) {
      for (const player of snapshot.players) {
        const val = Number(player.cumulativeNet);
        if (val < minVal) minVal = val;
        if (val > maxVal) maxVal = val;
      }
    }
    const padding = (maxVal - minVal) * 0.1 || 10;

    const input = document.getElementById('play-range') as HTMLInputElement;
    const btn = document.getElementById('play-pause-button') as HTMLButtonElement;

    const chart = Highcharts.chart(containerRef.current, {
      chart: {
        type: 'line',
        marginRight: 110,
        backgroundColor: 'transparent',
        animation: {
          duration: animationDuration,
          easing: (t: number) => t,
        },
      },
      title: {
        text: 'Leaderboard Race',
        floating: true,
        align: 'left',
        x: 70,
        y: 50,
        style: { color: '#e5e7eb' },
      },
      xAxis: {
        allowDecimals: false,
        min: startRound,
        max: endRound,
      },
      yAxis: {
        min: minVal - padding,
        max: maxVal + padding,
        title: { text: 'Cumulative Net' },
        plotBands: [
          {
            from: 0,
            to: maxVal + padding,
            color: 'rgba(34, 197, 94, 0.05)',
          },
          {
            from: minVal - padding,
            to: 0,
            color: 'rgba(239, 68, 68, 0.05)',
          },
        ],
      },
      tooltip: {
        split: true,
        headerFormat: '<span style="font-size: 1.2em">Game {point.x}</span>',
        pointFormat: '{series.name}: {point.y:.1f}',
        crosshairs: true,
      },
      legend: { enabled: false },
      plotOptions: {
        line: {
          animation: false,
          pointStart: startRound,
          marker: { enabled: false },
        },
      },
      annotations: [{
        crop: false,
        labelOptions: {
          borderWidth: 0,
          backgroundColor: 'transparent',
          align: 'left',
          verticalAlign: 'middle',
          overflow: 'allow',
          style: {
            pointerEvents: 'none',
            transition: 'opacity 0.5s',
          },
          x: -8,
          y: -1,
        },
        labels: allPlayers.map(() => ({
          text: '',
          point: { x: 0, xAxis: 0, y: 0, yAxis: 0 },
        })),
      }],
      series: allPlayers.map((name, i) => ({
        type: 'line' as const,
        name,
        color: COLORS[i % COLORS.length],
        data: [] as (number | null)[],
      })),
    });

    function pause() {
      btn.textContent = '▶ Play';
      clearTimeout((chart as any).sequenceTimer);
      (chart as any).sequenceTimer = undefined;
    }

    function update(sliderClicked: boolean) {
      const series = chart.series;
      const labels = (chart as any).annotations[0].labels;
      const yearIndex = parseInt(input.value, 10) - startRound;
      const dataLength = (series[0].options as any).data
        ? ((series[0].options as any).data as any[]).length
        : 0;

      // If slider moved back in time
      if (yearIndex < dataLength - 1) {
        for (let i = 0; i < series.length; i++) {
          const seriesData = ((series[i].options as any).data as any[]).slice(0, yearIndex);
          series[i].setData(seriesData, false);
        }
      }

      // If slider moved forward in time
      if (yearIndex > dataLength - 1) {
        const remaining = yearIndex - dataLength;
        for (let i = 0; i < series.length; i++) {
          for (let j = parseInt(input.value, 10) - remaining; j < parseInt(input.value, 10); j++) {
            series[i].addPoint([formatPoints[i][j]], false);
          }
        }
      }

      // Add current point and update labels
      for (let i = 0; i < series.length; i++) {
        const newY = formatPoints[i][parseInt(input.value, 10)];

        labels[i].options.point.x = yearIndex;
        labels[i].options.point.y = newY;

        if (yearIndex === 0) {
          labels[i].options.text =
            `<span style="color:${series[i].color as string}">●</span> ` +
            `<span style="color:${series[i].color as string}">${series[i].name}</span>`;
        }

        if (((series[i].options as any).data as any[] || []).length <= yearIndex) {
          series[i].addPoint(newY, false);
        }
      }

      if (sliderClicked) {
        chart.redraw(false);
      } else {
        chart.redraw();
      }

      input.value = String(parseInt(input.value, 10) + 1);

      if (parseInt(input.value, 10) > endRound) {
        pause();
      }
    }

    function play() {
      if (parseInt(input.value, 10) > endRound) {
        input.value = String(startRound);
      }
      btn.textContent = '⏸ Pause';
      (chart as any).sequenceTimer = setInterval(() => {
        update(false);
      }, animationDuration);
    }

    btn.addEventListener('click', () => {
      if ((chart as any).sequenceTimer) {
        pause();
      } else {
        play();
      }
    });

    input.addEventListener('input', () => {
      pause();
      update(true);
    });

    // Initialize
    update(true);
    update(false);
    play();
  }

  return (
    <div>
      <h1 className="text-2xl font-bold mb-4">{t('race.title')}</h1>
      <div className="mb-4 flex gap-2 items-center">
        <button id="play-pause-button" className="px-3 py-1 rounded bg-green-600 text-white">
          ▶ Play
        </button>
      </div>
      <div className="mb-4">
        <input
          id="play-range"
          type="range"
          min={0}
          max={totalRounds}
          defaultValue={0}
          className="w-full"
        />
      </div>
      <div ref={containerRef} style={{ height: '600px' }} />
    </div>
  );
}
