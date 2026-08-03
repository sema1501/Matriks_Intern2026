import React, { useMemo } from 'react';
import { useNavigate } from 'react-router-dom';
import { useAuth } from '../../context/AuthContext';
import { useGlobalPrices } from '../../context/PriceContext';
import { useCurrency } from '../../context/CurrencyContext';
import CryptoGrid from '../../components/CryptoGrid/CryptoGrid';

export default function Home() {
    const { user } = useAuth();
    const { prices, connectionStatus } = useGlobalPrices();
    const { formatPrice } = useCurrency();
    const navigate = useNavigate();

    const { topGainers, topLosers, stats } = useMemo(() => {
        const priceArray = Object.values(prices || {});

        if (priceArray.length === 0) {
            return { topGainers: [], topLosers: [], stats: { total: 0, highestPrice: 0, highestSymbol: '-' } };
        }

        const gainers = priceArray
            .filter(coin => Number(coin.priceChangePercentage24h) > 0)
            .sort((a, b) => b.priceChangePercentage24h - a.priceChangePercentage24h);

        const losers = priceArray
            .filter(coin => Number(coin.priceChangePercentage24h) < 0)
            .sort((a, b) => a.priceChangePercentage24h - b.priceChangePercentage24h);

        let highest = priceArray[0];
        priceArray.forEach(coin => {
            if (coin.currentPrice > highest.currentPrice) {
                highest = coin;
            }
        });

        return {
            topGainers: gainers.slice(0, 5),
            topLosers: losers.slice(0, 5),
            stats: {
                total: priceArray.length,
                highestPrice: highest.currentPrice,
                highestSymbol: highest.symbol.replace('USDT', '')
            }
        };
    }, [prices]);

    const isLoading = connectionStatus === 'connecting' && topGainers.length === 0;

    const renderList = (title, list, isGainerSection) => (
        <div
            style={styles.card}
            onMouseEnter={(e) => {
                e.currentTarget.style.transform = 'translateY(-4px)';
                e.currentTarget.style.boxShadow = '0 12px 24px rgba(0,0,0,0.15)';
                e.currentTarget.style.borderColor = 'rgba(0, 180, 216, 0.2)';
            }}
            onMouseLeave={(e) => {
                e.currentTarget.style.transform = 'translateY(0)';
                e.currentTarget.style.boxShadow = 'none';
                e.currentTarget.style.borderColor = 'rgba(255, 255, 255, 0.05)';
            }}
        >
            <h3 style={{
                marginTop: '1px',
                marginBottom: '20px',
                color: 'var(--text-primary)',
                fontWeight: '700',
                fontSize: '18px',
                display: 'flex',
                alignItems: 'center',
                lineHeight: '1'
            }}>
                {title}
            </h3>
            <div style={styles.listContainer}>
                {list.length > 0 ? (
                    list.map((coin) => {
                        const isCoinPositive = coin.priceChangePercentage24h >= 0;
                        return (
                            <div
                                key={coin.symbol}
                                style={styles.listItem}
                                onClick={() => navigate(`/coin/${coin.symbol}`)}
                                onMouseEnter={(e) => e.currentTarget.style.backgroundColor = 'rgba(255, 255, 255, 0.05)'}
                                onMouseLeave={(e) => e.currentTarget.style.backgroundColor = 'rgba(255, 255, 255, 0.02)'}
                            >
                                <span style={styles.symbol}>{coin.symbol.replace('USDT', '')}</span>
                                <span style={styles.price}>{formatPrice(coin.currentPrice)}</span>
                                <span style={{
                                    ...styles.percentage,
                                    color: isCoinPositive ? '#00b4d8' : '#ef233c',
                                    backgroundColor: isCoinPositive ? 'rgba(0, 180, 216, 0.08)' : 'rgba(239, 35, 60, 0.08)',
                                    padding: '6px 10px',
                                    borderRadius: '6px',
                                    fontSize: '14px',
                                    display: 'inline-flex',
                                    alignItems: 'center',
                                    justifyContent: 'center'
                                }}>
                                    {isCoinPositive ? '+' : ''}{coin.priceChangePercentage24h.toFixed(2)}%
                                </span>
                            </div>
                        );
                    })
                ) : (
                    <div style={{ padding: '24px', textAlign: 'center', color: '#888', fontSize: '14px', backgroundColor: 'rgba(255, 255, 255, 0.01)', borderRadius: '12px', border: '1px dashed rgba(255,255,255,0.05)' }}>
                        {isGainerSection ? 'Yükseliş yaşayan varlık bulunmuyor.' : 'Düşüş yaşayan varlık bulunmuyor. 🚀'}
                    </div>
                )}
            </div>
        </div>
    );

    return (
        <div style={{ padding: '32px', color: 'var(--text-primary)', maxWidth: '1400px', margin: '0 auto' }}>

            <div style={styles.band}>
                <div style={styles.bandItem}>
                    <span style={styles.bandLabel}>Takip Edilen Kripto</span>
                    <span style={styles.bandValue}>{stats.total} Adet</span>
                </div>
                <div style={styles.bandItem}>
                    <span style={styles.bandLabel}>En Değerli Varlık</span>
                    <span style={styles.bandValue}>{stats.highestSymbol} ({formatPrice(stats.highestPrice)})</span>
                </div>
                <div style={styles.bandItem}>
                    <span style={styles.bandLabel}>Piyasa Durumu</span>
                    <span style={{ ...styles.bandValue, color: '#00b4d8' }}>Canlı Akış Aktif</span>
                </div>
            </div>

            {isLoading ? (
                <div style={styles.gridContainer}>
                    {[1, 2].map((i) => (
                        <div key={i} style={styles.card}>
                            <div style={{ ...styles.skeleton, width: '50%', height: '24px', marginBottom: '16px' }} />
                            {[1, 2, 3, 4, 5].map((j) => (
                                <div key={j} style={{ ...styles.skeleton, width: '100%', height: '40px', marginBottom: '8px' }} />
                            ))}
                        </div>
                    ))}
                </div>
            ) : (
                <div style={styles.gridContainer}>
                    {renderList('↗ Top 5 Yükselen', topGainers, true)}
                    {renderList('↘ Top 5 Düşen', topLosers, false)}
                </div>
            )}

            <div style={{ marginTop: '32px' }}>
                <h3 style={{ marginBottom: '16px' }}>Tüm Kripto Para Piyasası</h3>
                <CryptoGrid />
            </div>
        </div>
    );
}

const styles = {
    band: {
        display: 'flex',
        flexWrap: 'wrap',
        gap: '16px',
        backgroundColor: 'rgba(0, 180, 216, 0.08)',
        border: '1px solid rgba(0, 180, 216, 0.25)',
        borderRadius: '12px',
        padding: '16px 24px',
        marginBottom: '32px',
        backdropFilter: 'blur(8px)',
        WebkitBackdropFilter: 'blur(8px)'
    },
    bandItem: {
        display: 'flex',
        flexDirection: 'column',
        flex: '1 1 200px'
    },
    bandLabel: {
        fontSize: '11px',
        color: '#888',
        textTransform: 'uppercase',
        letterSpacing: '1px',
        marginBottom: '4px'
    },
    bandValue: {
        fontSize: '16px',
        fontWeight: 'bold'
    },
    gridContainer: {
        display: 'grid',
        gridTemplateColumns: 'repeat(auto-fit, minmax(300px, 1fr))',
        gap: '24px',
        marginBottom: '32px'
    },
    card: {
        backgroundColor: 'rgba(255, 255, 255, 0.05)',
        borderRadius: '24px',
        padding: '24px',
        border: '1px solid rgba(255, 255, 255, 0.08)',
        backdropFilter: 'blur(12px)',
        WebkitBackdropFilter: 'blur(12px)',
        transition: 'transform 0.2s ease, box-shadow 0.2s ease, border-color 0.2s ease',
        cursor: 'default'
    },
    listContainer: {
        display: 'flex',
        flexDirection: 'column',
        gap: '10px'
    },
    listItem: {
        display: 'flex',
        justifyContent: 'space-between',
        alignItems: 'center',
        padding: '14px 20px',
        backgroundColor: 'rgba(255, 255, 255, 0.02)',
        border: '1px solid rgba(255, 255, 255, 0.04)',
        borderRadius: '12px',
        transition: 'background-color 0.15s ease',
        cursor: 'pointer'
    },
    symbol: { fontWeight: '700', fontSize: '15px' },
    price: { color: 'var(--text-primary)', fontWeight: '600', fontSize: '15px', marginLeft: 'auto', marginRight: '24px' },
    percentage: { fontWeight: '700', minWidth: '75px', textAlign: 'right' },
    skeleton: {
        backgroundColor: 'rgba(255, 255, 255, 0.05)',
        borderRadius: '4px',
        height: '20px'
    }
};