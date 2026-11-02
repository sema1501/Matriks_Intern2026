import { useRealtime } from '../../context/RealtimeContext';
import { useLanguage } from '../../context/LanguageContext';
import '../ConnectionStatus/ConnectionStatus.css';

const DOT_CLASS = {
  connecting: 'connecting',
  reconnecting: 'connecting',
  connected: 'connected',
  offline: 'disconnected',
};

/** Backend bildirim bağlantısının durumu. Fiyat akışının durumunu ConnectionStatus gösterir. */
export default function RealtimeStatus() {
  const { status } = useRealtime();
  const { t } = useLanguage();

  if (status === 'idle') return null;

  return (
    <div
      className="connection-status"
      role="status"
      aria-live="polite"
      title={status === 'connected' ? undefined : t('realtime.fallbackHint')}
    >
      <span className={`status-dot ${DOT_CLASS[status]}`} aria-hidden="true" />
      <span className="status-text">{t(`realtime.${status}`)}</span>
    </div>
  );
}
