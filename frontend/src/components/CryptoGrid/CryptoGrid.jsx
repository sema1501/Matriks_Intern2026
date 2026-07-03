import React, { useMemo, useState } from 'react';
import { useBinancePrices } from '../../hooks/useBinancePrices';
import { COIN_META } from '../../data/coinMeta';
import { TRACKED_SYMBOLS } from '../../data/trackedSymbols';
import CryptoCard from '../CryptoCard/CryptoCard';
import './CryptoGrid.css';

const SORT_OPTIONS = [
  { value: 'price-asc', label: 'Fiyat (Düşük → Yüksek)' },
  { value: 'price-desc', label: 'Fiyat (Yüksek → Düşük)' },
  { value: 'change-asc', label: '24s Değişim (Düşük → Yüksek)' },
  { value: 'change-desc', label: '24s Değişim (Yüksek → Düşük)' },
];

function getPriceValue(priceData) {
  if (!priceData) return null;
  const num = Number(priceData.currentPrice ?? priceData.price);
  return Number.isFinite(num) ? num : null;
}

function getChangeValue(priceData) {
  if (!priceData) return null;
  const num = Number(
    priceData.priceChangePercentage24h ?? priceData.priceChangePercent
  );
  return Number.isFinite(num) ? num : null;
}

function compareNumeric(a, b, ascending) {
  const aMissing = a === null;
  const bMissing = b === null;
  if (aMissing && bMissing) return 0;
  if (aMissing) return 1;
  if (bMissing) return -1;
  return ascending ? a - b : b - a;
}

export default function CryptoGrid() {
  const { prices, connectionStatus } = useBinancePrices();
  const [searchTerm, setSearchTerm] = useState('');
  const [sortOption, setSortOption] = useState('price-desc');

  const filteredAndSortedSymbols = useMemo(() => {
    const term = searchTerm.trim().toLowerCase();

    const filtered = TRACKED_SYMBOLS.filter((symbol) => {
      if (!term) return true;

      const meta = COIN_META[symbol];
      if (!meta) return symbol.toLowerCase().includes(term);

      const name = (meta.name || '').toLowerCase();
      const coinSymbol = (meta.symbol || '').toLowerCase();
      return name.includes(term) || coinSymbol.includes(term);
    });

    const sorted = [...filtered].sort((symbolA, symbolB) => {
      const priceA = getPriceValue(prices[symbolA]);
      const priceB = getPriceValue(prices[symbolB]);
      const changeA = getChangeValue(prices[symbolA]);
      const changeB = getChangeValue(prices[symbolB]);

      switch (sortOption) {
        case 'price-asc':
          return compareNumeric(priceA, priceB, true);
        case 'price-desc':
          return compareNumeric(priceA, priceB, false);
        case 'change-asc':
          return compareNumeric(changeA, changeB, true);
        case 'change-desc':
          return compareNumeric(changeA, changeB, false);
        default:
          return 0;
      }
    });

    return sorted;
  }, [searchTerm, sortOption, prices]);

  return (
    <div className="crypto-grid-container">
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

      <div className="grid-toolbar">
        <div className="grid-search">
          <span className="grid-search__icon" aria-hidden="true">⌕</span>
          <input
            type="search"
            className="grid-search__input"
            placeholder="Coin ara (isim veya sembol)..."
            value={searchTerm}
            onChange={(e) => setSearchTerm(e.target.value)}
            aria-label="Coin ara"
          />
        </div>

        <div className="grid-sort">
          <label className="grid-sort__label" htmlFor="crypto-sort">
            Sırala
          </label>
          <select
            id="crypto-sort"
            className="grid-sort__select"
            value={sortOption}
            onChange={(e) => setSortOption(e.target.value)}
          >
            {SORT_OPTIONS.map((option) => (
              <option key={option.value} value={option.value}>
                {option.label}
              </option>
            ))}
          </select>
        </div>
      </div>

      {filteredAndSortedSymbols.length === 0 ? (
        <div className="grid-empty" role="status">
          <div className="grid-empty__icon" aria-hidden="true">∅</div>
          <h3 className="grid-empty__title">Sonuç bulunamadı</h3>
          <p className="grid-empty__text">
            &ldquo;{searchTerm.trim()}&rdquo; için eşleşen coin yok.
            Farklı bir isim veya sembol deneyin.
          </p>
        </div>
      ) : (
        <div className="crypto-grid">
          {filteredAndSortedSymbols.map((symbol) => (
            <CryptoCard
              key={symbol}
              meta={COIN_META[symbol]}
              priceData={prices[symbol]}
            />
          ))}
        </div>
      )}
    </div>
  );
}
