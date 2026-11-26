import { useMemo, useState } from 'react';
import { Link } from 'react-router-dom';
import { buyCoin, sellCoin } from '../../services/apiService';
import { useLanguage } from '../../context/LanguageContext';
import './TradeForm.css';

function getTradeError(error, t) {
  if (error.response?.status === 401 || error.response?.status === 403) {
    return t('trade.loginRequired');
  }

  return (
    error.response?.data?.error ||
    error.response?.data?.message ||
    (typeof error.response?.data === 'string' ? error.response.data : null) ||
    t('trade.failed')
  );
}

export default function TradeForm({ symbol, currentPrice, user }) {
  const { t, locale } = useLanguage();
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
      setMessage({ type: 'error', text: t('trade.loginRequired') });
      return;
    }

    if (!Number.isFinite(numericPrice) || numericPrice <= 0) {
      setMessage({ type: 'error', text: t('trade.priceNotReady') });
      return;
    }

    if (!Number.isFinite(numericQuantity) || numericQuantity <= 0) {
      setMessage({ type: 'error', text: t('trade.quantityInvalid') });
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
        text: tradeType === 'buy' ? t('trade.buySuccess') : t('trade.sellSuccess'),
      });
      setQuantity('');
      window.dispatchEvent(new Event('portfolio-changed'));
    } catch (error) {
      setMessage({ type: 'error', text: getTradeError(error, t) });
    } finally {
      setLoading(false);
    }
  };

  return (
    <section className="trade-card">
      <div className="trade-card__header">
        <div>
          <h2>{t('trade.title')}</h2>
          <p>{t('trade.subtitle', { symbol })}</p>
        </div>
        <span className="trade-card__price">
          {Number.isFinite(numericPrice)
            ? `$${numericPrice.toLocaleString(locale, { maximumFractionDigits: 8 })}`
            : t('trade.waitingPrice')}
        </span>
      </div>

      <div className="trade-tabs" role="tablist" aria-label={t('trade.typeLabel')}>
        <button
          type="button"
          className={`trade-tab trade-tab--buy ${tradeType === 'buy' ? 'active' : ''}`}
          onClick={() => {
            setTradeType('buy');
            setMessage({ type: '', text: '' });
          }}
          disabled={loading}
        >
          {t('trade.buy')}
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
          {t('trade.sell')}
        </button>
      </div>

      {!user && (
        <p className="trade-login-hint">
          {t('trade.signinPrefix')} <Link to="/signin">{t('trade.signinLink')}</Link>{t('trade.signinSuffix')}
        </p>
      )}

      {message.text && (
        <p className={`trade-message trade-message--${message.type}`}>{message.text}</p>
      )}

      <form onSubmit={handleSubmit} className="trade-form">
        <label htmlFor="tradeQuantity">{t('converter.amount')}</label>
        <input
          id="tradeQuantity"
          type="number"
          min="0.00000001"
          step="any"
          inputMode="decimal"
          value={quantity}
          onChange={(event) => setQuantity(event.target.value)}
          placeholder={t('trade.quantityPlaceholder')}
          disabled={!user || loading}
        />

        <div className="trade-total">
          <span>{t('trade.estimatedTotal')}</span>
          <strong>
            ${total.toLocaleString(locale, {
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
            ? t('trade.processing')
            : tradeType === 'buy'
              ? t('trade.buySymbol', { symbol })
              : t('trade.sellSymbol', { symbol })}
        </button>
      </form>
    </section>
  );
}
