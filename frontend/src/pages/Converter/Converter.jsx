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

  const [coinSearch, setCoinSearch] = useState('');
  const [amount, setAmount] = useState('1');

  const filteredCoinOptions = useMemo(() => {
    const searchValue = coinSearch.trim().toLowerCase();

    if (!searchValue) {
      return coinOptions;
    }

    return coinOptions.filter((coin) => {
      const name = coin.name?.toLowerCase() || '';
      const shortSymbol = coin.symbol?.toLowerCase() || '';
      const fullSymbol = coin.fullSymbol?.toLowerCase() || '';

      return (
        name.includes(searchValue) ||
        shortSymbol.includes(searchValue) ||
        fullSymbol.includes(searchValue)
      );
    });
  }, [coinOptions, coinSearch]);

  function handleSelectCoin(symbol) {
    if (!symbol) return;

    setSelectedSymbol(symbol);
    setCoinSearch('');
  }

  function handleSearchKeyDown(event) {
    if (event.key !== 'Enter') return;

    event.preventDefault();

    if (filteredCoinOptions.length > 0) {
      handleSelectCoin(filteredCoinOptions[0].fullSymbol);
    }
  }

  const isSelectedSymbolVisible = filteredCoinOptions.some(
    (coin) => coin.fullSymbol === selectedSymbol
  );

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
    currentPrice !== null && isAmountValid
      ? numericAmount * currentPrice
      : null;

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
            <span>Coin Ara ve Seç</span>

            <input
              type="text"
              value={coinSearch}
              onChange={(event) => setCoinSearch(event.target.value)}
              onKeyDown={handleSearchKeyDown}
              placeholder="BTC, ETH, Bitcoin, Ethereum..."
              className="converter-search-input"
            />

            <select
              value={
                isSelectedSymbolVisible
                  ? selectedSymbol
                  : filteredCoinOptions[0]?.fullSymbol || ''
              }
              disabled={filteredCoinOptions.length === 0}
              onChange={(event) => handleSelectCoin(event.target.value)}
            >
              {filteredCoinOptions.length > 0 ? (
                filteredCoinOptions.map((coin, index) => (
                  <option key={coin.fullSymbol} value={coin.fullSymbol}>
                    {coin.name} ({coin.symbol})
                    {index === 0 && coinSearch.trim() ? ' - İlk sonuç' : ''}
                  </option>
                ))
              ) : (
                <option value="">Sonuç bulunamadı</option>
              )}
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

        {coinSearch.trim() && filteredCoinOptions.length > 0 && (
          <p className="converter-message">
            Enter’a basarak ilk sonucu seçebilirsin:{' '}
            <strong>
              {filteredCoinOptions[0].name} ({filteredCoinOptions[0].symbol})
            </strong>
          </p>
        )}

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