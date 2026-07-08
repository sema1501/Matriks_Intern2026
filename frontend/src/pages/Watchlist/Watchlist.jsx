import { useWatchlist }    from '../../context/WatchlistContext';
import { useGlobalPrices } from '../../context/PriceContext';
import { COIN_META }       from '../../data/coinMeta';
import CryptoCard          from '../../components/CryptoCard/CryptoCard';
import './Watchlist.css';

export default function Watchlist() {
  const { watchlist, loading } = useWatchlist();
  const { prices }             = useGlobalPrices();

  if (loading) {
    return (
      <div className="watchlist-page">
        <div className="watchlist-page__header">
          <h1 className="watchlist-page__title">⭐ Favorilerim</h1>
        </div>
        <div className="watchlist-skeleton-grid">
          {[1, 2, 3].map(n => (
            <div key={n} className="watchlist-skeleton-card" />
          ))}
        </div>
      </div>
    );
  }

  return (
    <div className="watchlist-page">
      <div className="watchlist-page__header">
        <div>
          <h1 className="watchlist-page__title">⭐ Favorilerim</h1>
          <p className="watchlist-page__subtitle">
            Takip ettiğin {watchlist.length} coin
          </p>
        </div>
      </div>

      {watchlist.length === 0 ? (
        <div className="watchlist-empty">
          <div className="watchlist-empty__icon">☆</div>
          <h2 className="watchlist-empty__title">Henüz favori yok</h2>
          <p className="watchlist-empty__text">
            Ana sayfada coin kartlarındaki yıldıza tıklayarak favorilerine ekleyebilirsin.
          </p>
        </div>
      ) : (
        <div className="watchlist-grid">
          {watchlist.map(item => {
            // Backend'den gelen symbol (BTC, ETH...) ile COIN_META anahtarını eşleştir
            // COIN_META anahtarları BTCUSDT formatında olabilir
            const metaKey =
              Object.keys(COIN_META).find(
                k =>
                  COIN_META[k]?.baseAsset === item.symbol ||
                  COIN_META[k]?.shortSymbol === item.symbol ||
                  k.replace('USDT', '') === item.symbol
              ) || item.symbol + 'USDT';

            const priceKey = Object.keys(prices).find(
              k => k.replace('USDT', '') === item.symbol
            ) || metaKey;

            return (
              <CryptoCard
                key={item.id}
                meta={COIN_META[metaKey] || { symbol: metaKey, name: item.symbol, shortSymbol: item.symbol }}
                priceData={prices[priceKey]}
              />
            );
          })}
        </div>
      )}
    </div>
  );
}
