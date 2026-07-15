import { useState, useEffect } from 'react';
import { useParams, useNavigate, Link } from 'react-router-dom';
import { COIN_META } from '../../data/coinMeta';
import { useGlobalPrices } from '../../context/PriceContext';
import { useAuth } from '../../context/AuthContext';
import { createAlert } from '../../services/apiService';
import { useCurrency } from '../../context/CurrencyContext';
import ChartModule from '../../components/ChartModule/ChartModule';
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
    const [alertLoading, setAlertLoading] = useState(false);
    const [alertError, setAlertError] = useState('');
    const [alertSuccess, setAlertSuccess] = useState('');

    const [isMobile, setIsMobile] = useState(window.innerWidth <= 768);

    const [isEmaActive, setIsEmaActive] = useState(false);
    const [isRsiActive, setIsRsiActive] = useState(false);
    const [emaPeriod, setEmaPeriod] = useState(12);

    useEffect(() => {
        const handleResize = () => {
            setIsMobile(window.innerWidth <= 768);
        };
        window.addEventListener('resize', handleResize);
        return () => window.removeEventListener('resize', handleResize);
    }, []);

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

        setAlertLoading(true);
        try {
            await createAlert({
                symbol: fullSymbol,
                targetPrice: price,
                direction: direction === 'above' ? 0 : 1,
            });
            setAlertSuccess('Alarm başarıyla oluşturuldu.');
            setTargetPrice('');
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
        <div style={{ maxWidth: '1400px', margin: '0 auto', padding: '20px', display: 'flex', flexDirection: 'column', gap: '20px' }}>

            <div style={{
                display: 'grid',
                gridTemplateColumns: isMobile ? '1fr' : '1fr 2fr',
                gap: '20px',
                alignItems: 'start'
            }}>

                <div style={{ display: 'flex', flexDirection: 'column', gap: '20px', width: '100%' }}>

                    <div className="coin-detail-container" style={{ margin: 0, width: '100%' }}>
                        <div className="coin-detail-header" style={{ marginBottom: '20px' }}>
                            {meta.image && <img src={meta.image} alt={meta.name} width="56" height="56" />}
                            <h1 className="coin-detail-title" style={{ fontSize: '24px' }}>{meta.name} ({meta.symbol})</h1>
                        </div>

                        {coinData ? (
                            <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '12px' }}>
                                <div className="stat-box" style={{ padding: '12px' }}>
                                    <span className="stat-label" style={{ fontSize: '11px' }}>Anlık Fiyat</span>
                                    <span className="stat-value" style={{ fontSize: '16px' }}>{formatPrice(coinData.currentPrice)}</span>
                                </div>

                                <div className="stat-box" style={{ padding: '12px' }}>
                                    <span className="stat-label" style={{ fontSize: '11px' }}>24S Değişim</span>
                                    <span className={`stat-value ${isPositive ? 'positive' : 'negative'}`} style={{ fontSize: '16px' }}>
                                        {isPositive ? '+' : ''}{change24h.toFixed(2)}%
                                    </span>
                                </div>

                                <div className="stat-box" style={{ padding: '12px' }}>
                                    <span className="stat-label" style={{ fontSize: '11px' }}>24S Yüksek</span>
                                    <span className="stat-value" style={{ fontSize: '16px' }}>{formatPrice(coinData.high24h)}</span>
                                </div>

                                <div className="stat-box" style={{ padding: '12px' }}>
                                    <span className="stat-label" style={{ fontSize: '11px' }}>24S Düşük</span>
                                    <span className="stat-value" style={{ fontSize: '16px' }}>{formatPrice(coinData.low24h)}</span>
                                </div>
                            </div>
                        ) : (
                            <p style={{ color: '#64748b' }}>Canlı piyasa verisi bekleniyor...</p>
                        )}
                    </div>

                    <section className="alarm-section" style={{ margin: 0, width: '100%' }}>
                        <h2 className="alarm-title" style={{ fontSize: '20px', marginBottom: '15px' }}>Alarm Kur</h2>
                        {!user && (
                            <p className="alarm-hint" style={{ marginBottom: '15px' }}>
                                Alarm kurmak için <Link to="/signin">giriş yapın</Link>.
                            </p>
                        )}
                        {alertError && <p className="alarm-message alarm-message--error">{alertError}</p>}
                        {alertSuccess && <p className="alarm-message alarm-message--success">{alertSuccess}</p>}
                        <form onSubmit={handleAlertSubmit} className="alarm-form" style={{ display: 'flex', flexDirection: 'column', gap: '12px' }}>
                            <div className="alarm-field" style={{ margin: 0 }}>
                                <label htmlFor="targetPrice" style={{ fontSize: '12px', marginBottom: '4px' }}>Hedef Fiyat (USD)</label>
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
                            <div className="alarm-field" style={{ margin: 0 }}>
                                <label htmlFor="direction" style={{ fontSize: '12px', marginBottom: '4px' }}>Yön</label>
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
                            <button
                                type="submit"
                                className="btn-alarm"
                                disabled={!user || alertLoading}
                                style={{ width: '100%', marginTop: '5px' }}
                            >
                                {alertLoading ? 'Kaydediliyor...' : 'Alarm Kur'}
                            </button>
                        </form>
                    </section>

                    {!isMobile && (
                        <div style={{ alignSelf: 'flex-start' }}>
                            <button className="btn-back" onClick={() => navigate('/')} style={{ margin: 0 }}>
                                Listeye Dön
                            </button>
                        </div>
                    )}

                </div>

                <div style={{ width: '100%', overflow: 'hidden', display: 'flex', flexDirection: 'column', gap: '10px' }}>
                    
                   
                    <div style={{
                        display: 'flex',
                        alignItems: 'center',
                        gap: '15px',
                        padding: '10px 15px',
                        backgroundColor: '#f8fafc',
                        borderRadius: '8px',
                        border: '1px solid #e2e8f0',
                        width: 'fit-content'
                    }}>
                        <span style={{ fontWeight: '600', fontSize: '13px', color: '#475569' }}>İndikatörler:</span>
                        
                        <label style={{ display: 'flex', alignItems: 'center', gap: '6px', fontSize: '13px', cursor: 'pointer', color: '#334155' }}>
                            <input 
                                type="checkbox" 
                                checked={isEmaActive} 
                                onChange={(e) => setIsEmaActive(e.target.checked)} 
                            />
                            EMA
                        </label>

                        {isEmaActive && (
                            <input 
                                type="number" 
                                value={emaPeriod} 
                                onChange={(e) => setEmaPeriod(Number(e.target.value))}
                                min="1"
                                max="200"
                                style={{
                                    width: '50px',
                                    padding: '2px 6px',
                                    border: '1px solid #cbd5e1',
                                    borderRadius: '4px',
                                    fontSize: '12px',
                                    outline: 'none'
                                }}
                            />
                        )}

                        <label style={{ display: 'flex', alignItems: 'center', gap: '6px', fontSize: '13px', cursor: 'pointer', color: '#334155' }}>
                            <input 
                                type="checkbox" 
                                checked={isRsiActive} 
                                onChange={(e) => setIsRsiActive(e.target.checked)} 
                            />
                            RSI
                        </label>
                    </div>
                    

                    
                    <ChartModule 
                        symbol={fullSymbol} 
                        isEmaActive={isEmaActive}
                        isRsiActive={isRsiActive}
                        emaPeriod={emaPeriod}
                    />
                </div>

                {isMobile && (
                    <div style={{ width: '100%', marginTop: '10px' }}>
                        <button className="btn-back" onClick={() => navigate('/')} style={{ margin: '0 auto', display: 'block', width: '100%' }}>
                            Listeye Dön
                        </button>
                    </div>
                )}

            </div>
        </div>
    );
}