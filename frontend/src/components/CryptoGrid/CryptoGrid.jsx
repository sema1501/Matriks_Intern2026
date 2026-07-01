import React from 'react';
import { useBinancePrices } from '../../hooks/useBinancePrices';
import { COIN_META } from '../../data/coinMeta';
import { TRACKED_SYMBOLS } from '../../data/trackedSymbols';
import CryptoCard from '../CryptoCard/CryptoCard';
import './CryptoGrid.css';

export default function CryptoGrid() {
  // Binance canlı bağlantısından gelen verileri ve bağlantı durumunu alıyoruz
  const { prices, connectionStatus } = useBinancePrices();

  return (
    <div className="crypto-grid-container">
      {/* Üst Kısım: Başlık ve Bağlantı Durumu */}
      <div className="grid-header">
        <h2>Canlı Kripto Para Fiyatları</h2>
        <div className="connection-status">
          <span className={`status-dot ${connectionStatus}`}></span>
          <span className="status-text">
            {connectionStatus === 'connected' && 'Canlı Bağlantı Aktif'}
            {connectionStatus === 'connecting' && 'Bağlantı Kuruluyor...'}
            {connectionStatus === 'disconnected' && 'Bağlantı Koptu'}
          </span>
        </div>
      </div>

      {/* Alt Kısım: Kartların Listeleneceği Izgara (Grid) Kutusu */}
      <div className="crypto-grid">
        {TRACKED_SYMBOLS.map((symbol) => {
          // Her sembol için statik bilgileri (isim, logo) çekiyoruz
          const meta = COIN_META[symbol];
          
          // Her sembol için Binance'ten gelen anlık canlı fiyatı çekiyoruz
          const priceData = prices[symbol];
          
          return (
            <CryptoCard
              key={symbol}
              meta={meta}
              priceData={priceData}
            />
          );
        })}
      </div>
    </div>
  );
}