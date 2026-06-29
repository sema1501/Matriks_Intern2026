import { useState, useEffect, useRef } from 'react';
import { buildStreamUrl } from '../services/binanceService';
import { TRACKED_SYMBOLS } from '../data/trackedSymbols';

export function useBinancePrices() {
  const [prices, setPrices] = useState({});
  const [connectionStatus, setConnectionStatus] = useState('connecting');
  const wsRef = useRef(null);
  const reconnectTimerRef = useRef(null);
  const isMountedRef = useRef(true);

  useEffect(() => {
    isMountedRef.current = true;

    function connect() {
      if (!isMountedRef.current) return;

      setConnectionStatus('connecting');
      const url = buildStreamUrl(TRACKED_SYMBOLS);
      const ws = new WebSocket(url);
      wsRef.current = ws;

      ws.onopen = () => {
        if (isMountedRef.current) setConnectionStatus('connected');
      };

      ws.onmessage = (event) => {
        if (!isMountedRef.current) return;
        const { data: ticker } = JSON.parse(event.data);
        if (!ticker || !ticker.s) return;

        setPrices(prev => ({
          ...prev,
          [ticker.s]: {
            symbol: ticker.s,
            currentPrice: parseFloat(ticker.c),
            priceChangePercentage24h: parseFloat(ticker.P),
            high24h: parseFloat(ticker.h),
            low24h: parseFloat(ticker.l),
          },
        }));
      };

      const scheduleReconnect = () => {
        if (!isMountedRef.current) return;
        setConnectionStatus('disconnected');
        reconnectTimerRef.current = setTimeout(() => {
          if (isMountedRef.current) connect();
        }, 3000);
      };

      ws.onclose = scheduleReconnect;
      ws.onerror = scheduleReconnect;
    }

    connect();

    return () => {
      isMountedRef.current = false;
      clearTimeout(reconnectTimerRef.current);
      if (wsRef.current) wsRef.current.close();
    };
  }, []);

  return { prices, connectionStatus };
}
