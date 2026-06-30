import React, { useState, useMemo } from 'react';
import { useBinance } from '../../context/BinanceContext';
import { TRACKED_SYMBOLS } from '../../data/trackedSymbols';
import { COIN_META } from '../../data/coinMeta';
import CryptoCard from '../CryptoCard/CryptoCard';
import './CryptoGrid.css';

export default function CryptoGrid() {
    const { prices, connectionStatus } = useBinance();
    const [searchTerm, setSearchTerm] = useState('');
    const [sortBy, setSortBy] = useState('default');

    const processedCoins = useMemo(() => {
        let coins = TRACKED_SYMBOLS.map((symbol) => {
            const meta = COIN_META[symbol];
            const priceData = prices ? prices[symbol] : undefined;
            return {
                symbol,
                meta,
                priceData,
                name: meta?.name || '',
                coinSymbol: meta?.symbol || '',
                currentPrice: priceData?.currentPrice || 0,
                priceChange: priceData?.priceChangePercentage24h || 0
            };
        });

        if (searchTerm.trim() !== '') {
            const cleanSearch = searchTerm.toLowerCase().trim();
            coins = coins.filter(coin =>
                coin.name.toLowerCase().includes(cleanSearch) ||
                coin.coinSymbol.toLowerCase().includes(cleanSearch)
            );
        }

        if (sortBy === 'price-asc') {
            coins.sort((a, b) => a.currentPrice - b.currentPrice);
        } else if (sortBy === 'price-desc') {
            coins.sort((a, b) => b.currentPrice - a.currentPrice);
        } else if (sortBy === 'change-asc') {
            coins.sort((a, b) => a.priceChange - b.priceChange);
        } else if (sortBy === 'change-desc') {
            coins.sort((a, b) => b.priceChange - a.priceChange);
        }

        return coins;
    }, [prices, searchTerm, sortBy]);

    return (
        <div className="grid-container">
            <div className="grid-status-bar">
                <span>Canlı İzlenen Kripto Paralar ({TRACKED_SYMBOLS.length})</span>
                <span className={`status-badge ${connectionStatus}`}>
                    • {connectionStatus === 'connected' ? 'Canlı Veri Akışı Aktif' : 'Bağlantı Kuruluyor...'}
                </span>
            </div>

            <div className="grid-controls">
                <div className="search-wrapper">
                    <span className="search-icon">🔍</span>
                    <input
                        type="text"
                        placeholder="Coin adı veya sembolü ara... (örn: BTC)"
                        value={searchTerm}
                        onChange={(e) => setSearchTerm(e.target.value)}
                        className="search-input"
                    />
                </div>

                <div className="sort-wrapper">
                    <select
                        value={sortBy}
                        onChange={(e) => setSortBy(e.target.value)}
                        className="sort-select"
                    >
                        <option value="default">Sıralama Seçin (Varsayılan)</option>
                        <option value="price-desc">Fiyata Göre: En Yüksek ↑</option>
                        <option value="price-asc">Fiyata Göre: En Düşük ↓</option>
                        <option value="change-desc">24s Değişime Göre: En Yüksek ↑</option>
                        <option value="change-asc">24s Değişime Göre: En Düşük ↓</option>
                    </select>
                </div>
            </div>

            {processedCoins.length > 0 ? (
                <div className="crypto-grid">
                    {processedCoins.map((coin) => {
                        return (
                            <CryptoCard
                                key={coin.symbol}
                                meta={coin.meta}
                                priceData={coin.priceData}
                            />
                        );
                    })}
                </div>
            ) : (
                <div className="no-results-container">
                    <div className="no-results-icon">🔍❌</div>
                    <h3>Arama Sonucu Bulunamadı</h3>
                    <p>"{searchTerm}" kriterine uygun bir kripto varlık listelenmiyor. Lütfen kelimeyi kontrol edin.</p>
                </div>
            )}
        </div>
    );
}