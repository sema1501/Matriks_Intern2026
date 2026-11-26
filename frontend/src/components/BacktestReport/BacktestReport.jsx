import React, { useEffect, useMemo, useRef, useState } from 'react';
import { dispose, init, registerOverlay } from 'klinecharts';
import { runBotBacktest } from '../../services/apiService';
import { getKlinesRange } from '../../services/binanceService';
import { useLanguage } from '../../context/LanguageContext';
import { normalizeReason, reasonLabelKey } from '../../utils/botSignalReason';
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

const formatDateTime = (value, locale = 'tr-TR') => {
    if (!value) return '-';
    return new Intl.DateTimeFormat(locale, {
        dateStyle: 'short',
        timeStyle: 'short'
    }).format(new Date(value));
};

const formatNumber = (value, digits = 2, locale = 'tr-TR') => {
    const number = Number(value);
    if (!Number.isFinite(number)) return '-';
    return number.toLocaleString(locale, {
        minimumFractionDigits: 0,
        maximumFractionDigits: digits
    });
};

const getErrorMessage = (error, fallback) => {
    const data = error?.response?.data;
    if (typeof data === 'string') return data;
    return data?.message || data?.error || error?.message || fallback;
};

export default function BacktestReport({ bot, onClose }) {
    const { t, locale } = useLanguage();
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
            setError(t('backtest.selectDates'));
            return;
        }

        const start = new Date(`${startDate}T00:00:00`);
        const end = new Date(`${endDate}T23:59:59`);

        if (start >= end) {
            setError(t('backtest.rangeInvalid'));
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
            setError(getErrorMessage(err, t('backtest.runError')));
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
                        <p className="backtest-eyebrow">{t('backtest.eyebrow')}</p>
                        <h2 id="backtest-title">{t('backtest.title', { symbol: bot.symbol })}</h2>
                        <p>
                            {bot.strategy === 1
                                ? `EMA${bot.shortEmaPeriod} ↑ EMA${bot.longEmaPeriod} ${t('signal.buy')} · EMA${bot.shortEmaPeriod} ↓ EMA${bot.longEmaPeriod} ${t('signal.sell')} · ${t('bot.amount')} ${bot.tradeQuantity}`
                                : `RSI ≤ ${bot.buyRsiThreshold} ${t('signal.buy')} · RSI ≥ ${bot.sellRsiThreshold} ${t('signal.sell')} · ${t('bot.amount')} ${bot.tradeQuantity}`}
                        </p>
                    </div>
                    <button className="backtest-close" type="button" onClick={onClose} aria-label={t('common.close')}>×</button>
                </div>

                <div className="backtest-controls">
                    <label>
                        {t('common.startDate')}
                        <input
                            type="date"
                            value={startDate}
                            max={endDate}
                            onChange={(event) => setStartDate(event.target.value)}
                        />
                    </label>
                    <label>
                        {t('common.endDate')}
                        <input
                            type="date"
                            value={endDate}
                            min={startDate}
                            max={toDateInput(new Date())}
                            onChange={(event) => setEndDate(event.target.value)}
                        />
                    </label>
                    <button type="button" onClick={handleRunBacktest} disabled={loading}>
                        {loading ? t('backtest.running') : t('backtest.run')}
                    </button>
                </div>

                {error && <div className="backtest-error">{error}</div>}

                {!result && !loading && !error && (
                    <div className="backtest-placeholder">
                        {t('backtest.placeholder')}
                    </div>
                )}

                {loading && (
                    <div className="backtest-placeholder">{t('backtest.analyzing')}</div>
                )}

                {result && (
                    <>
                        <div className="backtest-summary-grid">
                            <div><span>{t('backtest.totalSignals')}</span><strong>{result.summary?.totalSignals ?? 0}</strong></div>
                            <div><span>{t('signal.buy')} / {t('signal.sell')}</span><strong>{result.summary?.buySignals ?? 0} / {result.summary?.sellSignals ?? 0}</strong></div>
                            <div><span>{t('backtest.completedTrades')}</span><strong>{result.summary?.completedTrades ?? 0}</strong></div>
                            <div>
                                <span>{t('backtest.netPnl')}</span>
                                <strong className={Number(result.summary?.netProfitLoss) >= 0 ? 'positive' : 'negative'}>
                                    {formatNumber(result.summary?.netProfitLoss, 6, locale)} USDT
                                </strong>
                            </div>
                        </div>

                        {candles.length > 0 ? (
                            <div className="backtest-chart-card">
                                <div className="backtest-chart-legend">
                                    <span><i className="buy-dot" /> {t('backtest.legend.buy')}</span>
                                    <span><i className="sell-dot" /> {t('backtest.legend.sell')}</span>
                                    <span>{t('backtest.legend.rsi')}</span>
                                </div>
                                <div ref={chartContainerRef} className="backtest-chart" />
                            </div>
                        ) : (
                            <div className="backtest-empty">{t('backtest.noCandles')}</div>
                        )}

                        <div className="backtest-table-card">
                            <div className="backtest-table-heading">
                                <div>
                                    <h3>{t('backtest.signalDetails')}</h3>
                                    <p>{t('backtest.signalDetailsHint')}</p>
                                </div>
                                <span>{t('backtest.signalCount', { count: sortedSignals.length })}</span>
                            </div>

                            {sortedSignals.length === 0 ? (
                                <div className="backtest-empty">
                                    {t('backtest.noSignals')}
                                </div>
                            ) : (
                                <div className="backtest-table-scroll">
                                    <table className="backtest-table">
                                        <thead>
                                            <tr>
                                                <th>{t('dashboard.date')}</th>
                                                <th>{t('backtest.col.side')}</th>
                                                <th>{t('signal.price')}</th>
                                                <th>{t('backtest.col.rsi')}</th>
                                                <th>{t('bot.reasonColumn')}</th>
                                            </tr>
                                        </thead>
                                        <tbody>
                                            {sortedSignals.map((signal, index) => {
                                                const isBuy = String(signal.type).toUpperCase() === 'BUY';
                                                const reason = normalizeReason(signal.reason);
                                                return (
                                                    <tr key={`${signal.timestamp}-${index}`}>
                                                        <td>{formatDateTime(signal.timestamp, locale)}</td>
                                                        <td>
                                                            <span className={`backtest-signal-badge ${isBuy ? 'buy' : 'sell'}`}>
                                                                {isBuy ? `↑ ${t('backtest.side.buy')}` : `↓ ${t('backtest.side.sell')}`}
                                                            </span>
                                                        </td>
                                                        <td>{formatNumber(signal.price, 8, locale)} USDT</td>
                                                        <td>{signal.rsi == null ? '-' : formatNumber(signal.rsi, 2, locale)}</td>
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
