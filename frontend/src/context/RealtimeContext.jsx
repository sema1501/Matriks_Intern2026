import { createContext, useCallback, useContext, useEffect, useRef, useState } from 'react';
import { HubConnectionBuilder, LogLevel } from '@microsoft/signalr';
import { useAuth } from './AuthContext';
import { API_BASE_URL, getFreshAccessToken } from '../services/apiService';

// Backend'deki RealtimeEvents ile aynı adlar.
export const REALTIME_EVENTS = ['alertTriggered', 'botSignal', 'botSignalPending'];

const HUB_URL = `${API_BASE_URL}/hubs/notifications`;
// withAutomaticReconnect denemeleri bitince (backend uzun süre kapalı) bağlantıyı bu aralıkla tekrar deneriz.
export const RESTART_DELAY_MS = 15_000;

// status: idle (giriş yok) | connecting | connected | reconnecting | offline
const RealtimeContext = createContext({ status: 'idle', isLive: false, subscribe: () => () => {} });

export function RealtimeProvider({ children }) {
  const { user } = useAuth();
  const userKey = user ? user.id ?? user.username : null;
  const [status, setStatus] = useState('idle');
  const handlersRef = useRef(new Map());

  const subscribe = useCallback((eventName, handler) => {
    const handlers = handlersRef.current;
    if (!handlers.has(eventName)) handlers.set(eventName, new Set());
    handlers.get(eventName).add(handler);
    return () => handlers.get(eventName)?.delete(handler);
  }, []);

  // Giriş yapılınca bağlantı açılır, çıkış yapılınca (userKey null) kapanır.
  useEffect(() => {
    if (userKey == null) {
      setStatus('idle');
      return undefined;
    }

    let stopped = false;
    let restartTimer = null;

    const connection = new HubConnectionBuilder()
      // Her bağlanışta ve yeniden bağlanışta çağrılır; token dolmak üzereyse önce yenilenir.
      .withUrl(HUB_URL, { accessTokenFactory: getFreshAccessToken })
      .withAutomaticReconnect()
      .configureLogging(LogLevel.Warning)
      .build();

    REALTIME_EVENTS.forEach((eventName) => {
      connection.on(eventName, (payload) => {
        handlersRef.current.get(eventName)?.forEach((handler) => handler(payload));
      });
    });

    const scheduleRestart = () => {
      setStatus('offline');
      restartTimer = setTimeout(start, RESTART_DELAY_MS);
    };

    const start = async () => {
      if (stopped) return;
      try {
        await connection.start();
        if (!stopped) setStatus('connected');
      } catch {
        if (!stopped) scheduleRestart();
      }
    };

    connection.onreconnecting(() => setStatus('reconnecting'));
    connection.onreconnected(() => setStatus('connected'));
    connection.onclose(() => {
      if (!stopped) scheduleRestart();
    });

    setStatus('connecting');
    start();

    return () => {
      stopped = true;
      clearTimeout(restartTimer);
      connection.stop().catch(() => {});
    };
  }, [userKey]);

  return (
    <RealtimeContext.Provider value={{ status, isLive: status === 'connected', subscribe }}>
      {children}
    </RealtimeContext.Provider>
  );
}

export const useRealtime = () => useContext(RealtimeContext);

/** Bir olaya abone olur; handler her render'da değişse de abonelik yeniden kurulmaz. */
export function useRealtimeEvent(eventName, handler) {
  const { subscribe } = useRealtime();
  const handlerRef = useRef(handler);
  useEffect(() => {
    handlerRef.current = handler;
  });
  useEffect(() => subscribe(eventName, (payload) => handlerRef.current(payload)), [subscribe, eventName]);
}

/**
 * Yedek polling: bağlantı yokken (bağlanıyor, yeniden bağlanıyor, çevrimdışı) poll'u aralıkla çağırır,
 * bağlantı kurulunca durur. Bağlantı geri geldiğinde arada kaçan olaylar için bir kez daha çağırır.
 * İlk yükleme bileşenin kendi işidir, bu hook onu yapmaz.
 */
export function usePollingFallback(poll, intervalMs, enabled = true) {
  const { isLive } = useRealtime();
  const pollRef = useRef(poll);
  const wasLiveRef = useRef(isLive);

  useEffect(() => {
    pollRef.current = poll;
  });

  useEffect(() => {
    if (!enabled) return undefined;
    if (isLive) {
      if (!wasLiveRef.current) pollRef.current();
      wasLiveRef.current = true;
      return undefined;
    }
    wasLiveRef.current = false;
    const id = setInterval(() => pollRef.current(), intervalMs);
    return () => clearInterval(id);
  }, [isLive, enabled, intervalMs]);
}
