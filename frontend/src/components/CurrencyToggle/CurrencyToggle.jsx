import { useCurrency } from '../../context/CurrencyContext';
import { useLanguage } from '../../context/LanguageContext';
import './CurrencyToggle.css';

function CurrencyToggle() {
  const { currency, changeCurrency, rateLoading, rateError } = useCurrency();
  const { t } = useLanguage();

  return (
    <div className="currency-toggle-wrapper">
      <div className="currency-toggle" aria-label={t('currency.selector')}>
        <button
          type="button"
          className={currency === 'USD' ? 'active' : ''}
          onClick={() => changeCurrency('USD')}
          aria-pressed={currency === 'USD'}
          title={t('currency.showUsd')}
        >
          <span className="currency-check">
            {currency === 'USD' ? '✓' : ''}
          </span>
          USD
        </button>

        <button
          type="button"
          className={currency === 'TRY' ? 'active' : ''}
          onClick={() => changeCurrency('TRY')}
          aria-pressed={currency === 'TRY'}
          title={t('currency.showTry')}
        >
          <span className="currency-check">
            {currency === 'TRY' ? '✓' : ''}
          </span>
          TRY
        </button>
      </div>

      {rateLoading && (
        <span className="currency-rate-message">{t('currency.rateLoading')}</span>
      )}

      {rateError && currency === 'TRY' && !rateLoading && (
        <span className="currency-rate-message warning">{t('currency.rateApprox')}</span>
      )}
    </div>
  );
}

export default CurrencyToggle;