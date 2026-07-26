import { useMemo, useState } from 'react';
import { Link } from 'react-router-dom';
import { buyCoin, sellCoin } from '../../services/apiService';
import './TradeForm.css';

function getTradeError(error) {
  if (error.response?.status === 401 || error.response?.status === 403) {
    return 'İşlem yapmak için giriş yapmanız gerekiyor.';
  }

  return (
    error.response?.data?.error ||
    error.response?.data?.message ||
    (typeof error.response?.data === 'string' ? error.response.data : null) ||
    'İşlem gerçekleştirilemedi. Lütfen tekrar deneyin.'
  );
}

export default function TradeForm({ symbol, currentPrice, user }) {
  const [tradeType, setTradeType] = useState('buy');
  const [quantity, setQuantity] = useState('');
  const [loading, setLoading] = useState(false);
  const [message, setMessage] = useState({ type: '', text: '' });

  const numericPrice = Number(currentPrice);
  const numericQuantity = Number(quantity);

  const total = useMemo(() => {
    if (!Number.isFinite(numericPrice) || !Number.isFinite(numericQuantity)) return 0;
    return numericPrice * numericQuantity;
  }, [numericPrice, numericQuantity]);

  const handleSubmit = async (event) => {
    event.preventDefault();
    setMessage({ type: '', text: '' });

    if (!user) {
      setMessage({ type: 'error', text: 'İşlem yapmak için giriş yapmanız gerekiyor.' });
      return;
    }

    if (!Number.isFinite(numericPrice) || numericPrice <= 0) {
      setMessage({ type: 'error', text: 'Canlı fiyat bilgisi henüz hazır değil.' });
      return;
    }

    if (!Number.isFinite(numericQuantity) || numericQuantity <= 0) {
      setMessage({ type: 'error', text: 'Lütfen sıfırdan büyük bir miktar girin.' });
      return;
    }

    const request = {
      symbol,
      quantity: numericQuantity,
      price: numericPrice,
    };

    setLoading(true);
    try {
      if (tradeType === 'buy') {
        await buyCoin(request);
      } else {
        await sellCoin(request);
      }

      setMessage({
        type: 'success',
        text: `${tradeType === 'buy' ? 'Alım' : 'Satım'} işlemi başarıyla tamamlandı.`,
      });
      setQuantity('');
      window.dispatchEvent(new Event('portfolio-changed'));
    } catch (error) {
      setMessage({ type: 'error', text: getTradeError(error) });
    } finally {
      setLoading(false);
    }
  };

  return (
    <section className="trade-card">
      <div className="trade-card__header">
        <div>
          <h2>Al / Sat</h2>
          <p>{symbol} için sanal işlem yapın.</p>
        </div>
        <span className="trade-card__price">
          {Number.isFinite(numericPrice)
            ? `$${numericPrice.toLocaleString(undefined, { maximumFractionDigits: 8 })}`
            : 'Fiyat bekleniyor'}
        </span>
      </div>

      <div className="trade-tabs" role="tablist" aria-label="İşlem türü">
        <button
          type="button"
          className={`trade-tab trade-tab--buy ${tradeType === 'buy' ? 'active' : ''}`}
          onClick={() => {
            setTradeType('buy');
            setMessage({ type: '', text: '' });
          }}
          disabled={loading}
        >
          Al
        </button>
        <button
          type="button"
          className={`trade-tab trade-tab--sell ${tradeType === 'sell' ? 'active' : ''}`}
          onClick={() => {
            setTradeType('sell');
            setMessage({ type: '', text: '' });
          }}
          disabled={loading}
        >
          Sat
        </button>
      </div>

      {!user && (
        <p className="trade-login-hint">
          İşlem yapmak için <Link to="/signin">giriş yapın</Link>.
        </p>
      )}

      {message.text && (
        <p className={`trade-message trade-message--${message.type}`}>{message.text}</p>
      )}

      <form onSubmit={handleSubmit} className="trade-form">
        <label htmlFor="tradeQuantity">Miktar</label>
        <input
          id="tradeQuantity"
          type="number"
          min="0.00000001"
          step="any"
          inputMode="decimal"
          value={quantity}
          onChange={(event) => setQuantity(event.target.value)}
          placeholder="Örn. 0.01"
          disabled={!user || loading}
        />

        <div className="trade-total">
          <span>Tahmini toplam</span>
          <strong>
            ${total.toLocaleString(undefined, {
              minimumFractionDigits: 2,
              maximumFractionDigits: 2,
            })}
          </strong>
        </div>

        <button
          type="submit"
          className={`trade-submit trade-submit--${tradeType}`}
          disabled={!user || loading || !Number.isFinite(numericPrice)}
        >
          {loading
            ? 'İşleniyor...'
            : tradeType === 'buy'
              ? `${symbol} Al`
              : `${symbol} Sat`}
        </button>
      </form>
    </section>
  );
}
