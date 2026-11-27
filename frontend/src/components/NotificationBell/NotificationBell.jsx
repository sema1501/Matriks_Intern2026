import { useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { useAuth } from '../../context/AuthContext';
import { useLanguage } from '../../context/LanguageContext';
import { useRealtimeEvent } from '../../context/RealtimeContext';
import { getBots } from '../../services/apiService';
import useDropdown from '../NavDropdown/useDropdown';
import '../NavDropdown/NavDropdown.css';

const MAX_ITEMS = 20;
const storageKey = (userId) => `crypto-tracker-notifications-${userId}`;

function readSaved(userId) {
  try {
    const saved = JSON.parse(localStorage.getItem(storageKey(userId)) || '[]');
    return Array.isArray(saved) ? saved : [];
  } catch {
    return [];
  }
}

const isSell = (type) => (typeof type === 'string' ? type.toLowerCase() === 'sell' : Number(type) === 1);

// Sağ üstteki zil: anlık gelen fiyat alarmı ve bot sinyallerini listeler.
// Liste tarayıcıda kullanıcıya özel saklanır, okunmamış sayısı rozette görünür.
export default function NotificationBell() {
  const { user } = useAuth();
  const { t, locale } = useLanguage();
  const { open, toggle, close, wrapperRef, buttonRef } = useDropdown();
  const [items, setItems] = useState([]);
  const [botSymbols, setBotSymbols] = useState({});
  const userId = user?.id ?? user?.username;

  useEffect(() => {
    setItems(userId ? readSaved(userId) : []);
  }, [userId]);

  useEffect(() => {
    if (!userId) return;
    getBots()
      .then((res) => {
        const map = {};
        (res?.data || []).forEach((bot) => { map[bot.id] = bot.symbol; });
        setBotSymbols(map);
      })
      .catch(() => {});
  }, [userId]);

  // Liste her değiştiğinde güncel hali saklanır; arka arkaya gelen olaylar birbirini ezmez.
  const update = (change) => {
    setItems((current) => {
      const next = change(current);
      if (userId && next !== current) localStorage.setItem(storageKey(userId), JSON.stringify(next));
      return next;
    });
  };

  const add = (item) => {
    if (!userId) return;
    update((current) => (current.some((existing) => existing.key === item.key)
      ? current
      : [item, ...current].slice(0, MAX_ITEMS)));
  };

  useRealtimeEvent('alertTriggered', (event) => {
    if (event?.id == null) return;
    add({
      key: `alert-${event.id}`,
      kind: 'alert',
      symbol: event.symbol,
      price: event.priceAtTrigger,
      at: event.triggeredAt || new Date().toISOString(),
      read: false,
    });
  });

  useRealtimeEvent('botSignal', (signal) => {
    if (signal?.id == null) return;
    add({
      key: `bot-${signal.id}`,
      kind: 'bot',
      botId: signal.botId,
      sell: isSell(signal.signalType),
      price: signal.priceAtSignal,
      at: signal.createdAt || new Date().toISOString(),
      read: false,
    });
  });

  if (!user) return null;

  const unread = items.filter((item) => !item.read).length;

  const markAllRead = () => update((current) => current.map((item) => ({ ...item, read: true })));

  const describe = (item) => {
    const price = Number(item.price || 0).toLocaleString(locale, { minimumFractionDigits: 2, maximumFractionDigits: 8 });
    if (item.kind === 'alert') return t('bell.alertText', { symbol: item.symbol, price });
    const symbol = botSymbols[item.botId] || `Bot #${item.botId}`;
    return t('bell.botText', { type: item.sell ? t('signal.sell') : t('signal.buy'), symbol, price });
  };

  const label = unread > 0 ? t('bell.labelUnread', { count: unread }) : t('bell.label');

  return (
    <div className="nav-dropdown" ref={wrapperRef}>
      <button
        type="button"
        ref={buttonRef}
        className="nav-dropdown__button"
        onClick={toggle}
        aria-haspopup="true"
        aria-expanded={open}
        aria-label={label}
        title={label}
      >
        <svg viewBox="0 0 24 24" width="19" height="19" aria-hidden="true">
          <path d="M6 16V11a6 6 0 1 1 12 0v5l1.5 2h-15z" fill="none" stroke="currentColor" strokeWidth="1.8" strokeLinejoin="round" />
          <path d="M10 20a2 2 0 0 0 4 0" fill="none" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" />
        </svg>
        {unread > 0 && <span className="nav-dropdown__badge" aria-hidden="true">{unread > 9 ? '9+' : unread}</span>}
      </button>

      {open && (
        <div className="nav-dropdown__panel" role="dialog" aria-label={t('bell.title')}>
          <div className="nav-dropdown__header">
            <strong>{t('bell.title')}</strong>
            {unread > 0 && (
              <button type="button" onClick={markAllRead}>{t('bell.markAllRead')}</button>
            )}
          </div>
          {items.length === 0 ? (
            <div className="nav-dropdown__empty">{t('bell.empty')}</div>
          ) : (
            <ul className="nav-dropdown__list">
              {items.map((item) => (
                <li key={item.key}>
                  <Link
                    to={item.kind === 'alert' ? `/coin/${item.symbol}` : '/bot'}
                    className={`nav-dropdown__notice nav-dropdown__item${item.read ? '' : ' is-unread'}`}
                    onClick={() => {
                      update((current) => current.map((other) => (other.key === item.key ? { ...other, read: true } : other)));
                      close();
                    }}
                  >
                    <span>
                      {describe(item)}
                      <small>{new Date(item.at).toLocaleString(locale)}</small>
                    </span>
                  </Link>
                </li>
              ))}
            </ul>
          )}
        </div>
      )}
    </div>
  );
}
