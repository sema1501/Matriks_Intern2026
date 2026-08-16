import { useCallback, useEffect, useMemo, useState } from 'react';
import {
    getAdminBots,
    getAdminPortfolios,
    getOvertradingBots,
    killAdminBot
} from '../../services/apiService';
import './AdminBots.css';

function AdminBots() {
    const [bots, setBots] = useState([]);
    const [portfolios, setPortfolios] = useState([]);
    const [overtradingBots, setOvertradingBots] = useState([]);
    const [search, setSearch] = useState('');
    const [statusFilter, setStatusFilter] = useState('all');
    const [loading, setLoading] = useState(true);
    const [error, setError] = useState('');
    const [message, setMessage] = useState('');
    const [killingBotId, setKillingBotId] = useState(null);

    const loadAdminData = useCallback(async (showLoader = true) => {
        try {
            if (showLoader) setLoading(true);
            setError('');

            const [botsResponse, portfoliosResponse, overtradingResponse] = await Promise.all([
                getAdminBots(),
                getAdminPortfolios(),
                getOvertradingBots()
            ]);

            setBots(botsResponse.data);
            setPortfolios(portfoliosResponse.data);
            setOvertradingBots(overtradingResponse.data);
        } catch (err) {
            if (err.response?.status === 403) {
                setError('Bu sayfaya yalnızca Admin veya SuperAdmin erişebilir.');
            } else {
                setError('Admin verileri alınırken bir hata oluştu.');
            }
        } finally {
            if (showLoader) setLoading(false);
        }
    }, []);

    useEffect(() => {
        loadAdminData();
        const intervalId = window.setInterval(() => loadAdminData(false), 15000);
        return () => window.clearInterval(intervalId);
    }, [loadAdminData]);

    const handleKillBot = async (bot) => {
        const approved = window.confirm(
            `${bot.username} kullanıcısının ${bot.symbol} botunu zorla durdurmak istiyor musunuz?`
        );
        if (!approved) return;

        try {
            setKillingBotId(bot.id);
            setMessage('');
            await killAdminBot(bot.id);
            setMessage(`${bot.symbol} botu başarıyla durduruldu.`);
            await loadAdminData(false);
        } catch (err) {
            setMessage(err.response?.data?.message || 'Bot durdurulurken bir hata oluştu.');
        } finally {
            setKillingBotId(null);
        }
    };

    const normalizedSearch = search.trim().toLowerCase();

    const filteredBots = useMemo(() => {
        return bots.filter((bot) => {
            const matchesSearch =
                bot.username.toLowerCase().includes(normalizedSearch) ||
                bot.symbol.toLowerCase().includes(normalizedSearch) ||
                bot.strategy.toLowerCase().includes(normalizedSearch);

            const matchesStatus =
                statusFilter === 'all' ||
                (statusFilter === 'active' && bot.isActive) ||
                (statusFilter === 'inactive' && !bot.isActive) ||
                (statusFilter === 'risky' && bot.isOvertrading);

            return matchesSearch && matchesStatus;
        });
    }, [bots, normalizedSearch, statusFilter]);

    const filteredPortfolios = useMemo(() => {
        return portfolios.filter((portfolio) => {
            const usernameMatches = portfolio.username.toLowerCase().includes(normalizedSearch);
            const holdingMatches = portfolio.holdings.some((holding) =>
                holding.symbol.toLowerCase().includes(normalizedSearch)
            );
            return usernameMatches || holdingMatches;
        });
    }, [portfolios, normalizedSearch]);

    if (loading) return <div className="admin-bots-page">Veriler yükleniyor...</div>;
    if (error) return <div className="admin-bots-page admin-error">{error}</div>;

    return (
        <div className="admin-bots-page">
            <div className="admin-page-header">
                <div>
                    <h1>Bot ve Portföy Gözetimi</h1>
                    <p>Aktif botları izleyin, aşırı işlem riskini görün ve gerektiğinde botu anında durdurun.</p>
                </div>
                <div className="admin-filters">
                    <input type="search" placeholder="Kullanıcı, sembol veya strateji ara..." value={search} onChange={(e) => setSearch(e.target.value)} />
                    <select value={statusFilter} onChange={(e) => setStatusFilter(e.target.value)}>
                        <option value="all">Tüm botlar</option>
                        <option value="active">Aktif botlar</option>
                        <option value="inactive">Pasif botlar</option>
                        <option value="risky">Aşırı işlem riski</option>
                    </select>
                </div>
            </div>

            {message && <div className="admin-message">{message}</div>}

            <section className={`admin-risk-summary ${overtradingBots.length > 0 ? 'has-risk' : ''}`}>
                <div>
                    <strong>Aşırı İşlem Tespiti</strong>
                    <span>Son 15 dakikada 5 veya daha fazla onaylı işlem yapan aktif botlar riskli kabul edilir.</span>
                </div>
                <div className="admin-risk-count">{overtradingBots.length} riskli bot</div>
            </section>

            <section className="admin-section">
                <h2>Botlar ({filteredBots.length})</h2>
                <div className="admin-table-wrapper">
                    <table className="admin-table">
                        <thead><tr><th>Kullanıcı</th><th>Sembol</th><th>Strateji</th><th>RSI Al / Sat</th><th>İşlem Miktarı</th><th>Son 15 dk</th><th>Durum</th><th>Admin İşlemi</th></tr></thead>
                        <tbody>
                            {filteredBots.length === 0 ? <tr><td colSpan="8" className="admin-empty">Gösterilecek bot bulunamadı.</td></tr> : filteredBots.map((bot) => (
                                <tr key={bot.id} className={bot.isOvertrading ? 'admin-risk-row' : ''}>
                                    <td>{bot.username}</td><td>{bot.symbol}</td><td>{bot.strategy}</td>
                                    <td>{bot.buyRsiThreshold} / {bot.sellRsiThreshold}</td><td>{bot.tradeQuantity}</td>
                                    <td><span className={`admin-trade-count ${bot.isOvertrading ? 'danger' : ''}`}>{bot.recentTradeCount} işlem</span>{bot.isOvertrading && <span className="admin-risk-badge">Aşırı işlem</span>}</td>
                                    <td><span className={`admin-status ${bot.isActive ? 'active' : 'inactive'}`}>{bot.isActive ? 'Aktif' : 'Pasif'}</span></td>
                                    <td><button className="admin-kill-button" disabled={!bot.isActive || killingBotId === bot.id} onClick={() => handleKillBot(bot)}>{killingBotId === bot.id ? 'Durduruluyor...' : bot.isActive ? 'Zorla Durdur' : 'Durduruldu'}</button></td>
                                </tr>
                            ))}
                        </tbody>
                    </table>
                </div>
            </section>

            <section className="admin-section">
                <h2>Portföyler ({filteredPortfolios.length})</h2>
                <div className="admin-table-wrapper"><table className="admin-table"><thead><tr><th>Kullanıcı</th><th>Sanal Bakiye</th><th>Varlık Sayısı</th><th>Holding Özeti</th></tr></thead><tbody>
                    {filteredPortfolios.length === 0 ? <tr><td colSpan="4" className="admin-empty">Gösterilecek portföy bulunamadı.</td></tr> : filteredPortfolios.map((p) => <tr key={p.userId}><td>{p.username}</td><td>{Number(p.virtualBalance).toFixed(2)} USD</td><td>{p.holdings.length}</td><td>{p.holdings.length === 0 ? 'Varlık yok' : p.holdings.map((h) => `${h.symbol}: ${h.quantity}`).join(', ')}</td></tr>)}
                </tbody></table></div>
            </section>
        </div>
    );
}

export default AdminBots;
