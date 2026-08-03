import React, { useState, useEffect } from 'react';
import { getLeaderboard } from '../../services/apiService';

export default function Leaderboard() {
    const [leaderboard, setLeaderboard] = useState([]);
    const [loading, setLoading] = useState(true);
    const [error, setError] = useState(null);

    useEffect(() => {
        const fetchLeaderboardData = async () => {
            try {
                const response = await getLeaderboard();
                const data = response?.data || [];
                setLeaderboard(data.slice(0, 10));
            } catch (err) {
                console.error("Liderlik tablosu çekilemedi:", err);
                setError("Liderlik tablosu verileri yüklenirken bir sorun oluştu.");
            } finally {
                setLoading(false);
            }
        };

        fetchLeaderboardData();
    }, []);

    return (
        <div style={{ padding: '32px', color: 'var(--text-primary)', maxWidth: '1000px', margin: '0 auto' }}>
            <div style={styles.headerContainer}>
                <h2 style={{ fontSize: '28px', fontWeight: '700', margin: 0 }}>🏆 Liderlik Tablosu (Top 10)</h2>
                <p style={{ color: '#888', marginTop: '8px', fontSize: '14px' }}>
                    Sanal bakiye ve portföy performansına göre en yüksek kâr oranına sahip kullanıcılar listelenmektedir.
                </p>
            </div>

            <div style={styles.card}>
                {loading ? (
                    <div style={styles.listContainer}>
                        {[1, 2, 3, 4, 5, 6, 7, 8, 9, 10].map((j) => (
                            <div key={j} style={{ ...styles.skeleton, width: '100%', height: '48px', marginBottom: '8px' }} />
                        ))}
                    </div>
                ) : error ? (
                    <div style={{ padding: '24px', textAlign: 'center', color: '#ef233c', fontSize: '14px' }}>
                        {error}
                    </div>
                ) : leaderboard.length > 0 ? (
                    <div style={styles.listContainer}>
                        {leaderboard.map((item, index) => {
                            const isProfit = item.profitLossPercentage >= 0;
                            return (
                                <div
                                    key={index}
                                    style={styles.listItem}
                                    onMouseEnter={(e) => e.currentTarget.style.backgroundColor = 'rgba(255, 255, 255, 0.05)'}
                                    onMouseLeave={(e) => e.currentTarget.style.backgroundColor = 'rgba(255, 255, 255, 0.02)'}
                                >
                                    <span style={{ ...styles.symbol, color: index === 0 ? '#ffd700' : index === 1 ? '#c0c0c0' : index === 2 ? '#cd7f32' : 'var(--text-primary)' }}>
                                        {index === 0 ? '🥇 #1' : index === 1 ? '🥈 #2' : index === 2 ? '🥉 #3' : `#${index + 1}`} {item.username}
                                    </span>
                                    <span style={{
                                        ...styles.percentage,
                                        color: isProfit ? '#00b4d8' : '#ef233c',
                                        backgroundColor: isProfit ? 'rgba(0, 180, 216, 0.08)' : 'rgba(239, 35, 60, 0.08)',
                                        padding: '6px 14px',
                                        borderRadius: '6px',
                                        fontSize: '14px',
                                        display: 'inline-flex',
                                        alignItems: 'center',
                                        justifyContent: 'center',
                                        marginLeft: 'auto'
                                    }}>
                                        {isProfit ? '+' : ''}{item.profitLossPercentage.toFixed(2)}%
                                    </span>
                                </div>
                            );
                        })}
                    </div>
                ) : (
                    <div style={{ padding: '32px', textAlign: 'center', color: '#888', fontSize: '14px' }}>
                        Henüz sıralama verisi bulunmuyor.
                    </div>
                )}
            </div>
        </div>
    );
}

const styles = {
    headerContainer: {
        marginBottom: '24px',
        textAlign: 'left'
    },
    card: {
        backgroundColor: 'rgba(255, 255, 255, 0.05)',
        borderRadius: '24px',
        padding: '24px',
        border: '1px solid rgba(255, 255, 255, 0.08)',
        backdropFilter: 'blur(12px)',
        WebkitBackdropFilter: 'blur(12px)'
    },
    listContainer: {
        display: 'flex',
        flexDirection: 'column',
        gap: '12px'
    },
    listItem: {
        display: 'flex',
        justifyContent: 'space-between',
        alignItems: 'center',
        padding: '16px 24px',
        backgroundColor: 'rgba(255, 255, 255, 0.02)',
        border: '1px solid rgba(255, 255, 255, 0.04)',
        borderRadius: '12px',
        transition: 'background-color 0.15s ease'
    },
    symbol: { fontWeight: '700', fontSize: '16px' },
    percentage: { fontWeight: '700', minWidth: '85px', textAlign: 'right' },
    skeleton: {
        backgroundColor: 'rgba(255, 255, 255, 0.05)',
        borderRadius: '8px'
    }
};