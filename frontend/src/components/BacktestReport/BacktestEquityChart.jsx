import { useMemo } from 'react';
import { Area, AreaChart, CartesianGrid, ReferenceLine, ResponsiveContainer, Tooltip, XAxis, YAxis } from 'recharts';
import { useLanguage } from '../../context/LanguageContext';
import { buildYAxis } from '../PortfolioHistoryChart/PortfolioHistoryChart';

const UP_COLOR = '#16a34a';
const DOWN_COLOR = '#dc2626';
const DAY_MS = 24 * 60 * 60 * 1000;

const formatUsdt = (value, digits, locale) =>
  `${Number(value).toLocaleString(locale, { minimumFractionDigits: digits, maximumFractionDigits: digits })} USDT`;

function EquityTooltip({ active, payload, locale }) {
  if (!active || !payload || payload.length === 0) return null;
  const point = payload[0].payload;
  return (
    <div className="portfolio-history-tooltip">
      <strong>{formatUsdt(point.value, 2, locale)}</strong>
      <span>{new Date(point.time).toLocaleString(locale, { dateStyle: 'medium', timeStyle: 'short' })}</span>
    </div>
  );
}

export default function BacktestEquityChart({ points }) {
  const { t, lang } = useLanguage();
  const locale = lang === 'en' ? 'en-US' : 'tr-TR';

  const chartData = useMemo(
    () => points.map((p) => ({ time: new Date(p.timestamp).getTime(), value: Number(p.value) })),
    [points]
  );
  const yAxis = useMemo(
    () => (chartData.length > 1 ? buildYAxis(chartData.map((p) => p.value)) : null),
    [chartData]
  );

  if (!yAxis) return <div className="backtest-empty">{t('backtest.equity.empty')}</div>;

  const startValue = chartData[0].value;
  const isUp = chartData[chartData.length - 1].value >= startValue;
  const color = isUp ? UP_COLOR : DOWN_COLOR;
  const gradientId = `backtest-equity-fill-${isUp ? 'up' : 'down'}`;
  const rangeMs = chartData[chartData.length - 1].time - chartData[0].time;

  // Birkaç günlük aralıkta yalnızca tarih yazılırsa eksende aynı gün tekrar eder, saat de gerekir.
  const formatAxisTime = (value) => {
    const date = new Date(value);
    const time = date.toLocaleTimeString(locale, { hour: '2-digit', minute: '2-digit' });
    const day = date.toLocaleDateString(locale, { day: '2-digit', month: 'short' });
    if (rangeMs <= DAY_MS) return time;
    return rangeMs <= 14 * DAY_MS ? `${day} ${time}` : day;
  };

  return (
    <div className="backtest-equity portfolio-history-chart" role="img" aria-label={t('backtest.equity.title')}>
      <ResponsiveContainer width="100%" height={260}>
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
            tickFormatter={formatAxisTime}
            tickLine={false}
            axisLine={false}
            minTickGap={48}
          />
          <YAxis
            domain={yAxis.domain}
            ticks={yAxis.ticks}
            tickFormatter={(value) => Number(value).toLocaleString(locale, { maximumFractionDigits: yAxis.digits })}
            tickLine={false}
            axisLine={false}
            width={70}
          />
          <Tooltip content={<EquityTooltip locale={locale} />} cursor={{ strokeDasharray: '3 3' }} />
          <ReferenceLine y={startValue} stroke="#94a3b8" strokeDasharray="6 4" />
          <Area type="monotone" dataKey="value" stroke={color} strokeWidth={2.5} fill={`url(#${gradientId})`} />
        </AreaChart>
      </ResponsiveContainer>
    </div>
  );
}
