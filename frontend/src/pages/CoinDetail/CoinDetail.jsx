import { useState } from 'react';
import { useParams, useNavigate, Link } from 'react-router-dom'; 
import { COIN_META } from '../../data/coinMeta'; 
import { useGlobalPrices } from '../../context/PriceContext';
import { useAuth } from '../../context/AuthContext';
import { createAlert } from '../../services/apiService';
import { useCurrency } from '../../context/CurrencyContext';
import './CoinDetail.css';

function getErrorMessage(err) {
  if (err.response?.status === 401 || err.response?.status === 403) {
    return 'Bu işlem için giriş yapmanız gerekiyor.';
  }
  return err.response?.data?.error || err.response?.data?.message || 'Alarm oluşturulamadı.';
}

export default function CoinDetail() {
  const { symbol } = useParams();
  const navigate = useNavigate();
  const { prices } = useGlobalPrices();
  const { user } = useAuth();
  const { formatPrice } = useCurrency();

  const [targetPrice, setTargetPrice] = useState('');
  const [direction, setDirection] = useState('above');
  const [alertInterval, setAlertInterval] = useState(0);
  const [alertLoading, setAlertLoading] = useState(false);
  const [alertError, setAlertError] = useState('');
  const [alertSuccess, setAlertSuccess] = useState('');

  const fullSymbol = symbol.toUpperCase().endsWith('USDT') 
    ? symbol.toUpperCase() 
    : `${symbol.toUpperCase()}USDT`;
  const meta = COIN_META[fullSymbol];
  const coinData = prices?.[fullSymbol];

  const handleAlertSubmit = async (e) => {
    e.preventDefault();
    setAlertError('');
    setAlertSuccess('');

    if (!user) {
      setAlertError('Alarm kurmak için giriş yapmanız gerekiyor.');
      return;
    }

    const price = Number(targetPrice);
    if (!targetPrice || !Number.isFinite(price) || price <= 0) {
      setAlertError('Geçerli bir hedef fiyat girin (pozitif sayı).');
      return;
    }

    if (direction !== 'above' && direction !== 'below') {
      setAlertError('Yön seçimi geçersiz.');
      return;
    }

    if (Number(alertInterval) !== 0) {
      setAlertError('Bu aralık henüz desteklenmiyor. Dakikalık seçin.');
      return;
    }

    setAlertLoading(true);
    try {
      await createAlert({
        symbol: fullSymbol,
        targetPrice: price,
        direction: direction === 'above' ? 0 : 1,
        interval: 0,
      });
      setAlertSuccess('Alarm başarıyla oluşturuldu.');
      setTargetPrice('');
      setAlertInterval(0);
      window.dispatchEvent(new Event('alerts-changed'));
    } catch (err) {
      setAlertError(getErrorMessage(err));
    } finally {
      setAlertLoading(false);
    }
  };

  
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
              <span className="stat-value">{formatPrice(coinData.currentPrice)}</span>            
            </div>
            
            <div className="stat-box">
              <span className="stat-label">24S Değişim</span>
              <span className={`stat-value ${isPositive ? 'positive' : 'negative'}`}>
                {isPositive ? '+' : ''}{change24h.toFixed(2)}%
              </span>
            </div>

            <div className="stat-box">
              <span className="stat-label">24S Yüksek</span>
              <span className="stat-value">{formatPrice(coinData.high24h)}</span>
            </div>

            <div className="stat-box">
              <span className="stat-label">24S Düşük</span>
              <span className="stat-value">{formatPrice(coinData.low24h)}</span>
            </div>
          </div>
      ) : (
        <p style={{ margin: '40px 0', color: '#64748b' }}>Canlı piyasa verisi bekleniyor...</p>
      )}

      <section className="alarm-section">
        <h2 className="alarm-title">Alarm Kur</h2>
        {!user && (
          <p className="alarm-hint">
            Alarm kurmak için <Link to="/signin">giriş yapın</Link>.
          </p>
        )}
        {alertError && <p className="alarm-message alarm-message--error">{alertError}</p>}
        {alertSuccess && <p className="alarm-message alarm-message--success">{alertSuccess}</p>}
        <form onSubmit={handleAlertSubmit} className="alarm-form">
          <div className="alarm-field">
            <label htmlFor="targetPrice">Hedef Fiyat (USD)</label>
            <input
              id="targetPrice"
              type="number"
              min="0"
              step="any"
              value={targetPrice}
              onChange={(e) => setTargetPrice(e.target.value)}
              placeholder={coinData ? String(coinData.currentPrice) : '0.00'}
              disabled={!user || alertLoading}
              className="alarm-input"
            />
          </div>
          <div className="alarm-field">
            <label htmlFor="direction">Yön</label>
            <select
              id="direction"
              value={direction}
              onChange={(e) => setDirection(e.target.value)}
              disabled={!user || alertLoading}
              className="alarm-input"
            >
              <option value="above">Yukarı (fiyat hedefin üstüne çıkınca)</option>
              <option value="below">Aşağı (fiyat hedefin altına inince)</option>
            </select>
          </div>
          <div className="alarm-field">
            <label htmlFor="alertInterval">Kontrol Sıklığı</label>
            <select
              id="alertInterval"
              value={alertInterval}
              onChange={(e) => setAlertInterval(Number(e.target.value))}
              disabled={!user || alertLoading}
              className="alarm-input"
            >
              <option value={0}>Dakikalık</option>
              <option value={1} disabled>
                Saatlik (Yakında)
              </option>
              <option value={2} disabled>
                Günlük (Yakında)
              </option>
            </select>
          </div>
          <button
            type="submit"
            className="btn-alarm"
            disabled={!user || alertLoading}
          >
            {alertLoading ? 'Kaydediliyor...' : 'Alarm Kur'}
          </button>
        </form>
      </section>

      <button className="btn-back" onClick={() => navigate('/')}>Listeye Dön</button>
    </div>
  );
}