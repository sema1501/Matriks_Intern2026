import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { getBotSignals } from '../../services/apiService';
import './BotSignalApproval.css';

const POLL_INTERVAL_MS = 15000;

const STATUS_LABELS = {
  pending: 'Bekleyen',
  approved: 'Otomatik Onaylandı',
  rejected: 'Reddedildi',
  expired: 'Süresi Doldu',
  failed: 'Başarısız',
};

const normalizeStatus = (status) => {
  if (typeof status === 'string') return status.toLowerCase();
  return ['pending', 'approved', 'rejected', 'expired', 'failed'][Number(status)] || 'unknown';
};

const normalizeSignalType = (type) => {
  if (typeof type === 'string') return type.toLowerCase();
  return Number(type) === 1 ? 'sell' : 'buy';
};

const formatPrice = (value) =>
  Number(value || 0).toLocaleString('tr-TR', {
    style: 'currency',
    currency: 'USD',
    minimumFractionDigits: 2,
    maximumFractionDigits: 8,
  });

const formatDate = (value) => {
  if (!value) return '-';
  return new Date(value).toLocaleString('tr-TR');
};

export default function BotSignalApproval({ bots = [] }) {
  const [signals, setSignals] = useState([]);
  const [loading, setLoading] = useState(false);
  const [message, setMessage] = useState(null);
  const knownIds = useRef(new Set());
  const initialized = useRef(false);

  const botMap = useMemo(
    () => new Map(bots.map((bot) => [bot.id, bot])),
    [bots]
  );

  const recentSignals = useMemo(
    () => signals.slice(0, 20),
    [signals]
  );

  const notifyNewSignals = useCallback((nextSignals) => {
    const newSignals = nextSignals.filter(
      (signal) => !knownIds.current.has(signal.id)
    );

    if (initialized.current && newSignals.length > 0) {
      const newest = newSignals[0];
      const type = normalizeSignalType(newest.signalType);
      const status = normalizeStatus(newest.status);
      setMessage({
        type: status === 'failed' ? 'error' : 'info',
        text: `Yeni otomatik bot sinyali: ${type === 'buy' ? 'AL' : 'SAT'} ${botMap.get(newest.botId)?.symbol || ''} (${STATUS_LABELS[status] || status})`,
      });

      if ('Notification' in window && Notification.permission === 'granted') {
        new Notification('Bot sinyali', {
          body: `${botMap.get(newest.botId)?.symbol || 'Coin'} için ${type === 'buy' ? 'AL' : 'SAT'} otomatik işlendi.`,
        });
      }
    }

    knownIds.current = new Set(nextSignals.map((signal) => signal.id));
    initialized.current = true;
  }, [botMap]);

  const fetchSignals = useCallback(async ({ silent = false } = {}) => {
    if (!bots.length) {
      setSignals([]);
      setLoading(false);
      return;
    }

    try {
      if (!silent) setLoading(true);
      const responses = await Promise.all(
        bots.map((bot) => getBotSignals(bot.id))
      );
      const merged = responses
        .flatMap((response) => response?.data || [])
        .sort((a, b) => new Date(b.createdAt) - new Date(a.createdAt));

      setSignals(merged);
      notifyNewSignals(merged);
    } catch (error) {
      console.error('Bot sinyalleri yüklenemedi:', error);
      if (!silent) {
        setMessage({ type: 'error', text: 'Bot sinyalleri yüklenemedi.' });
      }
    } finally {
      if (!silent) setLoading(false);
    }
  }, [bots, notifyNewSignals]);

  useEffect(() => {
    fetchSignals();
    const interval = window.setInterval(
      () => fetchSignals({ silent: true }),
      POLL_INTERVAL_MS
    );
    return () => window.clearInterval(interval);
  }, [fetchSignals]);

  const requestNotificationPermission = async () => {
    if (!('Notification' in window)) {
      setMessage({ type: 'error', text: 'Tarayıcınız bildirimleri desteklemiyor.' });
      return;
    }

    const permission = await Notification.requestPermission();
    setMessage({
      type: permission === 'granted' ? 'success' : 'error',
      text:
        permission === 'granted'
          ? 'Tarayıcı bildirimleri açıldı.'
          : 'Bildirim izni verilmedi.',
    });
  };

  return (
    <section className="bot-signal-card" aria-labelledby="bot-signal-title">
      <div className="bot-signal-header">
        <div>
          <h3 id="bot-signal-title">Bot Sinyal Geçmişi</h3>
          <p>
            Bot sinyalleri otomatik olarak sanal portföyde çalıştırılır. Manuel onay gerekmez.
          </p>
        </div>
        <div className="bot-signal-header-actions">
          <span className="bot-signal-count">{recentSignals.length} kayıt</span>
          <button type="button" className="bot-signal-secondary" onClick={requestNotificationPermission}>
            Bildirimleri Aç
          </button>
          <button type="button" className="bot-signal-secondary" onClick={() => fetchSignals()}>
            Yenile
          </button>
        </div>
      </div>

      {message && (
        <div className={`bot-signal-message bot-signal-message--${message.type}`} role="status">
          {message.text}
          <button type="button" onClick={() => setMessage(null)} aria-label="Mesajı kapat">×</button>
        </div>
      )}

      {loading ? (
        <div className="bot-signal-empty">Sinyaller yükleniyor...</div>
      ) : recentSignals.length === 0 ? (
        <div className="bot-signal-empty">
          Henüz bot sinyali yok. Sistem arka planda otomatik işlem yapar.
        </div>
      ) : (
        <div className="bot-signal-list">
          {recentSignals.map((signal) => {
            const bot = botMap.get(signal.botId);
            const type = normalizeSignalType(signal.signalType);
            const status = normalizeStatus(signal.status);
            const isBuy = type === 'buy';
            return (
              <article key={signal.id} className={`bot-signal-item bot-signal-item--${type}`}>
                <div className="bot-signal-main">
                  <span className={`bot-signal-type bot-signal-type--${type}`}>
                    {isBuy ? 'AL' : 'SAT'}
                  </span>
                  <div>
                    <strong>{bot?.symbol || `Bot #${signal.botId}`}</strong>
                    <div className="bot-signal-meta">
                      RSI: {Number(signal.rsiValueAtSignal).toFixed(2)} · Fiyat: {formatPrice(signal.priceAtSignal)}
                    </div>
                    <div className="bot-signal-date">{formatDate(signal.createdAt)}</div>
                  </div>
                </div>

                <div className="bot-signal-actions">
                  <span className={`bot-signal-status bot-signal-status--${status}`}>
                    {STATUS_LABELS[status] || status}
                  </span>
                </div>
              </article>
            );
          })}
        </div>
      )}
    </section>
  );
}
