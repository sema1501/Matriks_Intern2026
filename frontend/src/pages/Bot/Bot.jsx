import React, { useState, useEffect } from 'react';
import { getBots, createBot, toggleBot, deleteBot } from '../../services/apiService';

const ALL_SYMBOLS = [
    'BTCUSDT', 'ETHUSDT', 'BNBUSDT', 'SOLUSDT', 'LTCUSDT',
    'LINKUSDT', 'ETCUSDT', 'AVAXUSDT', 'UNIUSDT', 'NEARUSDT',
    'ATOMUSDT', 'XRPUSDT', 'DOTUSDT', 'FILUSDT', 'APTUSDT',
    'TRXUSDT', 'ADAUSDT', 'XLMUSDT', 'POLUSDT', 'DOGEUSDT',
    'SHIBUSDT', 'PEPEUSDT', 'SUIUSDT', 'ARBUSDT', 'OPUSDT'
];

export default function Bot() {
    const [bots, setBots] = useState([]);
    const [loading, setLoading] = useState(true);
    const [submitting, setSubmitting] = useState(false);
    const [error, setError] = useState(null);
    const [formError, setFormError] = useState(null);

    const [symbol, setSymbol] = useState('BTCUSDT');
    const [buyRsiThreshold, setBuyRsiThreshold] = useState(30);
    const [sellRsiThreshold, setSellRsiThreshold] = useState(70);
    const [tradeQuantity, setTradeQuantity] = useState(0.01);

    const fetchBots = async () => {
        try {
            setError(null);
            const response = await getBots();
            setBots(response?.data || []);
        } catch (err) {
            console.error("Botlar yüklenemedi:", err);
            setError("Bot verileri çekilirken bir sorun oluştu.");
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

        if (Number(buyRsiThreshold) >= Number(sellRsiThreshold)) {
            setFormError("AL eşiği (Buy RSI), SAT eşiğinden (Sell RSI) küçük olmalıdır!");
            return;
        }

        if (Number(tradeQuantity) <= 0) {
            setFormError("İşlem miktarı 0'dan büyük olmalıdır.");
            return;
        }

        try {
            setSubmitting(true);
            await createBot({
                symbol: symbol.toUpperCase(),
                buyRsiThreshold: parseInt(buyRsiThreshold, 10),
                sellRsiThreshold: parseInt(sellRsiThreshold, 10),
                tradeQuantity: parseFloat(tradeQuantity)
            });

            setFormError(null);
            fetchBots();
        } catch (err) {
            console.error("Bot oluşturulamadı:", err);
            setFormError(err.response?.data?.message || "Bot oluşturulurken bir hata oluştu.");
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
            alert("Bot durumu değiştirilirken hata oluştu.");
        }
    };

    // BOT SILME / KALDIRMA FONKSIYONU
    const handleDeleteBot = async (id) => {
        if (!window.confirm("Bu botu kaldırmak istediğinize emin misiniz?")) return;

        try {
            await deleteBot(id);
            // Silinen botu state'ten kaldırarak arayüzün anında güncellenmesini sağlıyoruz
            setBots(prevBots => prevBots.filter(bot => bot.id !== id));
        } catch (err) {
            console.error("Bot silinemedi:", err);
            alert("Bot kaldırılırken bir hata oluştu.");
        }
    };

    return (
        <div style={styles.container}>
            <h2 style={{ fontSize: '28px', fontWeight: '700', marginBottom: '8px' }}>🤖 Alım-Satım Botu Yönetimi</h2>
            <p style={{ color: '#888', marginBottom: '24px', fontSize: '14px' }}>
                RSI stratejinize göre otomatik sinyal üreten sanal botlarınızı buradan kurabilir ve yönetebilirsiniz.
            </p>

            {/* BOT KURULUM FORMU */}
            <div style={styles.card}>
                <h3 style={{ marginTop: 0, marginBottom: '16px' }}>Yeni Bot Kur</h3>

                {formError && (
                    <div style={styles.errorBanner}>{formError}</div>
                )}

                <form onSubmit={handleCreateBot} style={styles.formGrid}>
                    <div style={styles.formGroup}>
                        <label style={styles.label}>Sembol Seçin</label>
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
                        <label style={styles.label}>AL RSI Eşiği (Varsayılan: 30)</label>
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
                        <label style={styles.label}>SAT RSI Eşiği (Varsayılan: 70)</label>
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

                    <div style={styles.formGroup}>
                        <label style={styles.label}>İşlem Miktarı (Quantity)</label>
                        <input
                            type="number"
                            step="any"
                            value={tradeQuantity}
                            onChange={(e) => setTradeQuantity(e.target.value)}
                            required
                            style={styles.input}
                        />
                    </div>

                    <div style={{ gridColumn: '1 / -1', marginTop: '12px' }}>
                        <button type="submit" disabled={submitting} style={styles.submitBtn}>
                            {submitting ? 'Bot Kuruluyor...' : '🤖 Botu Oluştur ve Başlat'}
                        </button>
                    </div>
                </form>
            </div>

            {/* BOT LISTESI TABLOSU */}
            <div style={{ ...styles.card, marginTop: '32px' }}>
                <h3 style={{ marginTop: 0, marginBottom: '16px' }}>Mevcut Botlarım</h3>

                {loading ? (
                    <p style={{ color: '#888' }}>Botlar yükleniyor...</p>
                ) : error ? (
                    <p style={{ color: '#ef233c' }}>{error}</p>
                ) : bots.length === 0 ? (
                    <p style={{ color: '#888' }}>Henüz kurulmuş bir botunuz bulunmamaktadır.</p>
                ) : (
                    <div style={styles.tableContainer}>
                        <table style={styles.table}>
                            <thead>
                                <tr style={styles.thRow}>
                                    <th style={styles.th}>Sembol</th>
                                    <th style={styles.th}>AL Eşiği (RSI)</th>
                                    <th style={styles.th}>SAT Eşiği (RSI)</th>
                                    <th style={styles.th}>Miktar</th>
                                    <th style={styles.th}>Durum</th>
                                    <th style={styles.th}>İşlem</th>
                                </tr>
                            </thead>
                            <tbody>
                                {bots.map((bot) => (
                                    <tr key={bot.id} style={styles.tr}>
                                        <td style={{ ...styles.td, fontWeight: 'bold' }}>{bot.symbol}</td>
                                        <td style={{ ...styles.td, color: '#00b4d8' }}>≤ {bot.buyRsiThreshold}</td>
                                        <td style={{ ...styles.td, color: '#ef233c' }}>≥ {bot.sellRsiThreshold}</td>
                                        <td style={styles.td}>{bot.tradeQuantity}</td>
                                        <td style={styles.td}>
                                            <span style={{
                                                padding: '4px 10px',
                                                borderRadius: '12px',
                                                fontSize: '12px',
                                                fontWeight: 'bold',
                                                backgroundColor: bot.isActive ? 'rgba(0, 180, 216, 0.15)' : 'rgba(255, 255, 255, 0.1)',
                                                color: bot.isActive ? '#00b4d8' : '#888'
                                            }}>
                                                {bot.isActive ? '● Aktif' : '○ Pasif'}
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
                                                {bot.isActive ? 'Durdur' : 'Başlat'}
                                            </button>

                                            {/* KALDIR BUTONU */}
                                            <button
                                                onClick={() => handleDeleteBot(bot.id)}
                                                style={styles.deleteBtn}
                                            >
                                                Kaldır
                                            </button>
                                        </td>
                                    </tr>
                                ))}
                            </tbody>
                        </table>
                    </div>
                )}
            </div>
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