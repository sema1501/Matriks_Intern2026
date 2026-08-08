import { useGlobalPrices } from '../../context/PriceContext';
import './ConnectionStatus.css';

const STATUS_TEXT = {
  connected: 'Canlı Bağlantı Aktif',
  connecting: 'Bağlantı Kuruluyor...',
  disconnected: 'Bağlantı Koptu',
};

export default function ConnectionStatus() {
  const { connectionStatus = 'connecting' } = useGlobalPrices() || {};

  return (
    <div className="connection-status" role="status" aria-live="polite">
      <span className={`status-dot ${connectionStatus}`} aria-hidden="true" />
      <span className="status-text">
        {STATUS_TEXT[connectionStatus] || 'Bağlantı Durumu Bilinmiyor'}
      </span>
    </div>
  );
}
