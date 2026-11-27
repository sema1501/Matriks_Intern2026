import { useCallback, useEffect, useMemo, useState } from 'react';
import {
    getAdminBots,
    getAdminPortfolios,
    getOvertradingBots,
    killAdminBot
} from '../../services/apiService';
import { useLanguage } from '../../context/LanguageContext';
import './AdminBots.css';

function AdminBots() {
    const { t, locale } = useLanguage();
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
                setError(t('adminBots.forbidden'));
            } else {
                setError(t('adminBots.loadError'));
            }
        } finally {
            if (showLoader) setLoading(false);
        }
    }, [t]);

    useEffect(() => {
        loadAdminData();
        const intervalId = window.setInterval(() => loadAdminData(false), 15000);
        return () => window.clearInterval(intervalId);
    }, [loadAdminData]);

    const handleKillBot = async (bot) => {
        const approved = window.confirm(
            t('adminBots.killConfirm', { username: bot.username, symbol: bot.symbol })
        );
        if (!approved) return;

        try {
            setKillingBotId(bot.id);
            setMessage('');
            await killAdminBot(bot.id);
            setMessage(t('adminBots.killSuccess', { symbol: bot.symbol }));
            await loadAdminData(false);
        } catch (err) {
            setMessage(err.response?.data?.message || t('adminBots.killError'));
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

    const strategyLabel = (strategy) => {
        const key = String(strategy).toLowerCase();
        if (key === 'rsi' || key === '0' || key === 'rsithreshold') return t('bot.strategy.rsi');
        if (key === 'ema' || key === '1' || key === 'emacrossover') return t('bot.strategy.ema');
        return strategy;
    };

    if (loading) return <div className="admin-bots-page">{t('adminBots.loading')}</div>;
    if (error) return <div className="admin-bots-page admin-error">{error}</div>;

    return (
        <div className="admin-bots-page">
            <div className="admin-page-header">
                <div>
                    <h1>{t('nav.admin')}</h1>
                    <p>{t('adminBots.subtitle')}</p>
                </div>
                <div className="admin-filters">
                    <input type="search" placeholder={t('adminBots.searchPlaceholder')} aria-label={t('adminBots.searchPlaceholder')} value={search} onChange={(e) => setSearch(e.target.value)} />
                    <select aria-label={t('adminBots.filterLabel')} value={statusFilter} onChange={(e) => setStatusFilter(e.target.value)}>
                        <option value="all">{t('adminBots.filter.all')}</option>
                        <option value="active">{t('adminBots.filter.active')}</option>
                        <option value="inactive">{t('adminBots.filter.inactive')}</option>
                        <option value="risky">{t('adminBots.filter.risky')}</option>
                    </select>
                </div>
            </div>

            {message && <div className="admin-message">{message}</div>}

            <section className={`admin-risk-summary ${overtradingBots.length > 0 ? 'has-risk' : ''}`}>
                <div>
                    <strong>{t('adminBots.risk.title')}</strong>
                    <span>{t('adminBots.risk.description')}</span>
                </div>
                <div className="admin-risk-count">{t('adminBots.risk.count', { count: overtradingBots.length })}</div>
            </section>

            <section className="admin-section">
                <h2>{t('adminBots.bots', { count: filteredBots.length })}</h2>
                <div className="admin-table-wrapper">
                    <table className="admin-table">
                        <thead><tr><th>{t('adminBots.col.user')}</th><th>{t('bot.symbol')}</th><th>{t('bot.strategy')}</th><th>{t('adminBots.col.rsi')}</th><th>{t('bot.quantity')}</th><th>{t('adminBots.col.last15')}</th><th>{t('bot.status')}</th><th>{t('adminBots.col.adminAction')}</th></tr></thead>
                        <tbody>
                            {filteredBots.length === 0 ? <tr><td colSpan="8" className="admin-empty">{t('adminBots.noBots')}</td></tr> : filteredBots.map((bot) => (
                                <tr key={bot.id} className={bot.isOvertrading ? 'admin-risk-row' : ''}>
                                    <td>{bot.username}</td><td>{bot.symbol}</td><td>{strategyLabel(bot.strategy)}</td>
                                    <td>{bot.buyRsiThreshold} / {bot.sellRsiThreshold}</td><td>{bot.tradeQuantity}</td>
                                    <td><span className={`admin-trade-count ${bot.isOvertrading ? 'danger' : ''}`}>{t('adminBots.tradeCount', { count: bot.recentTradeCount })}</span>{bot.isOvertrading && <span className="admin-risk-badge">{t('adminBots.overtrading')}</span>}</td>
                                    <td><span className={`admin-status ${bot.isActive ? 'active' : 'inactive'}`}>{bot.isActive ? t('common.active') : t('common.passive')}</span></td>
                                    <td><button className="admin-kill-button" disabled={!bot.isActive || killingBotId === bot.id} onClick={() => handleKillBot(bot)}>{killingBotId === bot.id ? t('adminBots.killing') : bot.isActive ? t('adminBots.kill') : t('adminBots.killed')}</button></td>
                                </tr>
                            ))}
                        </tbody>
                    </table>
                </div>
            </section>

            <section className="admin-section">
                <h2>{t('adminBots.portfolios', { count: filteredPortfolios.length })}</h2>
                <div className="admin-table-wrapper"><table className="admin-table"><thead><tr><th>{t('adminBots.col.user')}</th><th>{t('adminBots.col.balance')}</th><th>{t('adminBots.col.assetCount')}</th><th>{t('adminBots.col.holdings')}</th></tr></thead><tbody>
                    {filteredPortfolios.length === 0 ? <tr><td colSpan="4" className="admin-empty">{t('adminBots.noPortfolios')}</td></tr> : filteredPortfolios.map((p) => <tr key={p.userId}><td>{p.username}</td><td>{Number(p.virtualBalance).toLocaleString(locale, { minimumFractionDigits: 2, maximumFractionDigits: 2 })} USD</td><td>{p.holdings.length}</td><td>{p.holdings.length === 0 ? t('adminBots.noAssets') : p.holdings.map((h) => `${h.symbol}: ${h.quantity}`).join(', ')}</td></tr>)}
                </tbody></table></div>
            </section>
        </div>
    );
}

export default AdminBots;
