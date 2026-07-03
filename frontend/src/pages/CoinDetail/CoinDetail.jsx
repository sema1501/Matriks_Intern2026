import { useParams, useNavigate } from 'react-router-dom'; 
import { COIN_META } from '../../data/coinMeta'; 
import { useGlobalPrices } from '../../context/PriceContext';
import './CoinDetail.css';

export default function CoinDetail() {
  const { symbol } = useParams();
  const navigate = useNavigate();
  const { prices } = useGlobalPrices();
  
  const fullSymbol = symbol.toUpperCase().endsWith('USDT') 
    ? symbol.toUpperCase() 
    : `${symbol.toUpperCase()}USDT`;
  const meta = COIN_META[fullSymbol];
  const coinData = prices?.[fullSymbol];

  
  if (!meta) {
    return (
      <div className="coin-detail-container">
         <h1 className="coin-detail-title">Coin Bulunamadı</h1>
         <p style={{ marginTop: '15px', color: '#64748b' }}>"{symbol}" şu an takip ettiğimiz coinler arasında yer almıyor.</p>
         <button className="btn-back" onClick={() => navigate('/')} style={{ marginTop: '20px' }}>Listeye Dön</button>
      </div>
    );
  }
  const change24h = coinData ? Number(coinData.priceChangePercentage24h) : 0;
  const isPositive = change24h >= 0;

  return (
    <div className="coin-detail-container">
      
      <div className="coin-detail-header">
        {meta.image && <img src={meta.image} alt={meta.name} width="56" height="56" />}
        <h1 className="coin-detail-title">{meta.name} ({meta.symbol})</h1>
      </div>

      {coinData ? (
          <div className="coin-detail-grid">
            <div className="stat-box">
              <span className="stat-label">Anlık Fiyat</span>
              <span className="stat-value">${Number(coinData.currentPrice).toLocaleString(undefined, {minimumFractionDigits: 2, maximumFractionDigits: 6})}</span>
            </div>
            
            <div className="stat-box">
              <span className="stat-label">24S Değişim</span>
              <span className={`stat-value ${isPositive ? 'positive' : 'negative'}`}>
                {isPositive ? '+' : ''}{change24h.toFixed(2)}%
              </span>
            </div>

            <div className="stat-box">
              <span className="stat-label">24S Yüksek</span>
              <span className="stat-value">{Number(coinData.high24h).toLocaleString()}</span>
            </div>

            <div className="stat-box">
              <span className="stat-label">24S Düşük</span>
              <span className="stat-value">{Number(coinData.low24h).toLocaleString()}</span>
            </div>
          </div>
      ) : (
        <p style={{ margin: '40px 0', color: '#64748b' }}>Canlı piyasa verisi bekleniyor...</p>
      )}

      <button className="btn-back" onClick={() => navigate('/')}>Listeye Dön</button>
    </div>
  );
}