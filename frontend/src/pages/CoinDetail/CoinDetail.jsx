import React, { useState } from 'react';
import { useParams, useNavigate } from 'react-router-dom';
import { useBinance } from '../../context/BinanceContext';
import { COIN_META } from '../../data/coinMeta';
import './CoinDetail.css';

export default function CoinDetail() {
    const { symbol } = useParams();
    const navigate = useNavigate();
    const { prices } = useBinance();
    const [imageError, setImageError] = useState(false);

    const meta = COIN_META[symbol];
    const priceData = prices ? prices[symbol] : undefined;

    if (!meta) {
        return (
            <div className="detail-error-container">
                <div className="error-icon">⚠️</div>
                <h2>Geçersiz Sembol</h2>
                <p>"{symbol}" kriterine uygun bir kripto varlık bulunamadı.</p>
                <button onClick={() => navigate('/')} className="back-btn">Listeye Geri Dön</button>
            </div>
        );
    }

    const displayName = meta.name || "Bilinmeyen Varlık";
    const displaySymbol = meta.symbol || "N/A";
    const isPositive = priceData?.priceChangePercentage24h >= 0;

    return (
        <div className="detail-container">
            <button onClick={() => navigate('/')} className="back-btn">← Listeye Geri Dön</button>

            <div className="detail-card">
                <div className="detail-header">
                    {meta.image && !imageError ? (
                        <img
                            src={meta.image}
                            alt={displayName}
                            className="detail-logo-img"
                            onError={() => setImageError(true)}
                        />
                    ) : (
                        <div className="coin-logo-fallback large">
                            {displaySymbol.charAt(0)}
                        </div>
                    )}
                    <div>
                        <h1 className="detail-name">{displayName}</h1>
                        <span className="detail-symbol-badge">{displaySymbol}</span>
                    </div>
                </div>

                {priceData ? (
                    <div className="detail-body">
                        <div className="detail-price-section">
                            <div className="detail-price-label">Anlık Fiyat</div>
                            <div className="detail-price-value">
                                ${priceData.currentPrice.toLocaleString('en-US', { minimumFractionDigits: 2, maximumFractionDigits: 6 })}
                            </div>
                            <div className={`detail-change ${isPositive ? 'up' : 'down'}`}>
                                {isPositive ? '▲' : '▼'} {Math.abs(priceData.priceChangePercentage24h).toFixed(2)}% (24s)
                            </div>
                        </div>

                        <div className="detail-stats-grid">
                            <div className="stat-box">
                                <div className="stat-label">24s En Yüksek</div>
                                <div className="stat-value high">
                                    ${priceData.high24h.toLocaleString('en-US', { minimumFractionDigits: 2, maximumFractionDigits: 6 })}
                                </div>
                            </div>
                            <div className="stat-box">
                                <div className="stat-label">24s En Düşük</div>
                                <div className="stat-value low">
                                    ${priceData.low24h.toLocaleString('en-US', { minimumFractionDigits: 2, maximumFractionDigits: 6 })}
                                </div>
                            </div>
                        </div>
                    </div>
                ) : (
                    <div className="detail-loading">Canlı veriler yükleniyor...</div>
                )}
            </div>
        </div>
    );
}