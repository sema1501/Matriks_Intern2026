import { useCurrency } from '../../context/CurrencyContext';
import './CurrencyToggle.css';

function CurrencyToggle() {
  const { currency, changeCurrency, rateLoading, rateError } = useCurrency();

  return (
    <div className="currency-toggle-wrapper">
      <div className="currency-toggle" aria-label="Currency selector">
        <button
          type="button"
          className={currency === 'USD' ? 'active' : ''}
          onClick={() => changeCurrency('USD')}
        >
          USD
        </button>

        <button
          type="button"
          className={currency === 'TRY' ? 'active' : ''}
          onClick={() => changeCurrency('TRY')}
        >
          TRY
        </button>
      </div>

      {rateLoading && (
        <span className="currency-rate-message">Kur yükleniyor</span>
      )}

      {rateError && currency === 'TRY' && !rateLoading && (
        <span className="currency-rate-message warning">Yaklaşık kur</span>
      )}
    </div>
  );
}

export default CurrencyToggle;