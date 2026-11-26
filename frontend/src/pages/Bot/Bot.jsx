import React, { useState, useEffect } from 'react';
import { getBots, createBot, toggleBot, deleteBot } from '../../services/apiService';
import BotSignalApproval from '../../components/BotSignalApproval/BotSignalApproval';
import BacktestReport from '../../components/BacktestReport/BacktestReport';
import { useLanguage } from '../../context/LanguageContext';

const ALL_SYMBOLS = [
    'BTCUSDT', 'ETHUSDT', 'BNBUSDT', 'SOLUSDT', 'LTCUSDT',
    'LINKUSDT', 'ETCUSDT', 'AVAXUSDT', 'UNIUSDT', 'NEARUSDT',
    'ATOMUSDT', 'XRPUSDT', 'DOTUSDT', 'FILUSDT', 'APTUSDT',
    'TRXUSDT', 'ADAUSDT', 'XLMUSDT', 'POLUSDT', 'DOGEUSDT',
    'SHIBUSDT', 'PEPEUSDT', 'SUIUSDT', 'ARBUSDT', 'OPUSDT'
];

// Backend enum degerleri: BotStrategy.RsiThreshold = 0, BotStrategy.EmaCrossover = 1
const STRATEGY_RSI = 0;
const STRATEGY_EMA = 1;

export default function Bot() {
    const { t, locale } = useLanguage();
    const [bots, setBots] = useState([]);
    const [loading, setLoading] = useState(true);
    const [submitting, setSubmitting] = useState(false);
    const [error, setError] = useState(null);
    const [formError, setFormError] = useState(null);
    const [backtestBot, setBacktestBot] = useState(null);

    const [symbol, setSymbol] = useState('BTCUSDT');
    const [strategy, setStrategy] = useState(STRATEGY_RSI);
    const [shortEmaPeriod, setShortEmaPeriod] = useState(12); // #81: issue varsayılanı EMA12
    const [longEmaPeriod, setLongEmaPeriod] = useState(26); // #81: issue varsayılanı EMA26
    const [buyRsiThreshold, setBuyRsiThreshold] = useState(30);
    const [sellRsiThreshold, setSellRsiThreshold] = useState(70);
    const [tradeQuantity, setTradeQuantity] = useState(0.01);
    // Boş bırakılırsa bot yalnızca stratejiye göre satar.
    const [stopLossPercent, setStopLossPercent] = useState('');
    const [takeProfitPercent, setTakeProfitPercent] = useState('');

    const fetchBots = async () => {
        try {
            setError(null);
            const response = await getBots();
            setBots(response?.data || []);
        } catch (err) {
            console.error("Botlar yüklenemedi:", err);
            setError(t('bot.loadError'));
        } finally {
            setLoading(false);
        }
    };

    useEffect(() => {
        fetchBots();
    }, []);

    const handleCreateBot = async (e) => {
        e.preventDefault();
        setFormError(null);

        // Doğrulama stratejiye göre değişir.
        if (strategy === STRATEGY_RSI) {
            if (Number(buyRsiThreshold) >= Number(sellRsiThreshold)) {
                setFormError(t('bot.rsiOrderInvalid'));
                return;
            }
        } else {
            if (Number(shortEmaPeriod) >= Number(longEmaPeriod)) {
                setFormError(t('bot.emaOrderInvalid'));
                return;
            }
            if (Number(shortEmaPeriod) < 1 || Number(longEmaPeriod) < 2) {
                setFormError(t('bot.emaInvalid'));
                return;
            }
        }

        if (Number(tradeQuantity) <= 0) {
            setFormError(t('bot.quantityInvalid'));
            return;
        }

        if (stopLossPercent !== '' && (Number(stopLossPercent) <= 0 || Number(stopLossPercent) >= 100)) {
            setFormError(t('bot.stopLossInvalid'));
            return;
        }

        if (takeProfitPercent !== '' && Number(takeProfitPercent) <= 0) {
            setFormError(t('bot.takeProfitInvalid'));
            return;
        }

        try {
            setSubmitting(true);

            const payload = {
                symbol: symbol.toUpperCase(),
                strategy,
                tradeQuantity: parseFloat(tradeQuantity),
                buyRsiThreshold: parseInt(buyRsiThreshold, 10),
                sellRsiThreshold: parseInt(sellRsiThreshold, 10)
            };

            // EMA parametreleri yalnızca ilgili stratejide gönderilir.
            if (strategy === STRATEGY_EMA) {
                payload.shortEmaPeriod = parseInt(shortEmaPeriod, 10);
                payload.longEmaPeriod = parseInt(longEmaPeriod, 10);
            }

            if (stopLossPercent !== '') payload.stopLossPercent = parseFloat(stopLossPercent);
            if (takeProfitPercent !== '') payload.takeProfitPercent = parseFloat(takeProfitPercent);

            await createBot(payload);

            setFormError(null);
            fetchBots();
        } catch (err) {
            console.error("Bot oluşturulamadı:", err);
            setFormError(err.response?.data?.error || err.response?.data?.message || t('bot.createError'));
        } finally {
            setSubmitting(false);
        }
    };

    const handleToggleBot = async (id) => {
        try {
            await toggleBot(id);
            setBots(prevBots => prevBots.map(bot =>
                bot.id === id ? { ...bot, isActive: !bot.isActive } : bot
            ));
        } catch (err) {
            console.error("Bot durumu değiştirilemedi:", err);
            alert(t('bot.toggleError'));
        }
    };

    // BOT SILME / KALDIRMA FONKSIYONU
    const handleDeleteBot = async (id) => {
        if (!window.confirm(t('bot.removeConfirm'))) return;

        try {
            await deleteBot(id);
            // Silinen botu state'ten kaldırarak arayüzün anında güncellenmesini sağlıyoruz
            setBots(prevBots => prevBots.filter(bot => bot.id !== id));
        } catch (err) {
            console.error("Bot silinemedi:", err);
            alert(t('bot.removeError'));
        }
    };

    return (
        <div style={styles.container}>
            <h2 style={{ fontSize: '28px', fontWeight: '700', marginBottom: '8px' }}>{t('bot.title')}</h2>
            <p style={{ color: '#888', marginBottom: '24px', fontSize: '14px' }}>
                {t('bot.subtitle')}
            </p>

            {/* BOT KURULUM FORMU */}
            <div style={styles.card}>
                <h3 style={{ marginTop: 0, marginBottom: '16px' }}>{t('bot.newBot')}</h3>

                {formError && (
                    <div style={styles.errorBanner}>{formError}</div>
                )}

                <form onSubmit={handleCreateBot} style={styles.formGrid}>
                    <div style={styles.formGroup}>
                        <label style={styles.label}>{t('bot.selectSymbol')}</label>
                        <select
                            value={symbol}
                            onChange={(e) => setSymbol(e.target.value)}
                            style={styles.select}
                        >
                            {ALL_SYMBOLS.map((sym) => (
                                <option key={sym} value={sym} style={styles.option}>
                                    {sym}
                                </option>
                            ))}
                        </select>
                    </div>

                    <div style={styles.formGroup}>
                        <label style={styles.label}>{t('bot.strategy')}</label>
                        <select
                            value={strategy}
                            onChange={(e) => setStrategy(Number(e.target.value))}
                            style={styles.select}
                        >
                            <option value={STRATEGY_RSI} style={styles.option}>{t('bot.strategy.rsi')}</option>
                            <option value={STRATEGY_EMA} style={styles.option}>{t('bot.strategy.ema')}</option>
                        </select>
                    </div>

                    {/* Yalnızca seçili stratejinin parametreleri gösterilir. */}
                    {strategy === STRATEGY_RSI && (
                        <>
                            <div style={styles.formGroup}>
                                <label style={styles.label}>{t('bot.buyRsi')}</label>
                                <input
                                    type="number"
                                    value={buyRsiThreshold}
                                    onChange={(e) => setBuyRsiThreshold(e.target.value)}
                                    min="1"
                                    max="99"
                                    required
                                    style={styles.input}
                                />
                            </div>

                            <div style={styles.formGroup}>
                                <label style={styles.label}>{t('bot.sellRsi')}</label>
                                <input
                                    type="number"
                                    value={sellRsiThreshold}
                                    onChange={(e) => setSellRsiThreshold(e.target.value)}
                                    min="1"
                                    max="99"
                                    required
                                    style={styles.input}
                                />
                            </div>
                        </>
                    )}

                    {strategy === STRATEGY_EMA && (
                        <>
                            <div style={styles.formGroup}>
                                <label style={styles.label}>{t('bot.shortEmaPeriod')}</label>
                                <input
                                    type="number"
                                    value={shortEmaPeriod}
                                    onChange={(e) => setShortEmaPeriod(e.target.value)}
                                    min="1"
                                    max="200"
                                    required
                                    style={styles.input}
                                />
                            </div>

                            <div style={styles.formGroup}>
                                <label style={styles.label}>{t('bot.longEmaPeriod')}</label>
                                <input
                                    type="number"
                                    value={longEmaPeriod}
                                    onChange={(e) => setLongEmaPeriod(e.target.value)}
                                    min="2"
                                    max="400"
                                    required
                                    style={styles.input}
                                />
                            </div>
                        </>
                    )}

                    <div style={styles.formGroup}>
                        <label style={styles.label}>{t('bot.quantity')}</label>
                        <input
                            type="number"
                            step="any"
                            value={tradeQuantity}
                            onChange={(e) => setTradeQuantity(e.target.value)}
                            required
                            style={styles.input}
                        />
                    </div>

                    <div style={styles.formGroup}>
                        <label style={styles.label}>{t('bot.stopLoss')}</label>
                        <input
                            type="number"
                            step="any"
                            min="0"
                            max="99.99"
                            value={stopLossPercent}
                            onChange={(e) => setStopLossPercent(e.target.value)}
                            placeholder={t('bot.optionalPlaceholder')}
                            style={styles.input}
                        />
                    </div>

                    <div style={styles.formGroup}>
                        <label style={styles.label}>{t('bot.takeProfit')}</label>
                        <input
                            type="number"
                            step="any"
                            min="0"
                            value={takeProfitPercent}
                            onChange={(e) => setTakeProfitPercent(e.target.value)}
                            placeholder={t('bot.optionalPlaceholder')}
                            style={styles.input}
                        />
                    </div>

                    <p style={styles.hint}>{t('bot.riskHint')}</p>

                    <div style={{ gridColumn: '1 / -1', marginTop: '12px' }}>
                        <button type="submit" disabled={submitting} style={styles.submitBtn}>
                            {submitting ? t('bot.creating') : `🤖 ${t('bot.create')}`}
                        </button>
                    </div>
                </form>
            </div>

            {/* BOT LISTESI TABLOSU */}
            <div style={{ ...styles.card, marginTop: '32px' }}>
                <h3 style={{ marginTop: 0, marginBottom: '16px' }}>{t('bot.myBots')}</h3>

                {loading ? (
                    <p style={{ color: '#888' }}>{t('bot.loading')}</p>
                ) : error ? (
                    <p style={{ color: '#ef233c' }}>{error}</p>
                ) : bots.length === 0 ? (
                    <p style={{ color: '#888' }}>{t('bot.empty')}</p>
                ) : (
                    <div style={styles.tableContainer}>
                        <table style={styles.table}>
                            <thead>
                                <tr style={styles.thRow}>
                                    <th style={styles.th}>{t('bot.symbol')}</th>
                                    <th style={styles.th}>{t('bot.strategy')}</th>
                                    <th style={styles.th}>{t('bot.param1')}</th>
                                    <th style={styles.th}>{t('bot.param2')}</th>
                                    <th style={styles.th}>{t('bot.amount')}</th>
                                    <th style={styles.th}>{t('bot.riskLimits')}</th>
                                    <th style={styles.th}>{t('bot.status')}</th>
                                    <th style={styles.th}>{t('bot.action')}</th>
                                </tr>
                            </thead>
                            <tbody>
                                {bots.map((bot) => (
                                    <tr key={bot.id} style={styles.tr}>
                                        <td style={{ ...styles.td, fontWeight: 'bold' }}>{bot.symbol}</td>
                                        <td style={styles.td}>
                                            {bot.strategy === STRATEGY_EMA ? t('bot.strategy.ema') : t('bot.strategy.rsi')}
                                        </td>
                                        <td style={{ ...styles.td, color: '#00b4d8' }}>
                                            {bot.strategy === STRATEGY_EMA
                                                ? `${t('bot.shortEma')}: ${bot.shortEmaPeriod}`
                                                : `${t('signal.buy')} ≤ ${bot.buyRsiThreshold}`}
                                        </td>
                                        <td style={{ ...styles.td, color: '#ef233c' }}>
                                            {bot.strategy === STRATEGY_EMA
                                                ? `${t('bot.longEma')}: ${bot.longEmaPeriod}`
                                                : `${t('signal.sell')} ≥ ${bot.sellRsiThreshold}`}
                                        </td>
                                        <td style={styles.td}>{Number(bot.tradeQuantity).toLocaleString(locale, { maximumFractionDigits: 8 })}</td>
                                        <td style={styles.td}>
                                            {bot.stopLossPercent == null && bot.takeProfitPercent == null ? (
                                                <span style={{ color: '#888' }}>{t('bot.noRiskLimits')}</span>
                                            ) : (
                                                <div style={{ display: 'flex', flexDirection: 'column', gap: '2px', fontSize: '13px' }}>
                                                    {bot.stopLossPercent != null && (
                                                        <span style={{ color: '#ef233c' }}>SL: %{Number(bot.stopLossPercent)}</span>
                                                    )}
                                                    {bot.takeProfitPercent != null && (
                                                        <span style={{ color: '#2b9348' }}>TP: %{Number(bot.takeProfitPercent)}</span>
                                                    )}
                                                </div>
                                            )}
                                        </td>
                                        <td style={styles.td}>
                                            <span style={{
                                                padding: '4px 10px',
                                                borderRadius: '12px',
                                                fontSize: '12px',
                                                fontWeight: 'bold',
                                                backgroundColor: bot.isActive ? 'rgba(0, 180, 216, 0.15)' : 'rgba(255, 255, 255, 0.1)',
                                                color: bot.isActive ? '#00b4d8' : '#888'
                                            }}>
                                                {bot.isActive ? `● ${t('common.active')}` : `○ ${t('common.passive')}`}
                                            </span>
                                        </td>
                                        <td style={{ ...styles.td, display: 'flex', gap: '8px', alignItems: 'center' }}>
                                            <button
                                                onClick={() => handleToggleBot(bot.id)}
                                                style={{
                                                    ...styles.toggleBtn,
                                                    backgroundColor: bot.isActive ? '#e0a96d' : '#2b9348'
                                                }}
                                            >
                                                {bot.isActive ? t('bot.stop') : t('bot.start')}
                                            </button>

                                            <button
                                                onClick={() => setBacktestBot(bot)}
                                                style={styles.backtestBtn}
                                            >
                                                {t('bot.backtest')}
                                            </button>

                                            {/* KALDIR BUTONU */}
                                            <button
                                                onClick={() => handleDeleteBot(bot.id)}
                                                style={styles.deleteBtn}
                                            >
                                                {t('bot.remove')}
                                            </button>
                                        </td>
                                    </tr>
                                ))}
                            </tbody>
                        </table>
                    </div>
                )}
            </div>

            <BotSignalApproval bots={bots} />

            {backtestBot && (
                <BacktestReport
                    bot={backtestBot}
                    onClose={() => setBacktestBot(null)}
                />
            )}
        </div>
    );
}

const styles = {
    container: { padding: '32px', maxWidth: '1100px', margin: '0 auto', color: 'var(--text-primary)' },
    card: {
        backgroundColor: 'rgba(255, 255, 255, 0.05)',
        borderRadius: '16px',
        padding: '24px',
        border: '1px solid rgba(255, 255, 255, 0.08)',
        backdropFilter: 'blur(10px)'
    },
    errorBanner: {
        backgroundColor: 'rgba(239, 35, 60, 0.15)',
        color: '#ef233c',
        padding: '12px',
        borderRadius: '8px',
        marginBottom: '16px',
        fontSize: '14px'
    },
    formGrid: { display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(200px, 1fr))', gap: '16px' },
    formGroup: { display: 'flex', flexDirection: 'column', gap: '6px' },
    label: { fontSize: '13px', color: '#aaa', fontWeight: '500' },
    hint: { gridColumn: '1 / -1', margin: 0, fontSize: '12px', color: '#888' },
    input: {
        padding: '10px 14px',
        borderRadius: '8px',
        border: '1px solid rgba(255, 255, 255, 0.15)',
        backgroundColor: 'rgba(0, 0, 0, 0.2)',
        color: '#fff',
        fontSize: '14px',
        outline: 'none'
    },
    select: {
        padding: '10px 14px',
        borderRadius: '8px',
        border: '1px solid rgba(255, 255, 255, 0.15)',
        backgroundColor: 'rgba(20, 25, 35, 0.9)',
        color: '#fff',
        fontSize: '14px',
        outline: 'none',
        cursor: 'pointer'
    },
    option: {
        backgroundColor: '#121620',
        color: '#fff'
    },
    submitBtn: {
        width: '100%',
        padding: '12px',
        borderRadius: '8px',
        border: 'none',
        backgroundColor: '#00b4d8',
        color: '#fff',
        fontWeight: 'bold',
        cursor: 'pointer',
        fontSize: '15px'
    },
    tableContainer: { overflowX: 'auto' },
    table: { width: '100%', borderCollapse: 'collapse', textAlign: 'left' },
    thRow: { borderBottom: '1px solid rgba(255, 255, 255, 0.1)' },
    th: { padding: '12px', color: '#888', fontSize: '13px' },
    tr: { borderBottom: '1px solid rgba(255, 255, 255, 0.05)' },
    td: { padding: '14px 12px', fontSize: '14px' },
    toggleBtn: {
        padding: '6px 12px',
        borderRadius: '6px',
        border: 'none',
        color: '#fff',
        fontWeight: 'bold',
        cursor: 'pointer',
        fontSize: '12px'
    },
    backtestBtn: {
        padding: '6px 12px',
        borderRadius: '6px',
        border: 'none',
        backgroundColor: '#2563eb',
        color: '#fff',
        fontWeight: 'bold',
        cursor: 'pointer',
        fontSize: '12px'
    },
    deleteBtn: {
        padding: '6px 12px',
        borderRadius: '6px',
        border: 'none',
        backgroundColor: '#ef233c',
        color: '#fff',
        fontWeight: 'bold',
        cursor: 'pointer',
        fontSize: '12px'
    }
};