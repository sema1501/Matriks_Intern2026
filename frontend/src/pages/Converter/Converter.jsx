import { useMemo, useState } from 'react';
import { COIN_META } from '../../data/coinMeta';
import { useGlobalPrices } from '../../context/PriceContext';
import { useCurrency } from '../../context/CurrencyContext';
import './Converter.css';

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

export default function Converter() {
  const { prices } = useGlobalPrices();
  const {
    usdTryRate,
    rateLoading,
    rateError,
    formatUsd,
    formatTry,
  } = useCurrency();

  const coinOptions = useMemo(() => {
    return Object.entries(COIN_META).map(([fullSymbol, meta]) => ({
      fullSymbol,
      name: meta.name,
      symbol: meta.symbol,
      image: meta.image,
    }));
  }, []);

  const [selectedSymbol, setSelectedSymbol] = useState(
    coinOptions[0]?.fullSymbol || ''
  );
  const [amount, setAmount] = useState('1');

  const selectedMeta = COIN_META[selectedSymbol];
  const selectedPriceData = prices?.[selectedSymbol];

  const currentPrice = getNumberValue(selectedPriceData, [
    'currentPrice',
    'price',
    'lastPrice',
    'c',
  ]);

  const numericAmount = Number(amount);
  const isAmountValid = amount !== '' && numericAmount > 0;

  const totalUsd =
    currentPrice !== null && isAmountValid ? numericAmount * currentPrice : null;

  const totalTry = totalUsd !== null ? totalUsd * usdTryRate : null;

  return (
    <div className="converter-page">
      <section className="converter-card">
        <div className="converter-header">
          <span className="converter-eyebrow">Kripto Dönüştürücü</span>
          <h1>Coin miktarını USD ve TRY değerine çevir</h1>
          <p>
            Canlı fiyat verisi üzerinden seçtiğin coin miktarının yaklaşık
            karşılığını hesapla.
          </p>
        </div>

        <div className="converter-form">
          <label className="converter-field">
            <span>Coin Seç</span>
            <select
              value={selectedSymbol}
              onChange={(event) => setSelectedSymbol(event.target.value)}
            >
              {coinOptions.map((coin) => (
                <option key={coin.fullSymbol} value={coin.fullSymbol}>
                  {coin.name} ({coin.symbol})
                </option>
              ))}
            </select>
          </label>

          <label className="converter-field">
            <span>Miktar</span>
            <input
              type="number"
              min="0"
              step="any"
              value={amount}
              onChange={(event) => setAmount(event.target.value)}
              placeholder="Örn: 1.5"
            />
          </label>
        </div>

        {!isAmountValid && (
          <p className="converter-message error">
            Lütfen sıfırdan büyük geçerli bir miktar gir.
          </p>
        )}

        {currentPrice === null && (
          <p className="converter-message error">
            Seçilen coin için fiyat verisi henüz gelmedi.
          </p>
        )}

        {rateLoading && (
          <p className="converter-message">Kur bilgisi yükleniyor...</p>
        )}

        {rateError && (
          <p className="converter-message warning">{rateError}</p>
        )}

        <div className="converter-selected">
          {selectedMeta?.image && (
            <img
              src={selectedMeta.image}
              alt={selectedMeta.name}
              className="converter-coin-image"
            />
          )}

          <div>
            <strong>{selectedMeta?.name || selectedSymbol}</strong>
            <span>{selectedSymbol}</span>
          </div>
        </div>

        <div className="converter-results">
          <div className="converter-result-box">
            <span>Birim Fiyat</span>
            <strong>
              {currentPrice !== null ? formatUsd(currentPrice) : '-'}
            </strong>
          </div>

          <div className="converter-result-box">
            <span>Toplam USD</span>
            <strong>{totalUsd !== null ? formatUsd(totalUsd) : '-'}</strong>
          </div>

          <div className="converter-result-box">
            <span>Toplam TRY</span>
            <strong>{totalTry !== null ? formatTry(totalTry) : '-'}</strong>
          </div>

          <div className="converter-result-box">
            <span>USD/TRY Kuru</span>
            <strong>{usdTryRate.toFixed(4)}</strong>
          </div>
        </div>
      </section>
    </div>
  );
}