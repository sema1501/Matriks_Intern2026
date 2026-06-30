import React, { useEffect, useRef, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useBinance } from '../../context/BinanceContext';
import './CryptoCard.css';

export default function CryptoCard({ meta, priceData }) {
    const navigate = useNavigate();
    const { connectionStatus } = useBinance();
    const [flashClass, setFlashClass] = useState('');
    const [imageError, setImageError] = useState(false);
    const prevPriceRef = useRef();

    useEffect(() => {
        if (priceData && prevPriceRef.current !== undefined) {
            const prevPrice = prevPriceRef.current;
            const currentPrice = priceData.currentPrice;

            if (currentPrice > prevPrice) {
                setFlashClass('flash-green');
            } else if (currentPrice < prevPrice) {
                setFlashClass('flash-red');
            }

            const timer = setTimeout(() => setFlashClass(''), 800);
            return () => clearTimeout(timer);
        }

        if (priceData) {
            prevPriceRef.current = priceData.currentPrice;
        }
    }, [priceData]);

    if (!priceData && meta) {
        return (
            <div className="crypto-card skeleton">
                <div className="skeleton-logo"></div>
                <div className="skeleton-info">
                    <div className="skeleton-line"></div>
                    <div className="skeleton-line short"></div>
                </div>
            </div>
        );
    }

    const displayName = meta && meta.name ? meta.name : "Bilinmeyen Varlık";
    const displaySymbol = meta && meta.symbol ? meta.symbol : "N/A";

    const currentPrice = priceData ? priceData.currentPrice : 0;
    const priceChangePercentage24h = priceData ? priceData.priceChangePercentage24h : 0;
    const isPositive = priceChangePercentage24h >= 0;
    const isDisconnected = connectionStatus === 'disconnected';

    const handleCardClick = () => {
        if (meta && meta.symbol && !isDisconnected) {
            navigate(`/coin/${meta.symbol}USDT`);
        }
    };

    return (
        <div
            className={`crypto-card ${flashClass} ${!meta ? 'inactive-card' : ''} ${isDisconnected ? 'offline-card' : ''}`}
            onClick={handleCardClick}
            style={{ cursor: isDisconnected ? 'not-allowed' : 'pointer' }}
        >
            {isDisconnected && (
                <div className="offline-overlay">
                    <span className="offline-badge">Canlı Değil</span>
                </div>
            )}

            <div className="card-header">
                {meta && meta.image && !imageError ? (
                    <img
                        src={meta.image}
                        alt={displayName}
                        className="coin-logo"
                        onError={() => setImageError(true)}
                    />
                ) : (
                    <div className="coin-logo-fallback">
                        {displaySymbol.charAt(0)}
                    </div>
                )}
                <div className="coin-info">
                    <h3 className="coin-name">{displayName}</h3>
                    <span className="coin-symbol">{displaySymbol}</span>
                </div>
            </div>

            <div className="card-body">
                <div className="coin-price">
                    {priceData
                        ? `$${Number(currentPrice).toLocaleString('en-US', { minimumFractionDigits: 2, maximumFractionDigits: 6 })}`
                        : "$0.00"
                    }
                </div>
                <div className={`coin-change ${priceData ? (isPositive ? 'positive' : 'negative') : 'neutral'}`}>
                    <span className="arrow-icon">{priceData ? (isPositive ? '↑' : '↓') : '•'}</span>
                    <span>{priceData ? `${Number(priceChangePercentage24h).toFixed(2)}%` : "Veri Yok"}</span>
                </div>
            </div>
        </div>
    );
}