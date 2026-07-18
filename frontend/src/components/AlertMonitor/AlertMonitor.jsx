import { useCallback, useEffect, useRef, useState } from 'react';
import { useAuth } from '../../context/AuthContext';
import { getAlerts, getAlertSignals } from '../../services/apiService';
import './AlertMonitor.css';

const TOAST_DURATION_MS = 8000;
const POLL_INTERVAL_MS = 45_000;

function getAlertField(alert, camel, pascal) {
  return alert[camel] ?? alert[pascal];
}

function showBrowserNotification(title, body) {
  if (typeof Notification !== 'undefined' && Notification.permission === 'granted') {
    new Notification(title, { body });
    return true;
  }
  return false;
}

function formatTriggeredAt(value) {
  if (!value) return '';
  return new Date(value).toLocaleString('tr-TR');
}

/**
 * Backend writes AlertSignal rows while this page is closed.
 * This component only polls for new signal history while the user is logged in
 * and the app is open. Closed-browser delivery (push/email) is out of scope.
 */
export default function AlertMonitor() {
  const { user } = useAuth();
  const [toasts, setToasts] = useState([]);
  const knownSignalIdsRef = useRef(new Set());
  const knownCountsRef = useRef(new Map());
  const baselineReadyRef = useRef(false);
  const pollingRef = useRef(false);

  const addToast = useCallback((message) => {
    const id = Date.now() + Math.random();
    setToasts((prev) => [...prev, { id, message }]);
    setTimeout(() => {
      setToasts((prev) => prev.filter((t) => t.id !== id));
    }, TOAST_DURATION_MS);
  }, []);

  const notifyNewSignals = useCallback((alert, signals) => {
    const symbol = getAlertField(alert, 'symbol', 'Symbol');
    const fresh = signals.filter((s) => {
      const id = s.id ?? s.Id;
      if (id == null || knownSignalIdsRef.current.has(id)) return false;
      knownSignalIdsRef.current.add(id);
      return true;
    });

    if (!baselineReadyRef.current || fresh.length === 0) return;

    fresh.forEach((signal) => {
      const price = Number(signal.priceAtTrigger ?? signal.PriceAtTrigger);
      const at = formatTriggeredAt(signal.triggeredAt ?? signal.TriggeredAt);
      const message = `${symbol} sinyal: $${price.toLocaleString()} (${at})`;
      addToast(message);
      showBrowserNotification('Fiyat Alarmı Sinyali', message);
    });
  }, [addToast]);

  const pollSignals = useCallback(async () => {
    if (!user || pollingRef.current) return;
    pollingRef.current = true;
    try {
      const alertsRes = await getAlerts();
      const alerts = Array.isArray(alertsRes.data) ? alertsRes.data : [];

      await Promise.all(
        alerts.map(async (alert) => {
          const alertId = getAlertField(alert, 'id', 'Id');
          const signalCount = Number(getAlertField(alert, 'signalCount', 'SignalCount') ?? 0);
          if (!alertId) return;

          const previousCount = knownCountsRef.current.get(alertId) ?? 0;
          const needsFetch = !baselineReadyRef.current
            ? signalCount > 0
            : signalCount > previousCount;

          knownCountsRef.current.set(alertId, signalCount);

          if (!needsFetch) return;

          try {
            const signalsRes = await getAlertSignals(alertId);
            const payload = signalsRes.data ?? {};
            const signals = Array.isArray(payload.signals)
              ? payload.signals
              : Array.isArray(payload.Signals)
                ? payload.Signals
                : [];

            if (!baselineReadyRef.current) {
              signals.forEach((s) => {
                const id = s.id ?? s.Id;
                if (id != null) knownSignalIdsRef.current.add(id);
              });
              return;
            }

            notifyNewSignals(alert, signals);
          } catch {
            // Per-alert signal fetch failures should not break polling.
          }
        })
      );

      if (!baselineReadyRef.current) {
        baselineReadyRef.current = true;
      }
    } catch {
      // Backend unavailable — keep previous known IDs; retry next cycle.
    } finally {
      pollingRef.current = false;
    }
  }, [user, notifyNewSignals]);

  useEffect(() => {
    if (!user) {
      knownSignalIdsRef.current = new Set();
      knownCountsRef.current = new Map();
      baselineReadyRef.current = false;
      return undefined;
    }

    if (typeof Notification !== 'undefined' && Notification.permission === 'default') {
      Notification.requestPermission().catch(() => {});
    }

    pollSignals();
    const intervalId = setInterval(pollSignals, POLL_INTERVAL_MS);
    const handleAlertsChanged = () => pollSignals();
    window.addEventListener('alerts-changed', handleAlertsChanged);

    return () => {
      clearInterval(intervalId);
      window.removeEventListener('alerts-changed', handleAlertsChanged);
    };
  }, [user, pollSignals]);

  if (!toasts.length) return null;

  return (
    <div className="alert-monitor-toasts" aria-live="polite">
      {toasts.map((t) => (
        <div key={t.id} className="alert-monitor-toast">{t.message}</div>
      ))}
    </div>
  );
}
