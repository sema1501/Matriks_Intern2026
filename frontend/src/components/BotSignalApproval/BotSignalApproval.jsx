import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { getBotSignals } from '../../services/apiService';
import { useLanguage } from '../../context/LanguageContext';
import { usePollingFallback, useRealtimeEvent } from '../../context/RealtimeContext';
import { isRiskExit, normalizeReason, reasonLabelKey } from '../../utils/botSignalReason';
import './BotSignalApproval.css';

const POLL_INTERVAL_MS = 15000;

const STATUSES = ['pending', 'approved', 'rejected', 'expired', 'failed'];

const normalizeStatus = (status) => {
  if (typeof status === 'string') return status.toLowerCase();
  return STATUSES[Number(status)] || 'unknown';
};

const normalizeSignalType = (type) => {
  if (typeof type === 'string') return type.toLowerCase();
  return Number(type) === 1 ? 'sell' : 'buy';
};

const formatPrice = (value, locale) =>
  Number(value || 0).toLocaleString(locale, {
    style: 'currency',
    currency: 'USD',
    minimumFractionDigits: 2,
    maximumFractionDigits: 8,
  });

const formatDate = (value, locale) => {
  if (!value) return '-';
  return new Date(value).toLocaleString(locale);
};

export default function BotSignalApproval({ bots = [] }) {
  const { t, locale } = useLanguage();
  const statusLabel = useCallback(
    (status) => (STATUSES.includes(status) ? t(`signal.status.${status}`) : status),
    [t]
  );
  const [signals, setSignals] = useState([]);
  const [loading, setLoading] = useState(false);
  const [message, setMessage] = useState(null);
  const knownIds = useRef(new Set());
  const initialized = useRef(false);
  const signalsRef = useRef([]);

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
      // Stop-loss / take-profit satışı varsa onu öne çıkar; kullanıcı neden satıldığını hemen görsün.
      const newest = newSignals.find((signal) => isRiskExit(signal.reason)) || newSignals[0];
      const type = normalizeSignalType(newest.signalType);
      const status = normalizeStatus(newest.status);
      const symbol = botMap.get(newest.botId)?.symbol || '';
      const riskExit = isRiskExit(newest.reason);
      const riskText = riskExit ? `${t(reasonLabelKey(newest.reason))} ${t('bot.riskTriggered')}: ` : '';

      setMessage({
        type: status === 'failed' ? 'error' : riskExit ? 'warning' : 'info',
        text: `${riskText}${t('signal.newSignal', { type: t(`signal.${type}`), symbol, status: statusLabel(status) })}`,
      });

      if ('Notification' in window && Notification.permission === 'granted') {
        new Notification(riskExit ? `${t(reasonLabelKey(newest.reason))} ${t('bot.riskTriggered')}` : t('signal.notificationTitle'), {
          body: t('signal.notificationBody', { symbol: symbol || 'Coin', type: t(`signal.${type}`) }),
        });
      }
    }

    knownIds.current = new Set(nextSignals.map((signal) => signal.id));
    initialized.current = true;
  }, [botMap, t, statusLabel]);

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

      signalsRef.current = merged;
      setSignals(merged);
      notifyNewSignals(merged);
    } catch (error) {
      console.error('Bot sinyalleri yüklenemedi:', error);
      if (!silent) {
        setMessage({ type: 'error', text: t('signal.loadError') });
      }
    } finally {
      if (!silent) setLoading(false);
    }
  }, [bots, notifyNewSignals, t]);

  useEffect(() => {
    fetchSignals();
  }, [fetchSignals]);

  // SignalR bağlantısı yokken eski polling devam eder, bağlantı gelince durur (#102).
  usePollingFallback(() => fetchSignals({ silent: true }), POLL_INTERVAL_MS, bots.length > 0);

  // Anlık gelen sinyal listeye eklenir. Polling ile aynı kimlik kümesinden geçtiği için iki kez bildirilmez.
  const handleRealtimeSignal = (signal) => {
    if (!signal || !initialized.current || !botMap.has(signal.botId) || knownIds.current.has(signal.id)) return;
    const merged = [signal, ...signalsRef.current]
      .sort((a, b) => new Date(b.createdAt) - new Date(a.createdAt));
    signalsRef.current = merged;
    setSignals(merged);
    notifyNewSignals(merged);
  };
  useRealtimeEvent('botSignal', handleRealtimeSignal);
  useRealtimeEvent('botSignalPending', handleRealtimeSignal);

  const requestNotificationPermission = async () => {
    if (!('Notification' in window)) {
      setMessage({ type: 'error', text: t('signal.notSupported') });
      return;
    }

    const permission = await Notification.requestPermission();
    setMessage({
      type: permission === 'granted' ? 'success' : 'error',
      text:
        permission === 'granted'
          ? t('signal.permissionGranted')
          : t('signal.permissionDenied'),
    });
  };

  return (
    <section className="bot-signal-card" aria-labelledby="bot-signal-title">
      <div className="bot-signal-header">
        <div>
          <h3 id="bot-signal-title">{t('signal.title')}</h3>
          <p>{t('signal.subtitle')}</p>
        </div>
        <div className="bot-signal-header-actions">
          <span className="bot-signal-count">{t('signal.count', { count: recentSignals.length })}</span>
          <button type="button" className="bot-signal-secondary" onClick={requestNotificationPermission}>
            {t('signal.enableNotifications')}
          </button>
          <button type="button" className="bot-signal-secondary" onClick={() => fetchSignals()}>
            {t('common.refresh')}
          </button>
        </div>
      </div>

      {message && (
        <div className={`bot-signal-message bot-signal-message--${message.type}`} role="status">
          {message.text}
          <button type="button" onClick={() => setMessage(null)} aria-label={t('common.closeMessage')}>×</button>
        </div>
      )}

      {loading ? (
        <div className="bot-signal-empty">{t('signal.loading')}</div>
      ) : recentSignals.length === 0 ? (
        <div className="bot-signal-empty">
          {t('signal.empty')}
        </div>
      ) : (
        <div className="bot-signal-list">
          {recentSignals.map((signal) => {
            const bot = botMap.get(signal.botId);
            const type = normalizeSignalType(signal.signalType);
            const status = normalizeStatus(signal.status);
            const isBuy = type === 'buy';
            const reason = normalizeReason(signal.reason);
            return (
              <article key={signal.id} className={`bot-signal-item bot-signal-item--${type}`}>
                <div className="bot-signal-main">
                  <span className={`bot-signal-type bot-signal-type--${type}`}>
                    {isBuy ? t('signal.buy') : t('signal.sell')}
                  </span>
                  <div>
                    <strong>{bot?.symbol || `Bot #${signal.botId}`}</strong>
                    <div className="bot-signal-meta">
                      RSI: {Number(signal.rsiValueAtSignal).toFixed(2)} · {t('signal.price')}: {formatPrice(signal.priceAtSignal, locale)}
                    </div>
                    <div className="bot-signal-date">{formatDate(signal.createdAt, locale)}</div>
                  </div>
                </div>

                <div className="bot-signal-actions">
                  {reason !== 'strategy' && (
                    <span className={`bot-signal-reason bot-signal-reason--${reason}`}>
                      {t(reasonLabelKey(reason))}
                    </span>
                  )}
                  <span className={`bot-signal-status bot-signal-status--${status}`}>
                    {statusLabel(status)}
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
