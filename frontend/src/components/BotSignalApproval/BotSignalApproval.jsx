import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import {
  approveBotSignal,
  getBotSignals,
  rejectBotSignal,
} from '../../services/apiService';
import './BotSignalApproval.css';

const POLL_INTERVAL_MS = 15000;

const normalizeStatus = (status) => {
  if (typeof status === 'string') return status.toLowerCase();
  return ['pending', 'approved', 'rejected', 'expired'][Number(status)] || 'unknown';
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
  const [actionId, setActionId] = useState(null);
  const [message, setMessage] = useState(null);
  const knownPendingIds = useRef(new Set());
  const initialized = useRef(false);

  const botMap = useMemo(
    () => new Map(bots.map((bot) => [bot.id, bot])),
    [bots]
  );

  const pendingSignals = useMemo(
    () => signals.filter((signal) => normalizeStatus(signal.status) === 'pending'),
    [signals]
  );

  const notifyNewSignals = useCallback((nextSignals) => {
    const pending = nextSignals.filter(
      (signal) => normalizeStatus(signal.status) === 'pending'
    );

    const newSignals = pending.filter(
      (signal) => !knownPendingIds.current.has(signal.id)
    );

    if (initialized.current && newSignals.length > 0) {
      const newest = newSignals[0];
      const type = normalizeSignalType(newest.signalType);
      setMessage({
        type: 'info',
        text: `${newSignals.length} yeni bot sinyali geldi: ${type === 'buy' ? 'AL' : 'SAT'} ${botMap.get(newest.botId)?.symbol || ''}`,
      });

      if ('Notification' in window && Notification.permission === 'granted') {
        new Notification('Yeni bot sinyali', {
          body: `${botMap.get(newest.botId)?.symbol || 'Coin'} için ${type === 'buy' ? 'AL' : 'SAT'} sinyali oluştu.`,
        });
      }
    }

    knownPendingIds.current = new Set(pending.map((signal) => signal.id));
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

  const handleAction = async (signal, action) => {
    try {
      setActionId(signal.id);
      setMessage(null);
      const response =
        action === 'approve'
          ? await approveBotSignal(signal.id)
          : await rejectBotSignal(signal.id);

      setSignals((current) =>
        current.map((item) =>
          item.id === signal.id
            ? { ...item, status: response?.data?.status ?? (action === 'approve' ? 1 : 2) }
            : item
        )
      );

      knownPendingIds.current.delete(signal.id);
      setMessage({
        type: 'success',
        text:
          action === 'approve'
            ? 'Sinyal onaylandı ve sanal işlem portföye uygulandı.'
            : 'Sinyal reddedildi.',
      });
    } catch (error) {
      console.error('Sinyal işlemi başarısız:', error);
      const serverMessage =
        error.response?.data?.error ||
        error.response?.data?.message ||
        'Sinyal işlemi tamamlanamadı.';
      setMessage({ type: 'error', text: serverMessage });
      await fetchSignals({ silent: true });
    } finally {
      setActionId(null);
    }
  };

  return (
    <section className="bot-signal-card" aria-labelledby="bot-signal-title">
      <div className="bot-signal-header">
        <div>
          <h3 id="bot-signal-title">Sinyal Onay Merkezi</h3>
          <p>Botların oluşturduğu bekleyen AL/SAT sinyallerini onaylayın veya reddedin.</p>
        </div>
        <div className="bot-signal-header-actions">
          <span className="bot-signal-count">{pendingSignals.length} bekleyen</span>
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
      ) : pendingSignals.length === 0 ? (
        <div className="bot-signal-empty">
          Şu anda onay bekleyen sinyal yok. Sistem 15 saniyede bir kontrol ediyor.
        </div>
      ) : (
        <div className="bot-signal-list">
          {pendingSignals.map((signal) => {
            const bot = botMap.get(signal.botId);
            const type = normalizeSignalType(signal.signalType);
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
                  <button
                    type="button"
                    className="bot-signal-reject"
                    disabled={actionId === signal.id}
                    onClick={() => handleAction(signal, 'reject')}
                  >
                    Reddet
                  </button>
                  <button
                    type="button"
                    className="bot-signal-approve"
                    disabled={actionId === signal.id}
                    onClick={() => handleAction(signal, 'approve')}
                  >
                    {actionId === signal.id ? 'İşleniyor...' : 'Onayla'}
                  </button>
                </div>
              </article>
            );
          })}
        </div>
      )}
    </section>
  );
}
