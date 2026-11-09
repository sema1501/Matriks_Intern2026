import React, { useEffect, useMemo, useRef, useState } from 'react';
import { dispose, init, registerOverlay } from 'klinecharts';
import { runBotBacktest } from '../../services/apiService';
import { getKlinesRange } from '../../services/binanceService';
import { useLanguage } from '../../context/LanguageContext';
import { normalizeReason, reasonLabelKey } from '../../utils/botSignalReason';
import BacktestEquityChart from './BacktestEquityChart';
import './BacktestReport.css';

let overlaysRegistered = false;

function registerBacktestOverlays() {
    if (overlaysRegistered) return;

    const buildOverlay = (name, color, direction, label) => ({
        name,
        totalStep: 2,
        lock: true,
        needDefaultPointFigure: false,
        needDefaultXAxisFigure: false,
        needDefaultYAxisFigure: false,
        createPointFigures: ({ coordinates }) => {
            if (!coordinates || coordinates.length === 0) return [];
            const point = coordinates[0];
            const up = direction === 'up';
            const tipY = point.y + (up ? 3 : -3);
            const baseY = point.y + (up ? 15 : -15);
            const textY = point.y + (up ? 20 : -32);

            return [
                {
                    key: `${name}-arrow`,
                    type: 'polygon',
                    attrs: {
                        coordinates: [
                            { x: point.x, y: tipY },
                            { x: point.x - 6, y: baseY },
                            { x: point.x + 6, y: baseY }
                        ]
                    },
                    styles: { style: 'fill', color },
                    ignoreEvent: true
                },
                {
                    key: `${name}-label`,
                    type: 'text',
                    attrs: {
                        x: point.x,
                        y: textY,
                        width: 36,
                        height: 18,
                        text: label,
                        align: 'center',
                        baseline: 'middle'
                    },
                    styles: {
                        style: 'fill',
                        color,
                        size: 11,
                        weight: '600'
                    },
                    ignoreEvent: true
                }
            ];
        }
    });

    registerOverlay(buildOverlay('backtestBuySignal', '#16a34a', 'up', 'AL'));
    registerOverlay(buildOverlay('backtestSellSignal', '#dc2626', 'down', 'SAT'));
    overlaysRegistered = true;
}

registerBacktestOverlays();

const toDateInput = (date) => {
    const year = date.getFullYear();
    const month = String(date.getMonth() + 1).padStart(2, '0');
    const day = String(date.getDate()).padStart(2, '0');
    return `${year}-${month}-${day}`;
};

const formatDateTime = (value) => {
    if (!value) return '-';
    return new Intl.DateTimeFormat('tr-TR', {
        dateStyle: 'short',
        timeStyle: 'short'
    }).format(new Date(value));
};

const formatNumber = (value, digits = 2) => {
    const number = Number(value);
    if (!Number.isFinite(number)) return '-';
    return number.toLocaleString('tr-TR', {
        minimumFractionDigits: 0,
        maximumFractionDigits: digits
    });
};

const EMPTY_METRIC = '—';

// null, NaN ve Infinity ekrana hiçbir zaman çıkmaz. Number(null) 0 olduğu için null önce kontrol edilir.
const isMetricNumber = (value) => value !== null && value !== undefined && Number.isFinite(Number(value));

const metricNumber = (value, lang) =>
    isMetricNumber(value)
        ? Number(value).toLocaleString(lang === 'en' ? 'en-US' : 'tr-TR', { maximumFractionDigits: 2 })
        : EMPTY_METRIC;

const metricPercent = (value, lang) => {
    if (!isMetricNumber(value)) return EMPTY_METRIC;
    const text = metricNumber(Math.abs(Number(value)), lang);
    const sign = Number(value) < 0 ? '-' : '';
    return lang === 'en' ? `${sign}${text}%` : `${sign}%${text}`;
};

function MetricCard({ label, info, valueClassName, value, children }) {
    return (
        <div>
            <span className="backtest-metric-label">
                {label}
                <span className="backtest-metric-info" role="img" tabIndex={0} title={info} aria-label={info}>ⓘ</span>
            </span>
            <strong className={valueClassName}>{value}</strong>
            {children}
        </div>
    );
}

function BacktestMetricCards({ metrics }) {
    const { t, lang } = useLanguage();
    const difference = metrics.buyAndHoldDifferencePercent;
    const hasDifference = isMetricNumber(difference);
    const beatsBuyHold = hasDifference && Number(difference) > 0;

    return (
        <div className="backtest-summary-grid backtest-metric-grid">
            <MetricCard
                label={t('backtest.metric.winRate')}
                info={t('backtest.metric.winRate.info')}
                value={metricPercent(metrics.winRatePercent, lang)}
            />
            <MetricCard
                label={t('backtest.metric.maxDrawdown')}
                info={t('backtest.metric.maxDrawdown.info')}
                valueClassName={isMetricNumber(metrics.maxDrawdownPercent) ? 'negative' : undefined}
                value={metricPercent(metrics.maxDrawdownPercent, lang)}
            />
            <MetricCard
                label={t('backtest.metric.profitFactor')}
                info={t('backtest.metric.profitFactor.info')}
                value={metricNumber(metrics.profitFactor, lang)}
            />
            <MetricCard
                label={t('backtest.metric.buyHold')}
                info={t('backtest.metric.buyHold.info')}
                valueClassName={hasDifference ? (beatsBuyHold ? 'positive' : 'negative') : undefined}
                value={metricPercent(difference, lang)}
            >
                {hasDifference && (
                    <small className={`backtest-buyhold-badge ${beatsBuyHold ? 'positive' : 'negative'}`}>
                        {t(beatsBuyHold ? 'backtest.metric.buyHold.beat' : 'backtest.metric.buyHold.behind')}
                    </small>
                )}
                <small className="backtest-metric-note">
                    {t('backtest.metric.buyHold.return')}: {metricPercent(metrics.buyAndHoldReturnPercent, lang)}
                </small>
            </MetricCard>
            <MetricCard
                label={t('backtest.metric.avgTrade')}
                info={t('backtest.metric.avgTrade.info')}
                value={metricPercent(metrics.averageTradeReturnPercent, lang)}
            />
            <MetricCard
                label={t('backtest.metric.riskExits')}
                info={t('backtest.metric.riskExits.info')}
                value={`${metrics.stopLossTrades ?? 0} / ${metrics.takeProfitTrades ?? 0}`}
            />
        </div>
    );
}

function RiskLimitComparison({ withLimits, withoutLimits }) {
    const { t, lang } = useLanguage();
    const rows = [
        ['backtest.metric.strategyReturn', (m) => metricPercent(m.strategyReturnPercent, lang)],
        ['backtest.metric.maxDrawdown', (m) => metricPercent(m.maxDrawdownPercent, lang)],
        ['backtest.metric.winRate', (m) => metricPercent(m.winRatePercent, lang)],
        ['backtest.metric.profitFactor', (m) => metricNumber(m.profitFactor, lang)],
        ['backtest.metric.avgTrade', (m) => metricPercent(m.averageTradeReturnPercent, lang)],
    ];

    return (
        <div className="backtest-table-card">
            <div className="backtest-table-heading">
                <div>
                    <h3>{t('backtest.compare.title')}</h3>
                    <p>{t('backtest.compare.subtitle')}</p>
                </div>
            </div>
            <div className="backtest-table-scroll">
                <table className="backtest-table">
                    <thead>
                        <tr>
                            <th>{t('backtest.compare.metric')}</th>
                            <th>{t('backtest.compare.withLimits')}</th>
                            <th>{t('backtest.compare.withoutLimits')}</th>
                        </tr>
                    </thead>
                    <tbody>
                        {rows.map(([labelKey, read]) => (
                            <tr key={labelKey}>
                                <td>{t(labelKey)}</td>
                                <td>{read(withLimits)}</td>
                                <td>{read(withoutLimits)}</td>
                            </tr>
                        ))}
                    </tbody>
                </table>
            </div>
        </div>
    );
}

const getErrorMessage = (error) => {
    const data = error?.response?.data;
    if (typeof data === 'string') return data;
    return data?.message || data?.error || error?.message || 'Backtest çalıştırılırken bir hata oluştu.';
};

export default function BacktestReport({ bot, onClose }) {
    const { t } = useLanguage();
    const chartContainerRef = useRef(null);
    const chartRef = useRef(null);
    const [startDate, setStartDate] = useState(() => {
        const date = new Date();
        date.setDate(date.getDate() - 1);
        return toDateInput(date);
    });
    const [endDate, setEndDate] = useState(() => toDateInput(new Date()));
    const [result, setResult] = useState(null);
    const [candles, setCandles] = useState([]);
    const [loading, setLoading] = useState(false);
    const [error, setError] = useState('');

    const sortedSignals = useMemo(() => {
        if (!result?.signals) return [];
        return [...result.signals].sort(
            (a, b) => new Date(a.timestamp).getTime() - new Date(b.timestamp).getTime()
        );
    }, [result]);

    const handleRunBacktest = async () => {
        setError('');
        setResult(null);
        setCandles([]);

        if (!startDate || !endDate) {
            setError('Başlangıç ve bitiş tarihini seçin.');
            return;
        }

        const start = new Date(`${startDate}T00:00:00`);
        const end = new Date(`${endDate}T23:59:59`);

        if (start >= end) {
            setError('Başlangıç tarihi bitiş tarihinden önce olmalıdır.');
            return;
        }

        try {
            setLoading(true);
            const response = await runBotBacktest(bot.id, {
                startDate: start.toISOString(),
                endDate: end.toISOString()
            });
            const report = response?.data;
            setResult(report);

            const historicalCandles = await getKlinesRange(
                report?.symbol || bot.symbol,
                report?.interval || '1m',
                start.getTime(),
                end.getTime()
            );
            setCandles(historicalCandles);
        } catch (err) {
            console.error('Backtest çalıştırılamadı:', err);
            setError(getErrorMessage(err));
        } finally {
            setLoading(false);
        }
    };

    useEffect(() => {
        if (!result || candles.length === 0 || !chartContainerRef.current) return undefined;

        if (chartRef.current) {
            dispose(chartRef.current);
            chartRef.current = null;
        }

        const chart = init(chartContainerRef.current, {
            timezone: 'Europe/Istanbul'
        });
        chartRef.current = chart;

        chart.setStyles({
            grid: {
                horizontal: { color: 'rgba(148, 163, 184, 0.16)' },
                vertical: { color: 'rgba(148, 163, 184, 0.10)' }
            },
            candle: {
                bar: {
                    upColor: '#16a34a',
                    downColor: '#dc2626',
                    noChangeColor: '#64748b'
                }
            }
        });

        chart.applyNewData(candles);
        chart.createIndicator(
            { name: 'RSI', calcParams: [14] },
            false,
            {
                id: 'backtest_rsi_pane',
                height: 150,
                minHeight: 110,
                dragEnabled: true,
                gap: { top: 0.08, bottom: 0.08 }
            }
        );

        sortedSignals.forEach((signal, index) => {
            const isBuy = String(signal.type).toUpperCase() === 'BUY';
            chart.createOverlay(
                {
                    name: isBuy ? 'backtestBuySignal' : 'backtestSellSignal',
                    id: `backtest-signal-${index}`,
                    groupId: 'backtest-signals',
                    lock: true,
                    points: [{
                        timestamp: new Date(signal.timestamp).getTime(),
                        value: Number(signal.price)
                    }]
                },
                'candle_pane'
            );
        });

        return () => {
            if (chartRef.current) {
                dispose(chartRef.current);
                chartRef.current = null;
            }
        };
    }, [result, candles, sortedSignals]);

    useEffect(() => () => {
        if (chartRef.current) {
            dispose(chartRef.current);
            chartRef.current = null;
        }
    }, []);

    return (
        <div className="backtest-modal-backdrop" role="presentation" onMouseDown={onClose}>
            <section
                className="backtest-modal"
                role="dialog"
                aria-modal="true"
                aria-labelledby="backtest-title"
                onMouseDown={(event) => event.stopPropagation()}
            >
                <div className="backtest-header">
                    <div>
                        <p className="backtest-eyebrow">Geçmiş strateji simülasyonu</p>
                        <h2 id="backtest-title">{bot.symbol} Backtest Raporu</h2>
                        <p>
                            {bot.strategy === 1
                                ? `EMA${bot.shortEmaPeriod} ↑ EMA${bot.longEmaPeriod} AL · EMA${bot.shortEmaPeriod} ↓ EMA${bot.longEmaPeriod} SAT · Miktar ${bot.tradeQuantity}`
                                : `RSI ≤ ${bot.buyRsiThreshold} AL · RSI ≥ ${bot.sellRsiThreshold} SAT · Miktar ${bot.tradeQuantity}`}
                        </p>
                    </div>
                    <button className="backtest-close" type="button" onClick={onClose} aria-label="Kapat">×</button>
                </div>

                <div className="backtest-controls">
                    <label>
                        Başlangıç
                        <input
                            type="date"
                            value={startDate}
                            max={endDate}
                            onChange={(event) => setStartDate(event.target.value)}
                        />
                    </label>
                    <label>
                        Bitiş
                        <input
                            type="date"
                            value={endDate}
                            min={startDate}
                            max={toDateInput(new Date())}
                            onChange={(event) => setEndDate(event.target.value)}
                        />
                    </label>
                    <button type="button" onClick={handleRunBacktest} disabled={loading}>
                        {loading ? 'Hesaplanıyor...' : 'Backtest Çalıştır'}
                    </button>
                </div>

                {error && <div className="backtest-error">{error}</div>}

                {!result && !loading && !error && (
                    <div className="backtest-placeholder">
                        Tarih aralığını seçip backtest'i çalıştırın. Gerçek/Testnet emri gönderilmez.
                    </div>
                )}

                {loading && (
                    <div className="backtest-placeholder">Geçmiş veriler analiz ediliyor...</div>
                )}

                {result && (
                    <>
                        <div className="backtest-summary-grid">
                            <div><span>Toplam Sinyal</span><strong>{result.summary?.totalSignals ?? 0}</strong></div>
                            <div><span>AL / SAT</span><strong>{result.summary?.buySignals ?? 0} / {result.summary?.sellSignals ?? 0}</strong></div>
                            <div><span>Tamamlanan İşlem</span><strong>{result.summary?.completedTrades ?? 0}</strong></div>
                            <div>
                                <span>Net Kâr / Zarar</span>
                                <strong className={Number(result.summary?.netProfitLoss) >= 0 ? 'positive' : 'negative'}>
                                    {formatNumber(result.summary?.netProfitLoss, 6)} USDT
                                </strong>
                            </div>
                        </div>

                        {result.metrics && (
                            <>
                                <BacktestMetricCards metrics={result.metrics} />

                                <div className="backtest-table-card">
                                    <div className="backtest-table-heading">
                                        <div>
                                            <h3>{t('backtest.equity.title')}</h3>
                                            <p>{t('backtest.equity.subtitle')}</p>
                                        </div>
                                    </div>
                                    <BacktestEquityChart points={result.metrics.equityCurve ?? []} />
                                </div>

                                {result.metricsWithoutRiskLimits && (
                                    <RiskLimitComparison
                                        withLimits={result.metrics}
                                        withoutLimits={result.metricsWithoutRiskLimits}
                                    />
                                )}
                            </>
                        )}

                        {candles.length > 0 ? (
                            <div className="backtest-chart-card">
                                <div className="backtest-chart-legend">
                                    <span><i className="buy-dot" /> Yeşil ok: Alış</span>
                                    <span><i className="sell-dot" /> Kırmızı ok: Satış</span>
                                    <span>Alt panel: RSI (14)</span>
                                </div>
                                <div ref={chartContainerRef} className="backtest-chart" />
                            </div>
                        ) : (
                            <div className="backtest-empty">Grafik için geçmiş mum verisi alınamadı.</div>
                        )}

                        <div className="backtest-table-card">
                            <div className="backtest-table-heading">
                                <div>
                                    <h3>Sinyal Detayları</h3>
                                    <p>Grafikteki sinyallerle aynı kayıtlar, kronolojik sırada.</p>
                                </div>
                                <span>{sortedSignals.length} sinyal</span>
                            </div>

                            {sortedSignals.length === 0 ? (
                                <div className="backtest-empty">
                                    Bu tarih aralığında bot stratejisi herhangi bir AL/SAT sinyali üretmedi.
                                </div>
                            ) : (
                                <div className="backtest-table-scroll">
                                    <table className="backtest-table">
                                        <thead>
                                            <tr>
                                                <th>Tarih</th>
                                                <th>Alış / Satış</th>
                                                <th>Fiyat</th>
                                                <th>RSI Değeri</th>
                                                <th>{t('bot.reasonColumn')}</th>
                                            </tr>
                                        </thead>
                                        <tbody>
                                            {sortedSignals.map((signal, index) => {
                                                const isBuy = String(signal.type).toUpperCase() === 'BUY';
                                                const reason = normalizeReason(signal.reason);
                                                return (
                                                    <tr key={`${signal.timestamp}-${index}`}>
                                                        <td>{formatDateTime(signal.timestamp)}</td>
                                                        <td>
                                                            <span className={`backtest-signal-badge ${isBuy ? 'buy' : 'sell'}`}>
                                                                {isBuy ? '↑ ALIŞ' : '↓ SATIŞ'}
                                                            </span>
                                                        </td>
                                                        <td>{formatNumber(signal.price, 8)} USDT</td>
                                                        <td>{signal.rsi == null ? '-' : formatNumber(signal.rsi, 2)}</td>
                                                        <td>
                                                            <span className={`backtest-reason-badge ${reason}`}>
                                                                {t(reasonLabelKey(reason))}
                                                            </span>
                                                        </td>
                                                    </tr>
                                                );
                                            })}
                                        </tbody>
                                    </table>
                                </div>
                            )}
                        </div>
                    </>
                )}
            </section>
        </div>
    );
}
