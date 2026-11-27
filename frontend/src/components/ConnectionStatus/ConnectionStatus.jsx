import { useGlobalPrices } from '../../context/PriceContext';
import { useLanguage } from '../../context/LanguageContext';
import './ConnectionStatus.css';

const KNOWN_STATUSES = ['connected', 'connecting', 'disconnected'];

export default function ConnectionStatus() {
  const { connectionStatus = 'connecting' } = useGlobalPrices() || {};
  const { t } = useLanguage();

  return (
    <div className="connection-status" role="status" aria-live="polite">
      <span className={`status-dot ${connectionStatus}`} aria-hidden="true" />
      <span className="status-text">
        {KNOWN_STATUSES.includes(connectionStatus)
          ? t(`connection.${connectionStatus}`)
          : t('connection.unknown')}
      </span>
    </div>
  );
}
