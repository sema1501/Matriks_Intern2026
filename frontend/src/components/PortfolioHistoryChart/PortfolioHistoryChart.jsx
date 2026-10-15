import { useEffect, useMemo, useState } from 'react';
import { Area, AreaChart, CartesianGrid, ResponsiveContainer, Tooltip, XAxis, YAxis } from 'recharts';
import { getPortfolioHistory } from '../../services/apiService';
import { useLanguage } from '../../context/LanguageContext';
import './PortfolioHistoryChart.css';

const RANGES = ['24h', '7d', '30d', 'all'];

const UP_COLOR = '#16a34a';
const DOWN_COLOR = '#dc2626';
const CHART_HEIGHT = 260;

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

/** 1, 2, 2.5, 5 veya 10'un katı olan "yuvarlak" adım: eksende $10,250 gibi okunaklı değerler çıkar. */
const niceStep = (rough) => {
  const power = 10 ** Math.floor(Math.log10(rough));
  const fraction = rough / power;
  const nice = fraction <= 1 ? 1 : fraction <= 2 ? 2 : fraction <= 2.5 ? 2.5 : fraction <= 5 ? 5 : 10;
  return nice * power;
};

/** Y ekseni sınırları ve çizgileri: en düşük ve en yüksek değeri yuvarlak sayılarla kapsar. */
const buildYAxis = (values) => {
  let min = Math.min(...values);
  let max = Math.max(...values);
  // Değer hiç değişmediyse çizgi ortada dursun.
  if (min === max) {
    const pad = Math.max(1, Math.abs(min) * 0.01);
    min -= pad;
    max += pad;
  }
  const step = niceStep((max - min) / 4);
  const low = Math.max(0, Math.floor(min / step) * step);
  const high = Math.ceil(max / step) * step;
  const ticks = [];
  for (let value = low; value <= high + step / 2; value += step) ticks.push(value);
  // Adım 1'den küçükse (çok küçük portföy) kuruşlar da gösterilir, yoksa etiketler tekrar eder.
  return { domain: [low, high], ticks, digits: step < 1 ? 2 : 0 };
};

function ChartTooltip({ active, payload, lang }) {
  if (!active || !payload || payload.length === 0) return null;
  const point = payload[0].payload;
  return (
    <div className="portfolio-history-tooltip">
      <strong>{formatUsd(point.totalValue)}</strong>
      <span>{formatFullDate(point.time, lang)}</span>
    </div>
  );
}

export default function PortfolioHistoryChart() {
  const { t, lang } = useLanguage();
  const [range, setRange] = useState('24h');
  const [data, setData] = useState(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(false);

  useEffect(() => {
    let cancelled = false;

    const fetchHistory = async () => {
      try {
        setLoading(true);
        setError(false);
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
  // Recharts zaman eksenini sayı olarak ister: tarih milisaniyeye çevrilir.
  const chartData = useMemo(
    () => points.map((p) => ({ time: new Date(p.takenAt).getTime(), totalValue: Number(p.totalValue) })),
    [points]
  );
  const yAxis = useMemo(
    () => (chartData.length > 1 ? buildYAxis(chartData.map((p) => p.totalValue)) : null),
    [chartData]
  );

  const changeUsd = data?.changeUsd;
  const changePercent = data?.changePercent;
  const hasChange = changeUsd !== null && changeUsd !== undefined;
  const isUp = hasChange && Number(changeUsd) >= 0;
  const color = isUp ? UP_COLOR : DOWN_COLOR;
  const gradientId = `portfolio-history-fill-${isUp ? 'up' : 'down'}`;

  const renderBody = () => {
    if (loading) return <div className="portfolio-history-empty">{t('portfolio.history.loading')}</div>;
    if (error) return <div className="portfolio-history-empty">{t('portfolio.history.error')}</div>;
    if (points.length === 0) return <div className="portfolio-history-empty">{t('portfolio.history.empty')}</div>;
    if (points.length === 1) {
      return (
        <div className="portfolio-history-empty">
          <strong>{formatUsd(points[0].totalValue)}</strong>
          <span>{formatFullDate(points[0].takenAt, lang)}</span>
          <span>{t('portfolio.history.singlePoint')}</span>
        </div>
      );
    }

    return (
      <div className="portfolio-history-chart" role="img" aria-label={t('portfolio.history.chartLabel')}>
        <ResponsiveContainer width="100%" height={CHART_HEIGHT}>
          <AreaChart data={chartData} margin={{ top: 12, right: 12, bottom: 0, left: 8 }}>
            <defs>
              <linearGradient id={gradientId} x1="0" y1="0" x2="0" y2="1">
                <stop offset="0%" stopColor={color} stopOpacity={0.35} />
                <stop offset="100%" stopColor={color} stopOpacity={0.02} />
              </linearGradient>
            </defs>
            <CartesianGrid strokeDasharray="4 4" vertical={false} />
            <XAxis
              dataKey="time"
              type="number"
              scale="time"
              domain={['dataMin', 'dataMax']}
              tickFormatter={(value) => formatAxisDate(value, range, lang)}
              tickLine={false}
              axisLine={false}
              minTickGap={32}
            />
            <YAxis
              domain={yAxis.domain}
              ticks={yAxis.ticks}
              tickFormatter={(value) => formatUsd(value, yAxis.digits)}
              tickLine={false}
              axisLine={false}
              width={80}
            />
            <Tooltip
              content={<ChartTooltip lang={lang} />}
              cursor={{ strokeDasharray: '3 3' }}
            />
            <Area
              type="monotone"
              dataKey="totalValue"
              stroke={color}
              strokeWidth={2.5}
              fill={`url(#${gradientId})`}
              activeDot={{ r: 5, strokeWidth: 2.5 }}
            />
          </AreaChart>
        </ResponsiveContainer>
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
