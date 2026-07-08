import { useCallback, useEffect, useRef, useState } from 'react';
import { useAuth } from '../../context/AuthContext';
import { useGlobalPrices } from '../../context/PriceContext';
import { getAlerts } from '../../services/apiService';
import './AlertMonitor.css';

const TRIGGERED_KEY = 'cryptotracker_triggered_alert_ids';

function getTriggeredIds() {
  try {
    const raw = localStorage.getItem(TRIGGERED_KEY);
    return raw ? JSON.parse(raw) : [];
  } catch {
    return [];
  }
}

function markTriggered(key) {
  const ids = getTriggeredIds();
  if (!ids.includes(key)) {
    localStorage.setItem(TRIGGERED_KEY, JSON.stringify([...ids, key]));
  }
}

function normalizeDirection(direction) {
  return String(direction || '').toLowerCase();
}

function getAlertId(alert) {
  return alert.id ?? alert.Id;
}

function getAlertDedupeKey(alert) {
  const id = getAlertId(alert);
  if (id != null) return String(id);
  const symbol = alert.symbol ?? alert.Symbol ?? '';
  const target = alert.targetPrice ?? alert.TargetPrice;
  const direction = normalizeDirection(alert.direction ?? alert.Direction);
  return `${symbol}-${target}-${direction}`;
}

function isAlertTriggered(alert, currentPrice) {
  const target = Number(alert.targetPrice ?? alert.TargetPrice);
  const direction = normalizeDirection(alert.direction ?? alert.Direction);
  if (!Number.isFinite(currentPrice) || !Number.isFinite(target) || target <= 0) return false;
  if (direction === 'above') return currentPrice >= target;
  if (direction === 'below') return currentPrice <= target;
  return false;
}

function showBrowserNotification(title, body) {
  if (typeof Notification !== 'undefined' && Notification.permission === 'granted') {
    new Notification(title, { body });
    return true;
  }
  return false;
}

export default function AlertMonitor() {
  const { user } = useAuth();
  const { prices } = useGlobalPrices();
  const [alerts, setAlerts] = useState([]);
  const [toasts, setToasts] = useState([]);
  const notifiedRef = useRef(new Set());

  const addToast = useCallback((message) => {
    const id = Date.now() + Math.random();
    setToasts(prev => [...prev, { id, message }]);
    setTimeout(() => {
      setToasts(prev => prev.filter(t => t.id !== id));
    }, 5000);
  }, []);

  const loadAlerts = useCallback(async () => {
    if (!user) {
      setAlerts([]);
      return;
    }
    try {
      const res = await getAlerts();
      setAlerts(Array.isArray(res.data) ? res.data : []);
    } catch {
      // Backend unavailable or unauthorized — do not break the app
      setAlerts([]);
    }
  }, [user]);

  useEffect(() => {
    loadAlerts();
  }, [loadAlerts]);

  useEffect(() => {
    const handleAlertsChanged = () => loadAlerts();
    window.addEventListener('alerts-changed', handleAlertsChanged);
    return () => window.removeEventListener('alerts-changed', handleAlertsChanged);
  }, [loadAlerts]);

  useEffect(() => {
    if (!user || typeof Notification === 'undefined') return;
    if (Notification.permission === 'default') {
      Notification.requestPermission().catch(() => {});
    }
  }, [user]);

  useEffect(() => {
    if (!user || !alerts.length || !prices) return;

    const triggeredIds = getTriggeredIds();

    alerts.forEach((alert) => {
      const dedupeKey = getAlertDedupeKey(alert);
      const symbol = alert.symbol ?? alert.Symbol;
      const alreadyTriggered =
        alert.isTriggered === true ||
        alert.IsTriggered === true ||
        triggeredIds.includes(dedupeKey) ||
        notifiedRef.current.has(dedupeKey);

      if (alreadyTriggered) return;

      const coinData = prices[symbol];
      if (!coinData?.currentPrice) return;

      const currentPrice = Number(coinData.currentPrice);
      if (!isAlertTriggered(alert, currentPrice)) return;

      const direction = normalizeDirection(alert.direction ?? alert.Direction);
      const target = Number(alert.targetPrice ?? alert.TargetPrice);
      const dirLabel = direction === 'above' ? 'yukarı' : 'aşağı';
      const message = `${symbol} fiyat alarmı tetiklendi! Hedef: $${target.toLocaleString()} (${dirLabel})`;

      markTriggered(dedupeKey);
      notifiedRef.current.add(dedupeKey);

      if (!showBrowserNotification('Fiyat Alarmı', message)) {
        addToast(message);
      }
    });
  }, [user, alerts, prices, addToast]);

  if (!toasts.length) return null;

  return (
    <div className="alert-monitor-toasts" aria-live="polite">
      {toasts.map(t => (
        <div key={t.id} className="alert-monitor-toast">{t.message}</div>
      ))}
    </div>
  );
}
