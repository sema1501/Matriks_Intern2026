import { useEffect, useMemo, useState } from 'react';
import {
    getAdminBots,
    getAdminPortfolios
} from '../../services/apiService';
import './AdminBots.css';

function AdminBots() {
    const [bots, setBots] = useState([]);
    const [portfolios, setPortfolios] = useState([]);
    const [search, setSearch] = useState('');
    const [statusFilter, setStatusFilter] = useState('all');
    const [loading, setLoading] = useState(true);
    const [error, setError] = useState('');

    useEffect(() => {
        const loadAdminData = async () => {
            try {
                setLoading(true);
                setError('');

                const [botsResponse, portfoliosResponse] = await Promise.all([
                    getAdminBots(),
                    getAdminPortfolios()
                ]);

                setBots(botsResponse.data);
                setPortfolios(portfoliosResponse.data);
            } catch (err) {
                if (err.response?.status === 403) {
                    setError('Bu sayfaya yalnızca Admin veya SuperAdmin erişebilir.');
                } else {
                    setError('Admin verileri alınırken bir hata oluştu.');
                }
            } finally {
                setLoading(false);
            }
        };

        loadAdminData();
    }, []);

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
                (statusFilter === 'inactive' && !bot.isActive);

            return matchesSearch && matchesStatus;
        });
    }, [bots, normalizedSearch, statusFilter]);

    const filteredPortfolios = useMemo(() => {
        return portfolios.filter((portfolio) => {
            const usernameMatches = portfolio.username
                .toLowerCase()
                .includes(normalizedSearch);

            const holdingMatches = portfolio.holdings.some((holding) =>
                holding.symbol.toLowerCase().includes(normalizedSearch)
            );

            return usernameMatches || holdingMatches;
        });
    }, [portfolios, normalizedSearch]);

    if (loading) {
        return <div className="admin-bots-page">Veriler yükleniyor...</div>;
    }

    if (error) {
        return <div className="admin-bots-page admin-error">{error}</div>;
    }

    return (
        <div className="admin-bots-page">
            <div className="admin-page-header">
                <div>
                    <h1>Bot ve Portföy Gözetimi</h1>
                    <p>Tüm kullanıcıların bot ve portföy özetlerini görüntüleyin.</p>
                </div>

                <div className="admin-filters">
                    <input
                        type="search"
                        placeholder="Kullanıcı, sembol veya strateji ara..."
                        value={search}
                        onChange={(event) => setSearch(event.target.value)}
                    />

                    <select
                        value={statusFilter}
                        onChange={(event) => setStatusFilter(event.target.value)}
                    >
                        <option value="all">Tüm botlar</option>
                        <option value="active">Aktif botlar</option>
                        <option value="inactive">Pasif botlar</option>
                    </select>
                </div>
            </div>

            <section className="admin-section">
                <h2>Botlar ({filteredBots.length})</h2>

                <div className="admin-table-wrapper">
                    <table className="admin-table">
                        <thead>
                            <tr>
                                <th>Kullanıcı</th>
                                <th>Sembol</th>
                                <th>Strateji</th>
                                <th>RSI Al / Sat</th>
                                <th>İşlem Miktarı</th>
                                <th>Durum</th>
                            </tr>
                        </thead>
                        <tbody>
                            {filteredBots.length === 0 ? (
                                <tr>
                                    <td colSpan="6" className="admin-empty">
                                        Gösterilecek bot bulunamadı.
                                    </td>
                                </tr>
                            ) : (
                                filteredBots.map((bot) => (
                                    <tr key={bot.id}>
                                        <td>{bot.username}</td>
                                        <td>{bot.symbol}</td>
                                        <td>{bot.strategy}</td>
                                        <td>
                                            {bot.buyRsiThreshold} / {bot.sellRsiThreshold}
                                        </td>
                                        <td>{bot.tradeQuantity}</td>
                                        <td>
                                            <span
                                                className={`admin-status ${
                                                    bot.isActive ? 'active' : 'inactive'
                                                }`}
                                            >
                                                {bot.isActive ? 'Aktif' : 'Pasif'}
                                            </span>
                                        </td>
                                    </tr>
                                ))
                            )}
                        </tbody>
                    </table>
                </div>
            </section>

            <section className="admin-section">
                <h2>Portföyler ({filteredPortfolios.length})</h2>

                <div className="admin-table-wrapper">
                    <table className="admin-table">
                        <thead>
                            <tr>
                                <th>Kullanıcı</th>
                                <th>Sanal Bakiye</th>
                                <th>Varlık Sayısı</th>
                                <th>Holding Özeti</th>
                            </tr>
                        </thead>
                        <tbody>
                            {filteredPortfolios.length === 0 ? (
                                <tr>
                                    <td colSpan="4" className="admin-empty">
                                        Gösterilecek portföy bulunamadı.
                                    </td>
                                </tr>
                            ) : (
                                filteredPortfolios.map((portfolio) => (
                                    <tr key={portfolio.userId}>
                                        <td>{portfolio.username}</td>
                                        <td>
                                            {Number(portfolio.virtualBalance).toFixed(2)} USD
                                        </td>
                                        <td>{portfolio.holdings.length}</td>
                                        <td>
                                            {portfolio.holdings.length === 0
                                                ? 'Varlık yok'
                                                : portfolio.holdings
                                                      .map(
                                                          (holding) =>
                                                              `${holding.symbol}: ${holding.quantity}`
                                                      )
                                                      .join(', ')}
                                        </td>
                                    </tr>
                                ))
                            )}
                        </tbody>
                    </table>
                </div>
            </section>
        </div>
    );
}

export default AdminBots;