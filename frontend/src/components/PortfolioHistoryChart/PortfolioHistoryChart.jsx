import { useEffect, useMemo, useState } from 'react';
import { getPortfolioHistory } from '../../services/apiService';
import { useLanguage } from '../../context/LanguageContext';
import './PortfolioHistoryChart.css';

const RANGES = ['24h', '7d', '30d', 'all'];

// SVG koordinat sistemi; genişlik CSS ile kapsayıcıya uyar (viewBox ölçeklenir).
const WIDTH = 720;
const HEIGHT = 240;
const PAD = { top: 16, right: 16, bottom: 28, left: 72 };

const formatUsd = (value, digits = 2) =>
  `$${Number(value || 0).toLocaleString(undefined, {
    minimumFractionDigits: digits,
    maximumFractionDigits: digits,
  })}`;

const formatAxisDate = (value, range, lang) => {
  const date = new Date(value);
  const locale = lang === 'en' ? 'en-US' : 'tr-TR';
  return range === '24h'
    ? date.toLocaleTimeString(locale, { hour: '2-digit', minute: '2-digit' })
    : date.toLocaleDateString(locale, { day: '2-digit', month: 'short' });
};

const formatFullDate = (value, lang) =>
  new Date(value).toLocaleString(lang === 'en' ? 'en-US' : 'tr-TR', {
    dateStyle: 'medium',
    timeStyle: 'short',
  });

/** Veriyi SVG noktalarına çevirir. En az 2 nokta yoksa null döner. */
const buildChart = (points) => {
  if (points.length < 2) return null;

  const times = points.map((p) => new Date(p.takenAt).getTime());
  const values = points.map((p) => Number(p.totalValue));

  const minTime = Math.min(...times);
  const maxTime = Math.max(...times);
  let minValue = Math.min(...values);
  let maxValue = Math.max(...values);

  // Değer hiç değişmediyse çizgi ortada dursun.
  if (minValue === maxValue) {
    minValue -= 1;
    maxValue += 1;
  }
  const margin = (maxValue - minValue) * 0.1;
  minValue -= margin;
  maxValue += margin;

  const innerW = WIDTH - PAD.left - PAD.right;
  const innerH = HEIGHT - PAD.top - PAD.bottom;
  const x = (t) => PAD.left + ((t - minTime) / (maxTime - minTime || 1)) * innerW;
  const y = (v) => PAD.top + (1 - (v - minValue) / (maxValue - minValue)) * innerH;

  const coords = points.map((p, i) => ({ x: x(times[i]), y: y(values[i]), point: p }));
  const line = coords.map((c, i) => `${i === 0 ? 'M' : 'L'}${c.x.toFixed(1)},${c.y.toFixed(1)}`).join(' ');
  const baseY = PAD.top + innerH;
  const area = `${line} L${coords[coords.length - 1].x.toFixed(1)},${baseY} L${coords[0].x.toFixed(1)},${baseY} Z`;

  const yTicks = [0, 0.5, 1].map((r) => {
    const value = minValue + (maxValue - minValue) * r;
    return { value, y: y(value) };
  });
  const xTicks = [0, Math.floor((points.length - 1) / 2), points.length - 1]
    .filter((index, i, all) => all.indexOf(index) === i)
    .map((index) => coords[index]);

  return { coords, line, area, yTicks, xTicks };
};

export default function PortfolioHistoryChart() {
  const { t, lang } = useLanguage();
  const [range, setRange] = useState('24h');
  const [data, setData] = useState(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(false);
  const [hoverIndex, setHoverIndex] = useState(null);

  useEffect(() => {
    let cancelled = false;

    const fetchHistory = async () => {
      try {
        setLoading(true);
        setError(false);
        setHoverIndex(null);
        const response = await getPortfolioHistory(range);
        if (!cancelled) setData(response.data);
      } catch (err) {
        console.error('Portföy geçmişi yüklenemedi:', err);
        if (!cancelled) {
          setData(null);
          setError(true);
        }
      } finally {
        if (!cancelled) setLoading(false);
      }
    };

    fetchHistory();
    return () => {
      cancelled = true;
    };
  }, [range]);

  const points = useMemo(() => data?.points || [], [data]);
  const chart = useMemo(() => buildChart(points), [points]);

  const changeUsd = data?.changeUsd;
  const changePercent = data?.changePercent;
  const hasChange = changeUsd !== null && changeUsd !== undefined;
  const isUp = hasChange && Number(changeUsd) >= 0;
  const hovered = chart && hoverIndex !== null ? chart.coords[hoverIndex] : null;

  // Fare hangi noktaya en yakınsa onu seç (SVG ölçeklendiği için oranla hesaplanır).
  const handleMouseMove = (event) => {
    if (!chart) return;
    const rect = event.currentTarget.getBoundingClientRect();
    const svgX = ((event.clientX - rect.left) / rect.width) * WIDTH;
    let nearest = 0;
    chart.coords.forEach((c, i) => {
      if (Math.abs(c.x - svgX) < Math.abs(chart.coords[nearest].x - svgX)) nearest = i;
    });
    setHoverIndex(nearest);
  };

  const renderBody = () => {
    if (loading) return <div className="portfolio-history-empty">{t('portfolio.history.loading')}</div>;
    if (error) return <div className="portfolio-history-empty">{t('portfolio.history.error')}</div>;
    if (points.length === 0) return <div className="portfolio-history-empty">{t('portfolio.history.empty')}</div>;
    if (!chart) {
      return (
        <div className="portfolio-history-empty">
          <strong>{formatUsd(points[0].totalValue)}</strong>
          <span>{formatFullDate(points[0].takenAt, lang)}</span>
          <span>{t('portfolio.history.singlePoint')}</span>
        </div>
      );
    }

    return (
      <div className="portfolio-history-chart-wrap">
        <svg
          className={`portfolio-history-chart ${isUp ? 'up' : 'down'}`}
          viewBox={`0 0 ${WIDTH} ${HEIGHT}`}
          role="img"
          aria-label={t('portfolio.history.chartLabel')}
          onMouseMove={handleMouseMove}
          onMouseLeave={() => setHoverIndex(null)}
        >
          {chart.yTicks.map((tick) => (
            <g key={tick.y}>
              <line className="grid" x1={PAD.left} x2={WIDTH - PAD.right} y1={tick.y} y2={tick.y} />
              <text className="axis" x={PAD.left - 8} y={tick.y + 4} textAnchor="end">
                {formatUsd(tick.value, 0)}
              </text>
            </g>
          ))}
          {chart.xTicks.map((tick, i) => (
            <text
              key={tick.point.takenAt}
              className="axis"
              x={tick.x}
              y={HEIGHT - 8}
              textAnchor={i === 0 ? 'start' : i === chart.xTicks.length - 1 ? 'end' : 'middle'}
            >
              {formatAxisDate(tick.point.takenAt, range, lang)}
            </text>
          ))}

          <path className="area" d={chart.area} />
          <path className="line" d={chart.line} />

          {hovered && (
            <g>
              <line className="hover-line" x1={hovered.x} x2={hovered.x} y1={PAD.top} y2={HEIGHT - PAD.bottom} />
              <circle className="hover-dot" cx={hovered.x} cy={hovered.y} r="5" />
            </g>
          )}
        </svg>

        {hovered && (
          <div className="portfolio-history-tooltip">
            <strong>{formatUsd(hovered.point.totalValue)}</strong>
            <span>{formatFullDate(hovered.point.takenAt, lang)}</span>
          </div>
        )}
      </div>
    );
  };

  return (
    <div className="portfolio-section portfolio-history">
      <div className="portfolio-history-header">
        <div>
          <h2>{t('portfolio.history.title')}</h2>
          {hasChange && !loading && (
            <p className={`portfolio-history-change ${isUp ? 'positive' : 'negative'}`}>
              {t('portfolio.history.change')}:{' '}
              <strong>
                {isUp ? '▲' : '▼'} {isUp ? '+' : '-'}{formatUsd(Math.abs(Number(changeUsd)))}
                {changePercent !== null && changePercent !== undefined && (
                  <> ({isUp ? '+' : ''}{Number(changePercent).toFixed(2)}%)</>
                )}
              </strong>
            </p>
          )}
        </div>
        <div className="portfolio-history-ranges" role="group" aria-label={t('portfolio.history.title')}>
          {RANGES.map((item) => (
            <button
              key={item}
              type="button"
              className={item === range ? 'active' : ''}
              aria-pressed={item === range}
              onClick={() => setRange(item)}
            >
              {t(`portfolio.history.range.${item}`)}
            </button>
          ))}
        </div>
      </div>

      {renderBody()}
    </div>
  );
}
