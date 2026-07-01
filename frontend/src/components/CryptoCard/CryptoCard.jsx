import { useEffect, useRef, useState } from 'react';
import './CryptoCard.css';

function getNumberValue(source, keys) {
  if (!source) return null;

  for (const key of keys) {
    const value = source[key];

    if (value !== undefined && value !== null && value !== '') {
      const numberValue = Number(value);

      if (!Number.isNaN(numberValue)) {
        return numberValue;
      }
    }
  }

  return null;
}

function formatUsd(value) {
  if (value === null || value === undefined) return '-';

  return new Intl.NumberFormat('en-US', {
    style: 'currency',
    currency: 'USD',
    minimumFractionDigits: value >= 1 ? 2 : 4,
    maximumFractionDigits: value >= 1 ? 2 : 6,
  }).format(value);
}

function formatPercent(value) {
  if (value === null || value === undefined) return '-';
  return `${value > 0 ? '+' : ''}${value.toFixed(2)}%`;
}

function getAccentClass(symbol = '') {
  const upperSymbol = symbol.toUpperCase();

  if (upperSymbol.includes('BTC')) return 'crypto-card--btc';
  if (upperSymbol.includes('ETH')) return 'crypto-card--eth';
  if (upperSymbol.includes('SOL')) return 'crypto-card--sol';
  if (upperSymbol.includes('BNB')) return 'crypto-card--bnb';
  if (upperSymbol.includes('AVAX')) return 'crypto-card--avax';
  if (upperSymbol.includes('DOT')) return 'crypto-card--dot';

  return 'crypto-card--default';
}

function CryptoCardSkeleton() {
  return (
    <article className="crypto-card crypto-card--skeleton">
      <div className="crypto-card__shine" />

      <div className="crypto-card__header">
        <div className="crypto-card__skeleton-logo" />

        <div className="crypto-card__skeleton-info">
          <span className="crypto-card__skeleton-line crypto-card__skeleton-line--title" />
          <span className="crypto-card__skeleton-line crypto-card__skeleton-line--symbol" />
        </div>
      </div>

      <div className="crypto-card__skeleton-price" />

      <div className="crypto-card__footer">
        <span className="crypto-card__skeleton-line crypto-card__skeleton-line--label" />
        <span className="crypto-card__skeleton-pill" />
      </div>
    </article>
  );
}

export default function CryptoCard({ meta, priceData }) {
  const [flashClass, setFlashClass] = useState('');
  const previousPriceRef = useRef(null);
  const flashTimeoutRef = useRef(null);

  const currentPrice = getNumberValue(priceData, [
    'currentPrice',
    'price',
    'lastPrice',
    'c',
  ]);

  const changePercent = getNumberValue(priceData, [
    'priceChangePercentage24h',
    'priceChangePercent',
    'changePercent',
    'P',
  ]);

  useEffect(() => {
    if (currentPrice === null) return;

    const previousPrice = previousPriceRef.current;

    if (previousPrice !== null && previousPrice !== currentPrice) {
      const nextFlashClass =
        currentPrice > previousPrice
          ? 'crypto-card--flash-up'
          : 'crypto-card--flash-down';

      setFlashClass(nextFlashClass);

      if (flashTimeoutRef.current) {
        clearTimeout(flashTimeoutRef.current);
      }

      flashTimeoutRef.current = setTimeout(() => {
        setFlashClass('');
      }, 700);
    }

    previousPriceRef.current = currentPrice;
  }, [currentPrice]);

  useEffect(() => {
    return () => {
      if (flashTimeoutRef.current) {
        clearTimeout(flashTimeoutRef.current);
      }
    };
  }, []);

  if (!priceData || currentPrice === null) {
    return <CryptoCardSkeleton />;
  }

  const symbol = meta?.symbol || priceData?.symbol || 'UNKNOWN';
  const name = meta?.name || meta?.coinName || symbol;
  const logo = meta?.logo || meta?.logoUrl || meta?.icon || meta?.image;
  const shortSymbol =
    meta?.shortSymbol || meta?.baseAsset || symbol.replace('USDT', '');

  const isPositive = changePercent !== null && changePercent >= 0;
  const accentClass = getAccentClass(symbol);

  return (
    <article className={`crypto-card ${accentClass} ${flashClass}`}>
      <div className="crypto-card__shine" />

      <div className="crypto-card__header">
        <div className="crypto-card__logo-wrapper">
          {logo ? (
            <img className="crypto-card__logo" src={logo} alt={`${name} logo`} />
          ) : (
            <span className="crypto-card__logo-fallback">
              {shortSymbol.charAt(0)}
            </span>
          )}
        </div>

        <div className="crypto-card__info">
          <h3 className="crypto-card__name">{name}</h3>
          <span className="crypto-card__symbol">{symbol}</span>
        </div>
      </div>

      <div className="crypto-card__price-area">
        <span className="crypto-card__label">Anlık Fiyat</span>
        <strong className="crypto-card__price">{formatUsd(currentPrice)}</strong>
      </div>

      <div className="crypto-card__footer">
        <div>
          <span className="crypto-card__footer-title">24s Değişim</span>
          <span className="crypto-card__footer-subtitle">Canlı Binance verisi</span>
        </div>

        <span
          className={
            isPositive
              ? 'crypto-card__change crypto-card__change--positive'
              : 'crypto-card__change crypto-card__change--negative'
          }
        >
          <span className="crypto-card__change-icon">
            {isPositive ? '↑' : '↓'}
          </span>
          {formatPercent(changePercent)}
        </span>
      </div>
    </article>
  );
}