import { useState, useEffect } from 'react';
import { useParams, useNavigate } from 'react-router-dom'; 
import { COIN_META } from '../../data/coinMeta'; 
import { useGlobalPrices } from '../../context/PriceContext';
import './CoinDetail.css';

export default function CoinDetail() {
  const { symbol } = useParams();
  const navigate = useNavigate();
  const { prices } = useGlobalPrices();

  const [targetPrice, setTargetPrice] = useState('');
  const [direction, setDirection] = useState('Above');
  const [activeAlert, setActiveAlert] = useState(null);

  const fullSymbol = symbol.toUpperCase().endsWith('USDT') 
    ? symbol.toUpperCase() 
    : `${symbol.toUpperCase()}USDT`;
  const meta = COIN_META[fullSymbol];
  const coinData = prices?.[fullSymbol];

  // --- CANLI FİYAT TAKİP MEKANİZMASI ---
  useEffect(() => {
    if (activeAlert && coinData?.currentPrice) {
      let rawPrice = String(coinData.currentPrice);
      
      // Projedeki binlik nokta ve ondalık virgül formatını güvenli sayıya çevirme
      if (rawPrice.includes('.') && rawPrice.includes(',')) {
        rawPrice = rawPrice.replace(/\./g, '').replace(',', '.');
      } else if (rawPrice.includes(',')) {
        rawPrice = rawPrice.replace(',', '.');
      }

      const currentPriceNum = parseFloat(rawPrice);
      const targetPriceNum = parseFloat(activeAlert.target);

      if (!isNaN(currentPriceNum) && !isNaN(targetPriceNum)) {
        if (activeAlert.dir === 'Above' && currentPriceNum >= targetPriceNum) {
          alert(`🚨 ALARM TETİKLENDİ! ${fullSymbol} hedeflediğin ${targetPriceNum} USDT seviyesinin ÜSTÜNE ÇIKTI! Güncel: ${coinData.currentPrice} USDT`);
          setActiveAlert(null);
        } 
        else if (activeAlert.dir === 'Below' && currentPriceNum <= targetPriceNum) {
          alert(`🚨 ALARM TETİKLENDİ! ${fullSymbol} hedeflediğin ${targetPriceNum} USDT seviyesinin ALTINA DÜŞTÜ! Güncel: ${coinData.currentPrice} USDT`);
          setActiveAlert(null);
        }
      }
    }
  }, [coinData?.currentPrice, activeAlert, fullSymbol]);

  const handleCreateAlert = (e) => {
    e.preventDefault();
    if (!targetPrice || Number(targetPrice) <= 0) {
      alert("Lütfen geçerli bir hedef fiyat girin kanka!");
      return;
    }
    
    setActiveAlert({
      target: targetPrice,
      dir: direction
    });

    alert(`${fullSymbol} için ${targetPrice} USDT seviyesine alarm kuruldu ve takibe alındı! 🚀`);
    setTargetPrice('');
  };
  
  if (!meta) {
    return (
      <div className="coin-detail-container">
         <h1 className="coin-detail-title">Coin Bulunamadı</h1>
         <button className="btn-back" onClick={() => navigate('/')}>Listeye Dön</button>
      </div>
    );
  }

  const change24h = coinData ? Number(coinData.priceChangePercentage24h) : 0;
  const isPositive = change24h >= 0;

  return (
    <div className="coin-detail-container" style={{ paddingBottom: '40px' }}>
      
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

      {/* --- ALARM PANELİ --- */}
      <div className="stat-box" style={{ marginTop: '20px', marginBottom: '20px', textAlign: 'left', width: '100%', boxSizing: 'border-box' }}>
        <h3 style={{ marginBottom: '15px' }} className="coin-detail-title">Fiyat Alarmı Kur</h3>
        
        {activeAlert && (
          <p style={{ color: '#38bdf8', fontSize: '14px', marginBottom: '15px' }}>
            ⏳ Şuan aktif takip edilen: <b>{activeAlert.target} USDT</b> ({activeAlert.dir === 'Above' ? 'Üstü' : 'Altı'})
          </p>
        )}

        <form onSubmit={handleCreateAlert}>
          <div style={{ marginBottom: '15px' }}>
            <label style={{ display: 'block', marginBottom: '5px', opacity: 0.8 }}>Hedef Fiyat (USDT)</label>
            <input 
              type="number" 
              step="any"
              placeholder="Örn: 65000" 
              value={targetPrice}
              onChange={(e) => setTargetPrice(e.target.value)}
              style={{ width: '100%', padding: '10px', backgroundColor: 'transparent', color: 'inherit', border: '1px solid currentColor', borderRadius: '6px', boxSizing: 'border-box', opacity: 0.9 }}
            />
          </div>

          <div style={{ marginBottom: '20px' }}>
            <label style={{ display: 'block', marginBottom: '5px', opacity: 0.8 }}>Yön Seçimi</label>
            <select 
              value={direction}
              onChange={(e) => setDirection(e.target.value)}
              style={{ width: '100%', padding: '10px', backgroundColor: 'transparent', color: 'inherit', border: '1px solid currentColor', borderRadius: '6px', boxSizing: 'border-box', opacity: 0.9 }}
            >
              <option value="Above" style={{ backgroundColor: 'var(--bg-color, #1e293b)', color: 'inherit' }}>Anlık Fiyat Üstüne Çıkınca (Above)</option>
              <option value="Below" style={{ backgroundColor: 'var(--bg-color, #1e293b)', color: 'inherit' }}>Anlık Fiyat Altına Düşünce (Below)</option>
            </select>
          </div>

          <button type="submit" className="btn-back" style={{ width: '100%', margin: '0', padding: '12px', fontWeight: 'bold' }}>
            Alarmı Kaydet
          </button>
        </form>
      </div>

      <button className="btn-back" onClick={() => navigate('/')}>Listeye Dön</button>
    </div>
  );
}